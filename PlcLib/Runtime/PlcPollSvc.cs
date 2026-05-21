using System.Diagnostics;
using System.Globalization;
using System.Text;
using PlcLib.Abstractions;
using PlcLib.Common;
using PlcLib.Options;

namespace PlcLib.Runtime;

/// <summary>
/// PLC 아이템을 주기적으로 폴링하고 캐시를 갱신하는 서비스입니다.
///
/// 폴링 전략:
///   - 연속 주소(Prefix+숫자 형식) → BlockRead로 묶음 처리 (PlcProfile 기준 최적화)
///   - 산발적 주소 → RandomRead 배치 처리
///   - 단일 또는 파싱 불가 주소 → 단건 Read 처리
///   - String, 32bit 타입 → 워드 단위 읽기/쓰기 최적화
///   - WordBit(예: "D100.3") → 워드 읽기 후 비트 추출
///
/// S7 DB 주소("DB1.DBW10" 등)는 Prefix 파싱이 불가하여 RandomRead로 처리됩니다.
/// </summary>
public sealed class PlcPollSvc : IDisposable
{
    private readonly object _sync = new object();

    private readonly IPlcClient _plc;
    private readonly Dictionary<string, PlcItemOpt> _items;
    private readonly Dictionary<string, PlcItemOpt> _customs;
    private readonly Dictionary<string, PlcItemSnap> _snaps;
    private readonly Dictionary<string, PlcItemSnap> _addrSnaps;
    private readonly Queue<WriteRequest> _writeQ;
    private readonly int _pollMs;
    private readonly int _reconnMs;
    private readonly string? _hbKey;
    private readonly int _hbTimeoutMs;

    private CancellationTokenSource? _cts;
    private Task? _pollTask;
    private DateTime _nextReconnUtc;
    private int _ioFailCount;
    private bool _lastConnState;
    private int _lastPollMs;
    private int _lastBlockCount;
    private int _lastRandomCount;
    private int _lastSingleCount;
    private object? _lastHeartbeat;
    private DateTime _lastHeartbeatUtc;
    private bool _heartbeatInit;
    private bool _disposed;

    public event EventHandler<PlcItemSnap>? ItemUpdated;
    public event EventHandler? PollCycleCompleted;
    public event EventHandler<ConnArgs>? ConnectionChanged;

    public PlcPollSvc(
        IPlcClient plc,
        IReadOnlyList<PlcItemOpt>? items = null,
        int pollIntvMs = 100,
        int reconnIntvMs = 1000,
        string? heartbeatKey = null,
        int hbTimeoutMs = 0)
    {
        if (plc == null) throw new ArgumentNullException(nameof(plc));
        if (pollIntvMs <= 0)
            throw new ArgumentOutOfRangeException(nameof(pollIntvMs), "Poll interval must be at least 1ms.");
        if (reconnIntvMs < 100)
            throw new ArgumentOutOfRangeException(nameof(reconnIntvMs), "Reconnect interval must be at least 100ms.");
        if (hbTimeoutMs < 0)
            throw new ArgumentOutOfRangeException(nameof(hbTimeoutMs), "heartbeat 타임아웃은 0 이상이어야 합니다.");

        _plc         = plc;
        _pollMs      = pollIntvMs;
        _reconnMs    = reconnIntvMs;
        _hbKey       = string.IsNullOrWhiteSpace(heartbeatKey) ? null : heartbeatKey.Trim();
        _hbTimeoutMs = hbTimeoutMs;
        _writeQ      = new Queue<WriteRequest>();
        _customs     = new Dictionary<string, PlcItemOpt>(StringComparer.OrdinalIgnoreCase);
        _snaps       = new Dictionary<string, PlcItemSnap>(StringComparer.OrdinalIgnoreCase);
        _addrSnaps   = new Dictionary<string, PlcItemSnap>(StringComparer.OrdinalIgnoreCase);
        _items       = (items ?? Array.Empty<PlcItemOpt>())
            .Where(x => !string.IsNullOrWhiteSpace(x.Key))
            .ToDictionary(x => x.Key, StringComparer.OrdinalIgnoreCase);
        _nextReconnUtc    = DateTime.MinValue;
        _lastConnState    = _plc.IsConnected;
        _lastHeartbeatUtc = DateTime.UtcNow;

        Log(PlcLogLevel.Info, $"PLC polling service initialized. items={_items.Count}, pollMs={_pollMs}");
    }

    /// <summary>
    /// PlcPollOpt를 사용하는 편의 생성자입니다.
    /// </summary>
    public PlcPollSvc(
        IPlcClient plc,
        IReadOnlyList<PlcItemOpt>? items,
        PlcPollOpt pollOpt)
        : this(plc, items,
              pollOpt?.PollMs    ?? 100,
              pollOpt?.ReconnMs  ?? 1000,
              pollOpt?.HbKey,
              pollOpt?.HbMs      ?? 0)
    { }

    public string ProviderName => _plc.ProviderName;

    public bool IsRunning
    {
        get { lock (_sync) { return _pollTask != null && !_pollTask.IsCompleted; } }
    }

    public bool IsConnected
    {
        get { lock (_sync) { return _lastConnState; } }
    }

    public int LastPollMs
    {
        get { lock (_sync) { return _lastPollMs; } }
    }

    public int LastBlockCount
    {
        get { lock (_sync) { return _lastBlockCount; } }
    }

    public int LastRandomCount
    {
        get { lock (_sync) { return _lastRandomCount; } }
    }

    public int LastSingleCount
    {
        get { lock (_sync) { return _lastSingleCount; } }
    }

    // ── 서비스 제어 ───────────────────────────────────────────────────

    public void Start()
    {
        lock (_sync)
        {
            ThrowIfDisposed();
            if (_pollTask != null && !_pollTask.IsCompleted) return;
            _cts      = new CancellationTokenSource();
            _pollTask = Task.Run(() => PollLoopAsync(_cts.Token));
            Log(PlcLogLevel.Info, "PLC item polling service started.");
        }
    }

    public void Stop()
    {
        Task? runningTask = null;
        lock (_sync)
        {
            if (_pollTask == null) return;
            runningTask = _pollTask;
            _cts?.Cancel();
        }

        try
        {
            runningTask.Wait();
        }
        catch (AggregateException ex)
        {
            var flattened = ex.Flatten();
            var onlyCancellation = flattened.InnerExceptions.All(
                e => e is TaskCanceledException or OperationCanceledException);
            if (!onlyCancellation)
                Log(PlcLogLevel.Warning, "PLC item polling service stop observed non-cancellation exception.", ex);
        }

        lock (_sync)
        {
            _pollTask = null;
            _cts?.Dispose();
            _cts = null;
        }

        Log(PlcLogLevel.Info, "PLC item polling service stopped.");
    }

    // ── 쓰기 큐 ──────────────────────────────────────────────────────

    /// <summary>
    /// 아이템 키 기준으로 쓰기 요청을 큐에 등록합니다. 실제 쓰기는 다음 폴링 사이클에서 수행됩니다.
    /// </summary>
    public void EnqueueWrite<T>(string itemKey, T value)
    {
        if (string.IsNullOrWhiteSpace(itemKey))
            throw new ArgumentException("itemKey가 비어 있습니다.", nameof(itemKey));

        var item = GetItem(itemKey);
        if (!item.Writable) throw new InvalidOperationException("Write-protected item: " + itemKey);
        EnsureWriteType(item, typeof(T));

        lock (_sync)
        {
            ThrowIfDisposed();
            _writeQ.Enqueue(new WriteRequest(item, value!));
        }
    }

    // ── 캐시 조회 ─────────────────────────────────────────────────────

    public bool TryGetValue<T>(string itemKey, out T value)
    {
        value = default!;
        if (string.IsNullOrWhiteSpace(itemKey)) return false;
        lock (_sync)
        {
            if (!_snaps.TryGetValue(itemKey, out var snap)) return false;
            if (snap.Value is T typed) { value = typed; return true; }
        }
        return false;
    }

    public bool TryGetValueByAddress<T>(string address, out T value)
    {
        value = default!;
        if (string.IsNullOrWhiteSpace(address)) return false;
        lock (_sync)
        {
            if (!_addrSnaps.TryGetValue(address, out var snap)) return false;
            if (snap.Value is T typed) { value = typed; return true; }
        }
        return false;
    }

    public bool TryGetItemType(string itemKey, out PlcValueType valueType)
    {
        valueType = default;
        if (string.IsNullOrWhiteSpace(itemKey)) return false;
        lock (_sync)
        {
            PlcItemOpt item;
            if (!_items.TryGetValue(itemKey, out item!) && !_customs.TryGetValue(itemKey, out item!))
                return false;
            valueType = item.Type;
            return true;
        }
    }

    public bool TryGetItem(string itemKey, out PlcItemOpt item)
    {
        item = null!;
        if (string.IsNullOrWhiteSpace(itemKey)) return false;
        lock (_sync)
        {
            if (_items.TryGetValue(itemKey, out var found) || _customs.TryGetValue(itemKey, out found))
            {
                item = found;
                return true;
            }
            return false;
        }
    }

    public IReadOnlyList<PlcItemSnap> GetSnapshots()
    {
        lock (_sync)
        {
            return _snaps.Values
                .OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }
    }

    // ── 동적 아이템 등록 ──────────────────────────────────────────────

    /// <summary>
    /// 폴링 루프에 사용자 정의 아이템을 동적으로 추가합니다. 반환된 토큰을 Dispose하면 해제됩니다.
    /// </summary>
    public IDisposable AddCustomItems(IReadOnlyList<PlcItemOpt> items)
    {
        if (items == null) throw new ArgumentNullException(nameof(items));
        var normalized = NormalizeCustoms(items);
        lock (_sync)
        {
            ThrowIfDisposed();
            foreach (var item in normalized)
            {
                if (_items.ContainsKey(item.Key) || _customs.ContainsKey(item.Key))
                    throw new InvalidOperationException("Duplicate Key registration: " + item.Key);
                _customs[item.Key] = item;
            }
        }
        return new CustomReg(this, normalized.Select(x => x.Key).ToArray());
    }

    public void Dispose()
    {
        lock (_sync)
        {
            if (_disposed) return;
        }
        Stop();
        lock (_sync)
        {
            _writeQ.Clear(); _snaps.Clear(); _addrSnaps.Clear(); _customs.Clear();
            _disposed = true;
        }
    }

    // ── 폴링 루프 ─────────────────────────────────────────────────────

    private async Task PollLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            var sw = Stopwatch.StartNew();
            try
            {
                if (!EnsureConn())
                {
                    SetPollMs(sw.ElapsedMilliseconds);
                    await Task.Delay(_pollMs, ct).ConfigureAwait(false);
                    continue;
                }

                FlushWriteQ();
                var stats = new ReadStats();
                PollAll(stats);
                SetStats(stats);
                PollCycleCompleted?.Invoke(this, EventArgs.Empty);
                CheckHeartbeat();
                _ioFailCount = 0;
            }
            catch (Exception ex)
            {
                AddFail(ex, "Polling loop error.");
            }

            sw.Stop();
            var elapsedMs = (int)Math.Min(int.MaxValue, sw.ElapsedMilliseconds);
            SetPollMs(elapsedMs);
            var delayMs = _pollMs - elapsedMs;
            if (delayMs > 0)
                await Task.Delay(delayMs, ct).ConfigureAwait(false);
            else
                await Task.Yield();
        }
    }

    private void SetPollMs(long ms)
    {
        var v = (int)Math.Max(0L, Math.Min(int.MaxValue, ms));
        lock (_sync) { _lastPollMs = v; }
    }

    private void SetStats(ReadStats stats)
    {
        lock (_sync)
        {
            _lastBlockCount  = stats.BlockReadCount;
            _lastRandomCount = stats.RandomReadCount;
            _lastSingleCount = stats.SingleReadCount;
        }
    }

    // ── 쓰기 큐 처리 ──────────────────────────────────────────────────

    private void FlushWriteQ()
    {
        var requests = new List<WriteRequest>();
        lock (_sync)
        {
            while (_writeQ.Count > 0) requests.Add(_writeQ.Dequeue());
        }
        if (requests.Count == 0) return;

        var latestByKey = new Dictionary<string, WriteRequest>(StringComparer.OrdinalIgnoreCase);
        foreach (var req in requests) latestByKey[req.Item.Key] = req;
        var latest = latestByKey.Values.ToArray();

        var wordBitReqs = latest.Where(x => IsWordBitItem(x.Item)).ToArray();
        var normalReqs  = wordBitReqs.Length > 0
            ? latest.Where(x => !IsWordBitItem(x.Item)).ToArray()
            : latest;

        WriteScalars<bool>(normalReqs, PlcValueType.Bool);
        ProcessWordWrites(normalReqs);
        if (wordBitReqs.Length > 0) WriteWordBitItems(wordBitReqs);
    }

    // ── 폴링 ──────────────────────────────────────────────────────────

    private void PollAll(ReadStats stats)
    {
        var pollItems = GetItems();

        var wordBitItems = pollItems.Where(IsWordBitItem).ToArray();
        var normalItems  = wordBitItems.Length > 0
            ? pollItems.Where(x => !IsWordBitItem(x)).ToArray()
            : pollItems;

        PollScalarItems<bool>(normalItems, PlcValueType.Bool, stats);

        var wordItems = wordBitItems.Length > 0
            ? InjectWordBitBases(normalItems, wordBitItems)
            : normalItems;

        PollWordItems(wordItems, stats);

        if (wordBitItems.Length > 0) ResolveWordBits(wordBitItems);
    }

    private bool IsWordBitItem(PlcItemOpt item)
        => item.Type == PlcValueType.Bool
        && item.Address.Contains('.')
        && _plc.ProviderName != "S7";

    // ── WordBit 처리 ──────────────────────────────────────────────────

    private void WriteWordBitItems(IReadOnlyList<WriteRequest> requests)
    {
        var groups = requests
            .Select(req =>
            {
                TryParseWordBitAddr(req.Item.Address, out var baseAddr, out var bit);
                return new { Req = req, BaseAddr = baseAddr, Bit = bit };
            })
            .Where(x => !string.IsNullOrEmpty(x.BaseAddr))
            .GroupBy(x => x.Req.Item.No + "|" + x.BaseAddr, StringComparer.OrdinalIgnoreCase);

        foreach (var group in groups)
        {
            var entries = group.ToList();
            var baseAddr = entries[0].BaseAddr;
            try
            {
                var word = _plc.Read<ushort>(baseAddr);
                foreach (var entry in entries)
                {
                    var on = entry.Req.Value is bool b && b;
                    if (on) word |=  (ushort)(1 << entry.Bit);
                    else    word &= (ushort)~(1 << entry.Bit);
                }
                _plc.Write(baseAddr, word);
                foreach (var entry in entries)
                    UpdateSnapshot(entry.Req.Item, ((word >> entry.Bit) & 1) == 1, true, null);
            }
            catch (Exception ex)
            {
                foreach (var entry in entries)
                    UpdateSnapshot(entry.Req.Item, null, false, ex.Message);
                AddFail(ex, "Word bit write failed.");
                Log(PlcLogLevel.Warning, "Word bit write failed. addr=" + baseAddr, ex);
            }
        }
    }

    private static bool TryParseWordBitAddr(string address, out string baseAddr, out int bitOffset)
    {
        baseAddr  = string.Empty;
        bitOffset = 0;
        var dotIdx = address.IndexOf('.');
        if (dotIdx < 0) return false;
        baseAddr = address.Substring(0, dotIdx);
        var bitText = address.Substring(dotIdx + 1);
        if (!int.TryParse(bitText, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out bitOffset))
            return false;
        return bitOffset >= 0 && bitOffset <= 15;
    }

    private const string WordBitBasePrefix = "__wb_";

    private static IReadOnlyList<PlcItemOpt> InjectWordBitBases(
        IReadOnlyList<PlcItemOpt> normalItems,
        IReadOnlyList<PlcItemOpt> wordBitItems)
    {
        var existingAddrs = new HashSet<string>(
            normalItems.Where(IsWordReadableType).Select(x => x.No + "|" + x.Address),
            StringComparer.OrdinalIgnoreCase);

        var merged = new List<PlcItemOpt>(normalItems);
        foreach (var item in wordBitItems)
        {
            if (!TryParseWordBitAddr(item.Address, out var baseAddr, out _)) continue;
            var addrKey = item.No + "|" + baseAddr;
            if (existingAddrs.Contains(addrKey)) continue;
            existingAddrs.Add(addrKey);
            merged.Add(new PlcItemOpt
            {
                No      = item.No,
                Key     = WordBitBasePrefix + baseAddr,
                Address = baseAddr,
                Type    = PlcValueType.UInt16,
            });
        }
        return merged;
    }

    private void ResolveWordBits(IReadOnlyList<PlcItemOpt> wordBitItems)
    {
        foreach (var item in wordBitItems)
        {
            if (!TryParseWordBitAddr(item.Address, out var baseAddr, out var bit)) continue;

            if (TryGetValueByAddress(baseAddr, out ushort uword))
            {
                UpdateSnapshot(item, ((uword >> bit) & 1) == 1, true, null);
                continue;
            }
            if (TryGetValueByAddress(baseAddr, out short sword))
                UpdateSnapshot(item, (((ushort)sword >> bit) & 1) == 1, true, null);
        }
    }

    // ── 워드 타입 폴링 ────────────────────────────────────────────────

    private void PollWordItems(IReadOnlyList<PlcItemOpt> pollItems, ReadStats readStats)
    {
        var units = BuildUnits(pollItems.Where(IsWordReadableType));
        if (units.Count == 0) return;

        var sortable      = new List<WordAddrRead>();
        var directFallback = new List<ReadUnit>();

        foreach (var unit in units)
        {
            var wordLength = GetWordLength(unit.Representative);
            if (wordLength <= 0)
            {
                var emptyValue = unit.Representative.Type == PlcValueType.String ? string.Empty : (object?)null;
                UpdateSnapshots(unit.Items, emptyValue, true, null);
                continue;
            }
            if (TryParseAddress(unit.Representative.Address, out var token))
                sortable.Add(new WordAddrRead(unit, token, wordLength));
            else
                directFallback.Add(unit);
        }

        var grouped = sortable
            .GroupBy(x => x.Unit.Representative.No + "|" + x.Token.Prefix, StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (var group in grouped)
        {
            var ordered = group.OrderBy(x => x.Token.Number).ToList();
            for (var runStart = 0; runStart < ordered.Count;)
            {
                var runEnd = FindWordRunEnd(
                    ordered, runStart,
                    _plc.Profile.MaxWordBlockPoints,
                    _plc.Profile.MaxGapPoints,
                    _plc.Profile.MaxUnusedPoints);
                var run           = ordered.GetRange(runStart, runEnd - runStart + 1);
                var startNumber   = run[0].Token.Number;
                var spanWordCount = GetWordSpanCount(run, startNumber);
                try
                {
                    readStats.BlockReadCount++;
                    var words = _plc.BlockRead<ushort>(run[0].Unit.Representative.Address, (ushort)spanWordCount);
                    foreach (var item in run)
                    {
                        var value = DecodeWordValue(item.Unit.Representative, words, item.Token.Number - startNumber);
                        UpdateSnapshots(item.Unit.Items, value, true, null);
                    }
                }
                catch (Exception ex)
                {
                    foreach (var item in run) UpdateSnapshots(item.Unit.Items, null, false, ex.Message);
                    AddFail(ex, "Word block read failed.");
                    Log(PlcLogLevel.Warning, "Word block read failed.", ex);
                }
                runStart = runEnd + 1;
            }
        }

        foreach (var unit in directFallback)
        {
            try
            {
                readStats.SingleReadCount++;
                UpdateSnapshots(unit.Items, ReadWordValue(unit.Representative), true, null);
            }
            catch (Exception ex)
            {
                UpdateSnapshots(unit.Items, null, false, ex.Message);
                AddFail(ex, "Word single read failed.");
                Log(PlcLogLevel.Warning, "Word single read failed.", ex);
            }
        }
    }

    // ── 스칼라 타입 폴링 ──────────────────────────────────────────────

    private void PollScalarItems<T>(IReadOnlyList<PlcItemOpt> pollItems, PlcValueType valueType, ReadStats readStats)
        where T : unmanaged
    {
        var units = BuildUnits(pollItems.Where(x => x.Type == valueType));
        if (units.Count == 0) return;

        var sortable       = new List<AddrRead>();
        var randomFallback = new List<ReadUnit>();

        foreach (var unit in units)
        {
            if (TryParseAddress(unit.Representative.Address, out var token))
                sortable.Add(new AddrRead(unit, token));
            else
                randomFallback.Add(unit);
        }

        var grouped = sortable
            .GroupBy(x => x.Unit.Representative.No + "|" + x.Token.Prefix, StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (var group in grouped)
        {
            var ordered      = group.OrderBy(x => x.Token.Number).ToList();
            var step         = GetAddressStep(valueType);
            var maxBlock     = GetMaxBlockCount(valueType);
            var maxGap       = GetMaxGapCount(valueType);
            var maxUnused    = GetMaxUnused(valueType);

            for (var runStart = 0; runStart < ordered.Count;)
            {
                var runEnd    = FindReadRunEnd(ordered, runStart, step, maxBlock, maxGap, maxUnused);
                var itemCount = runEnd - runStart + 1;
                if (itemCount < 2) { randomFallback.Add(ordered[runStart].Unit); runStart++; continue; }

                var spanCount = GetSpanItemCount(ordered[runStart].Token.Number, ordered[runEnd].Token.Number, step);
                var run       = ordered.GetRange(runStart, itemCount);
                try
                {
                    readStats.BlockReadCount++;
                    var values = _plc.BlockRead<T>(run[0].Unit.Representative.Address, (ushort)spanCount);
                    for (var j = 0; j < itemCount; j++)
                    {
                        var valueIndex = GetSpanItemCount(run[0].Token.Number, run[j].Token.Number, step) - 1;
                        UpdateSnapshots(run[j].Unit.Items, values[valueIndex], true, null);
                    }
                }
                catch (Exception ex)
                {
                    foreach (var r in run) UpdateSnapshots(r.Unit.Items, null, false, ex.Message);
                    AddFail(ex, "Block read failed.");
                    Log(PlcLogLevel.Warning, "Block read failed.", ex);
                }
                runStart = runEnd + 1;
            }
        }

        if (randomFallback.Count == 0) return;

        if (randomFallback.Count == 1)
        {
            var item = randomFallback[0];
            try
            {
                readStats.SingleReadCount++;
                UpdateSnapshots(item.Items, _plc.Read<T>(item.Representative.Address), true, null);
            }
            catch (Exception ex)
            {
                UpdateSnapshots(item.Items, null, false, ex.Message);
                AddFail(ex, "Single read failed.");
                Log(PlcLogLevel.Warning, "Single read failed.", ex);
            }
            return;
        }

        try
        {
            readStats.RandomReadCount++;
            var addresses = randomFallback.Select(x => x.Representative.Address).ToArray();
            var values = _plc.RandomRead<T>(addresses);
            foreach (var item in randomFallback)
            {
                if (values.TryGetValue(item.Representative.Address, out T val))
                    UpdateSnapshots(item.Items, val, true, null);
                else
                    UpdateSnapshots(item.Items, null, false, "RandomRead 결과 누락");
            }
        }
        catch (Exception ex)
        {
            foreach (var item in randomFallback) UpdateSnapshots(item.Items, null, false, ex.Message);
            AddFail(ex, "Random read failed.");
            Log(PlcLogLevel.Warning, "Random read failed.", ex);
        }
    }

    // ── 워드 쓰기 처리 ────────────────────────────────────────────────

    private void ProcessWordWrites(IReadOnlyList<WriteRequest> requests)
    {
        var wordRequests = requests
            .Where(x => IsWordReadableType(x.Item))
            .Select((x, index) => CreateWordReq(x, index))
            .ToList();
        if (wordRequests.Count == 0) return;

        var sortable      = new List<WordAddrWriteReq>();
        var directFallback = new List<WordWriteReq>();

        foreach (var request in wordRequests)
        {
            if (TryParseAddress(request.Item.Address, out var token))
                sortable.Add(new WordAddrWriteReq(request, token));
            else
                directFallback.Add(request);
        }

        var grouped = sortable
            .GroupBy(x => x.Request.Item.No + "|" + x.Token.Prefix, StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (var group in grouped)
        {
            var ordered = group.OrderBy(x => x.Token.Number).ToList();
            for (var runStart = 0; runStart < ordered.Count;)
            {
                var runEnd = FindWordWriteEnd(ordered, runStart, _plc.Profile.MaxWordBlockPoints);
                var run    = ordered.GetRange(runStart, runEnd - runStart + 1);
                try { WriteWordRun(run); }
                catch (Exception ex)
                {
                    AddFail(ex, "Word block write failed. It will retry next cycle.");
                    Log(PlcLogLevel.Warning, "Word block write failed. It will retry next cycle.", ex);
                }
                runStart = runEnd + 1;
            }
        }

        foreach (var request in directFallback)
        {
            try { WriteWordValue(request.Item, request.Words); }
            catch (Exception ex)
            {
                AddFail(ex, "Word single write failed. It will retry next cycle.");
                Log(PlcLogLevel.Warning, "Word single write failed. It will retry next cycle.", ex);
            }
        }
    }

    private void WriteWordRun(IReadOnlyList<WordAddrWriteReq> run)
    {
        var startNumber   = run.Min(x => x.Token.Number);
        var endNumber     = run.Max(x => x.Token.Number + x.Request.Words.Length - 1);
        var spanWordCount = endNumber - startNumber + 1;
        var buffer        = new ushort[spanWordCount];

        foreach (var request in run.OrderBy(x => x.Request.Sequence))
        {
            var offset = request.Token.Number - startNumber;
            Array.Copy(request.Request.Words, 0, buffer, offset, request.Request.Words.Length);
        }
        _plc.BlockWrite(run[0].Request.Item.Address, buffer);
    }

    // ── 스칼라 쓰기 처리 ──────────────────────────────────────────────

    private void WriteScalars<T>(IReadOnlyList<WriteRequest> requests, PlcValueType valueType) where T : unmanaged
    {
        var typedRequests = requests
            .Where(x => x.Item.Type == valueType)
            .Select(x => new TypedWriteReq<T>(x.Item, (T)x.Value))
            .ToList();
        if (typedRequests.Count == 0) return;

        var latestByAddress = new Dictionary<string, TypedWriteReq<T>>(StringComparer.OrdinalIgnoreCase);
        foreach (var req in typedRequests) latestByAddress[req.Item.Address] = req;

        var latest         = latestByAddress.Values.ToList();
        var sortable       = new List<TypedAddrWriteReq<T>>();
        var randomFallback = new List<TypedWriteReq<T>>();

        foreach (var req in latest)
        {
            if (TryParseAddress(req.Item.Address, out var token))
                sortable.Add(new TypedAddrWriteReq<T>(req, token));
            else
                randomFallback.Add(req);
        }

        var grouped = sortable
            .GroupBy(x => x.Request.Item.No + "|" + x.Token.Prefix, StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (var group in grouped)
        {
            var ordered  = group.OrderBy(x => x.Token.Number).ToList();
            var runStart = 0;
            for (var i = 1; i <= ordered.Count; i++)
            {
                var breakRun = i == ordered.Count || ordered[i].Token.Number != ordered[i - 1].Token.Number + 1;
                if (!breakRun) continue;
                var runCount = i - runStart;
                if (runCount >= 2)
                {
                    var run = ordered.GetRange(runStart, runCount);
                    try
                    {
                        var values = run.Select(x => x.Request.Value).ToArray();
                        _plc.BlockWrite(run[0].Request.Item.Address, values);
                    }
                    catch (Exception ex)
                    {
                        AddFail(ex, "Block write failed. It will retry next cycle.");
                        Log(PlcLogLevel.Warning, "Block write failed. It will retry next cycle.", ex);
                    }
                }
                else
                {
                    randomFallback.Add(ordered[runStart].Request);
                }
                runStart = i;
            }
        }

        if (randomFallback.Count == 0) return;

        if (randomFallback.Count == 1)
        {
            try { var one = randomFallback[0]; _plc.Write(one.Item.Address, one.Value); }
            catch (Exception ex)
            {
                AddFail(ex, "Single write failed. It will retry next cycle.");
                Log(PlcLogLevel.Warning, "Single write failed. It will retry next cycle.", ex);
            }
            return;
        }

        try
        {
            var map = new Dictionary<string, T>(StringComparer.OrdinalIgnoreCase);
            foreach (var req in randomFallback) map[req.Item.Address] = req.Value;
            _plc.RandomWrite(map);
        }
        catch (Exception ex)
        {
            AddFail(ex, "Random write failed. It will retry next cycle.");
            Log(PlcLogLevel.Warning, "Random write failed. It will retry next cycle.", ex);
        }
    }

    // ── 연결 관리 ─────────────────────────────────────────────────────

    private bool EnsureConn()
    {
        if (_plc.IsConnected) { PublishConn(true); return true; }

        var now = DateTime.UtcNow;
        if (now < _nextReconnUtc) { PublishConn(false); return false; }

        try
        {
            _plc.Connect();
            _ioFailCount      = 0;
            _nextReconnUtc    = DateTime.MinValue;
            _heartbeatInit    = false;
            _lastHeartbeat    = null;
            _lastHeartbeatUtc = DateTime.UtcNow;
            PublishConn(true);
            Log(PlcLogLevel.Info, "PLC reconnected.");
            return true;
        }
        catch (Exception ex)
        {
            _nextReconnUtc = now.AddMilliseconds(_reconnMs);
            PublishConn(false);
            Log(PlcLogLevel.Warning, "PLC reconnect failed.", ex);
            return false;
        }
    }

    private void AddFail(Exception ex, string logMessage)
    {
        _ioFailCount++;
        if (_ioFailCount < 3) return;
        _ioFailCount = 0;
        PublishConn(false);
        ForceReconnect(logMessage, ex);
    }

    private void PublishConn(bool isConnected)
    {
        if (_lastConnState == isConnected) return;
        _lastConnState = isConnected;
        ConnectionChanged?.Invoke(this, new ConnArgs(isConnected));
    }

    private void CheckHeartbeat()
    {
        if (string.IsNullOrWhiteSpace(_hbKey) || _hbTimeoutMs <= 0) return;
        if (!_heartbeatInit) return;
        var elapsed = DateTime.UtcNow - _lastHeartbeatUtc;
        if (elapsed.TotalMilliseconds < _hbTimeoutMs) return;
        ForceReconnect($"PLC heartbeat timeout. key={_hbKey}, elapsedMs={(int)elapsed.TotalMilliseconds}", null);
    }

    private void ForceReconnect(string reason, Exception? exception)
    {
        _heartbeatInit    = false;
        _lastHeartbeat    = null;
        _lastHeartbeatUtc = DateTime.UtcNow;
        PublishConn(false);
        try { _plc.Disconnect(); }
        catch (Exception ex) { Log(PlcLogLevel.Warning, "PLC disconnect during reconnect flow failed.", ex); }
        _nextReconnUtc = DateTime.UtcNow.AddMilliseconds(_reconnMs);
        Log(PlcLogLevel.Warning, $"{reason} Reconnect scheduled.", exception);
    }

    // ── 워드 인코딩/디코딩 ────────────────────────────────────────────

    private object ReadWordValue(PlcItemOpt item)
    {
        switch (item.Type)
        {
            case PlcValueType.Int16:   return _plc.Read<short>(item.Address);
            case PlcValueType.UInt16:  return _plc.Read<ushort>(item.Address);
            case PlcValueType.Int32:   return _plc.Read<int>(item.Address);
            case PlcValueType.UInt32:  return _plc.Read<uint>(item.Address);
            case PlcValueType.Float:   return _plc.Read<float>(item.Address);
            case PlcValueType.String:  return ReadString(item);
            default: throw new InvalidOperationException("Unsupported word read type: " + item.Type);
        }
    }

    private static object DecodeWordValue(PlcItemOpt item, ushort[] words, int startIndex)
    {
        if (startIndex < 0 || startIndex >= words.Length)
            throw new ArgumentOutOfRangeException(nameof(startIndex));
        switch (item.Type)
        {
            case PlcValueType.Int16:  return unchecked((short)words[startIndex]);
            case PlcValueType.UInt16: return words[startIndex];
            case PlcValueType.Int32:  return unchecked((int)ReadUInt32Words(words, startIndex));
            case PlcValueType.UInt32: return ReadUInt32Words(words, startIndex);
            case PlcValueType.Float:  return BitConverter.Int32BitsToSingle(unchecked((int)ReadUInt32Words(words, startIndex)));
            case PlcValueType.String: return DecodeStringWords(words, startIndex, item.StringWordLength, item.ByteOrder);
            default: throw new InvalidOperationException("Unsupported word decode type: " + item.Type);
        }
    }

    private static uint ReadUInt32Words(ushort[] words, int startIndex)
    {
        if (startIndex + 1 >= words.Length)
            throw new InvalidOperationException("32비트 값을 읽기 위한 워드 수가 부족합니다.");
        return (uint)words[startIndex] | ((uint)words[startIndex + 1] << 16);
    }

    private string ReadString(PlcItemOpt item)
    {
        if (item.StringWordLength == 0) return string.Empty;
        var words = _plc.BlockRead<ushort>(item.Address, item.StringWordLength);
        return DecodeStringWords(words, 0, item.StringWordLength, item.ByteOrder);
    }

    private static string DecodeStringWords(ushort[] words, int startIndex, int wordLength, PlcByteOrder byteOrder)
    {
        if (wordLength <= 0) return string.Empty;
        if (startIndex + wordLength > words.Length)
            throw new InvalidOperationException("Insufficient words to read string.");
        var bytes = new byte[wordLength * 2];
        for (var i = 0; i < wordLength; i++)
        {
            var word = words[startIndex + i];
            var lo   = (byte)(word & 0x00FF);
            var hi   = (byte)((word >> 8) & 0x00FF);
            var offset = i * 2;
            if (byteOrder == PlcByteOrder.LittleEndian) { bytes[offset] = lo; bytes[offset + 1] = hi; }
            else                                         { bytes[offset] = hi; bytes[offset + 1] = lo; }
        }
        var zeroIndex = Array.IndexOf(bytes, (byte)0);
        var length    = zeroIndex >= 0 ? zeroIndex : bytes.Length;
        return Encoding.ASCII.GetString(bytes, 0, length);
    }

    private static WordWriteReq CreateWordReq(WriteRequest request, int sequence)
        => new WordWriteReq(request.Item, sequence, EncodeWordValue(request.Item, request.Value));

    private static ushort[] EncodeWordValue(PlcItemOpt item, object value)
    {
        switch (item.Type)
        {
            case PlcValueType.Int16:  return new[] { unchecked((ushort)(short)value) };
            case PlcValueType.UInt16: return new[] { (ushort)value };
            case PlcValueType.Int32:  return EncodeUInt32Words(unchecked((uint)(int)value));
            case PlcValueType.UInt32: return EncodeUInt32Words((uint)value);
            case PlcValueType.Float:  return EncodeUInt32Words(unchecked((uint)BitConverter.SingleToInt32Bits((float)value)));
            case PlcValueType.String: return EncodeStringWords(item, (string)value);
            default: throw new InvalidOperationException("Unsupported word encode type: " + item.Type);
        }
    }

    private static ushort[] EncodeUInt32Words(uint value)
        => new[] { unchecked((ushort)(value & 0xFFFF)), unchecked((ushort)((value >> 16) & 0xFFFF)) };

    private static ushort[] EncodeStringWords(PlcItemOpt item, string value)
    {
        if (item.StringWordLength == 0) return Array.Empty<ushort>();
        var bytes  = Encoding.ASCII.GetBytes(value ?? string.Empty);
        var buffer = new byte[item.StringWordLength * 2];
        Array.Copy(bytes, buffer, Math.Min(bytes.Length, buffer.Length));
        var words = new ushort[item.StringWordLength];
        for (var i = 0; i < words.Length; i++)
        {
            var offset = i * 2;
            byte lo, hi;
            if (item.ByteOrder == PlcByteOrder.LittleEndian) { lo = buffer[offset]; hi = buffer[offset + 1]; }
            else                                              { hi = buffer[offset]; lo = buffer[offset + 1]; }
            words[i] = (ushort)(lo | (hi << 8));
        }
        return words;
    }

    private void WriteWordValue(PlcItemOpt item, ushort[] words)
    {
        if (words.Length == 0) return;
        _plc.BlockWrite(item.Address, words);
    }

    // ── 주소 분석 헬퍼 ────────────────────────────────────────────────

    private static bool IsWordReadableType(PlcItemOpt item)
        => item.Type == PlcValueType.Int16
        || item.Type == PlcValueType.UInt16
        || item.Type == PlcValueType.Int32
        || item.Type == PlcValueType.UInt32
        || item.Type == PlcValueType.Float
        || item.Type == PlcValueType.String;

    private static int GetWordLength(PlcItemOpt item) => item.Type switch
    {
        PlcValueType.Int16  or PlcValueType.UInt16  => 1,
        PlcValueType.Int32  or PlcValueType.UInt32
            or PlcValueType.Float                    => 2,
        PlcValueType.String                          => item.StringWordLength,
        _                                            => 0,
    };

    private static int GetAddressStep(PlcValueType vt) => vt switch
    {
        PlcValueType.Int32 or PlcValueType.UInt32 or PlcValueType.Float => 2,
        _ => 1,
    };

    private int GetMaxBlockCount(PlcValueType vt)
    {
        var max = vt == PlcValueType.Bool ? _plc.Profile.MaxBitBlockPoints : _plc.Profile.MaxWordBlockPoints;
        return Math.Max(1, max / GetAddressStep(vt));
    }

    private int GetMaxGapCount(PlcValueType vt)
        => Math.Max(0, _plc.Profile.MaxGapPoints / GetAddressStep(vt));

    private int GetMaxUnused(PlcValueType vt)
        => Math.Max(0, _plc.Profile.MaxUnusedPoints / GetAddressStep(vt));

    /// <summary>
    /// 블록 최적화 가능한 Prefix+숫자 형식 주소인지 파싱합니다.
    /// S7 DB 주소("DB1.DBW10" 등)는 파싱 불가로 처리됩니다.
    /// </summary>
    private static bool TryParseAddress(string address, out AddressToken token)
    {
        token = default;
        if (string.IsNullOrWhiteSpace(address)) return false;
        var trimmed = address.Trim();
        var prefixLen = 0;
        while (prefixLen < trimmed.Length && char.IsLetter(trimmed[prefixLen])) prefixLen++;
        if (prefixLen <= 0 || prefixLen >= trimmed.Length) return false;
        var prefix     = trimmed.Substring(0, prefixLen);
        var numberText = trimmed.Substring(prefixLen);
        var radix      = GetAddressRadix(prefix);
        int number;
        if (radix == 16)
        {
            if (!int.TryParse(numberText, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out number))
                return false;
        }
        else
        {
            if (!int.TryParse(numberText, NumberStyles.Integer, CultureInfo.InvariantCulture, out number))
                return false;
        }
        if (number < 0) return false;
        token = new AddressToken(prefix, number);
        return true;
    }

    private static int GetAddressRadix(string prefix) => prefix.ToUpperInvariant() switch
    {
        "X" or "Y" or "B" or "W" or "SB" or "SW" or "DX" or "DY" => 16,
        _ => 10,
    };

    // ── 런 탐색 알고리즘 ──────────────────────────────────────────────

    private static int FindReadRunEnd(
        IReadOnlyList<AddrRead> ordered, int startIndex, int step,
        int maxBlockItemCount, int maxGapItemCount, int maxUnusedItemCount)
    {
        var startNumber  = ordered[startIndex].Token.Number;
        var endIndex     = startIndex;
        var unusedItemCount = 0;

        for (var i = startIndex + 1; i < ordered.Count; i++)
        {
            var offset = ordered[i].Token.Number - startNumber;
            if (offset <= 0 || offset % step != 0) break;
            var gapItemCount  = (ordered[i].Token.Number - ordered[endIndex].Token.Number) / step - 1;
            if (gapItemCount < 0) break;
            var spanItemCount = offset / step + 1;
            var nextUnused    = spanItemCount - (i - startIndex + 1);
            if (spanItemCount > maxBlockItemCount || gapItemCount > maxGapItemCount || nextUnused > maxUnusedItemCount) break;
            unusedItemCount = nextUnused;
            endIndex = i;
        }
        return unusedItemCount >= 0 ? endIndex : startIndex;
    }

    private static int GetSpanItemCount(int startNumber, int endNumber, int step)
        => (endNumber - startNumber) / step + 1;

    private static int FindWordRunEnd(
        IReadOnlyList<WordAddrRead> ordered, int startIndex,
        int maxBlockWordCount, int maxGapWordCount, int maxUnusedWordCount)
    {
        var startNumber      = ordered[startIndex].Token.Number;
        var coveredEndNumber = startNumber + ordered[startIndex].WordLength - 1;
        var usedWordCount    = ordered[startIndex].WordLength;
        var endIndex         = startIndex;

        for (var i = startIndex + 1; i < ordered.Count; i++)
        {
            var nextStartNumber   = ordered[i].Token.Number;
            var nextEndNumber     = nextStartNumber + ordered[i].WordLength - 1;
            var gapWordCount      = Math.Max(0, nextStartNumber - coveredEndNumber - 1);
            var candidateCovEnd   = Math.Max(coveredEndNumber, nextEndNumber);
            var candidateSpan     = candidateCovEnd - startNumber + 1;
            var addlUsed          = Math.Max(0, nextEndNumber - Math.Max(coveredEndNumber + 1, nextStartNumber) + 1);
            var candidateUsed     = usedWordCount + addlUsed;
            var candidateUnused   = candidateSpan - candidateUsed;

            if (candidateSpan > maxBlockWordCount || gapWordCount > maxGapWordCount || candidateUnused > maxUnusedWordCount) break;
            coveredEndNumber = candidateCovEnd;
            usedWordCount    = candidateUsed;
            endIndex = i;
        }
        return endIndex;
    }

    private static int GetWordSpanCount(IReadOnlyList<WordAddrRead> run, int startNumber)
    {
        var endNumber = run.Max(x => x.Token.Number + x.WordLength - 1);
        return endNumber - startNumber + 1;
    }

    private static int FindWordWriteEnd(
        IReadOnlyList<WordAddrWriteReq> ordered, int startIndex, int maxBlockWordCount)
    {
        var startNumber      = ordered[startIndex].Token.Number;
        var coveredEndNumber = startNumber + ordered[startIndex].Request.Words.Length - 1;
        var endIndex         = startIndex;

        for (var i = startIndex + 1; i < ordered.Count; i++)
        {
            var nextStartNumber = ordered[i].Token.Number;
            var nextEndNumber   = nextStartNumber + ordered[i].Request.Words.Length - 1;
            if (nextStartNumber > coveredEndNumber + 1) break;
            var nextCov  = Math.Max(coveredEndNumber, nextEndNumber);
            var nextSpan = nextCov - startNumber + 1;
            if (nextSpan > maxBlockWordCount) break;
            coveredEndNumber = nextCov;
            endIndex = i;
        }
        return endIndex;
    }

    // ── 내부 유틸 ─────────────────────────────────────────────────────

    private IReadOnlyList<PlcItemOpt> GetItems()
    {
        lock (_sync) { return _items.Values.Concat(_customs.Values).ToArray(); }
    }

    private static List<ReadUnit> BuildUnits(IEnumerable<PlcItemOpt> items)
        => items
            .GroupBy(GetReadKey, StringComparer.OrdinalIgnoreCase)
            .Select(g => new ReadUnit(g.First(), g.ToArray()))
            .ToList();

    private static string GetReadKey(PlcItemOpt item)
        => item.No.ToString(CultureInfo.InvariantCulture)
         + "|" + item.Address
         + "|" + item.Type
         + "|" + item.StringWordLength.ToString(CultureInfo.InvariantCulture)
         + "|" + item.ByteOrder;

    private static List<PlcItemOpt> NormalizeCustoms(IReadOnlyList<PlcItemOpt> items)
    {
        var normalized = new List<PlcItemOpt>(items.Count);
        var itemKeys   = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in items)
        {
            if (item == null) throw new ArgumentException("null custom item은 등록할 수 없습니다.", nameof(items));
            if (string.IsNullOrWhiteSpace(item.Key)) throw new ArgumentException("custom item의 Key가 비어 있습니다.", nameof(items));
            if (string.IsNullOrWhiteSpace(item.Address)) throw new ArgumentException("custom item의 Address가 비어 있습니다. key=" + item.Key, nameof(items));
            if (!itemKeys.Add(item.Key)) throw new ArgumentException("Duplicate custom item key: " + item.Key, nameof(items));
            normalized.Add(new PlcItemOpt
            {
                Category         = item.Category,
                No               = item.No,
                Key              = item.Key,
                Name             = item.Name,
                Address          = item.Address,
                Description      = item.Description,
                Type             = item.Type,
                StringWordLength = item.StringWordLength,
                ByteOrder        = item.ByteOrder,
                Writable         = item.Writable,
            });
        }
        return normalized;
    }

    private void RemoveCustoms(string[] itemKeys)
    {
        lock (_sync)
        {
            if (_disposed) return;
            foreach (var itemKey in itemKeys)
            {
                if (!_customs.TryGetValue(itemKey, out var item)) continue;
                _customs.Remove(itemKey);
                _snaps.Remove(itemKey);

                var hasSameAddress =
                    _items.Values.Any(x => string.Equals(x.Address, item.Address, StringComparison.OrdinalIgnoreCase))
                    || _customs.Values.Any(x => string.Equals(x.Address, item.Address, StringComparison.OrdinalIgnoreCase));
                if (!hasSameAddress) { _addrSnaps.Remove(item.Address); continue; }

                var replacement = _snaps.Values
                    .Where(x => string.Equals(x.Address, item.Address, StringComparison.OrdinalIgnoreCase))
                    .OrderByDescending(x => x.TimestampUtc)
                    .FirstOrDefault();
                if (replacement == null) _addrSnaps.Remove(item.Address);
                else                     _addrSnaps[item.Address] = replacement;
            }
        }
    }

    private static void EnsureWriteType(PlcItemOpt item, Type valueType)
    {
        var ok = item.Type switch
        {
            PlcValueType.Bool   => valueType == typeof(bool),
            PlcValueType.Int16  => valueType == typeof(short),
            PlcValueType.UInt16 => valueType == typeof(ushort),
            PlcValueType.Int32  => valueType == typeof(int),
            PlcValueType.UInt32 => valueType == typeof(uint),
            PlcValueType.Float  => valueType == typeof(float),
            PlcValueType.String => valueType == typeof(string),
            _ => false,
        };
        if (!ok) throw new InvalidOperationException(
            $"Item write type mismatch: {item.Key}, Type={item.Type}, Actual={valueType.Name}");
    }

    private PlcItemOpt GetItem(string itemKey)
    {
        lock (_sync)
        {
            if (_items.TryGetValue(itemKey, out var item) || _customs.TryGetValue(itemKey, out item))
                return item;
        }
        throw new KeyNotFoundException("Key를 찾지 못했습니다: " + itemKey);
    }

    private void UpdateSnapshot(PlcItemOpt item, object? value, bool isGood, string? error)
    {
        var snap = new PlcItemSnap(item.Key, item.Address, value, DateTime.UtcNow, isGood, error);
        lock (_sync)
        {
            _snaps[item.Key]       = snap;
            _addrSnaps[item.Address] = snap;
        }
        if (isGood && IsHeartbeat(item.Key))
        {
            if (!_heartbeatInit || !Equals(_lastHeartbeat, value))
            {
                _lastHeartbeat    = value;
                _lastHeartbeatUtc = DateTime.UtcNow;
                _heartbeatInit    = true;
            }
        }
        ItemUpdated?.Invoke(this, snap);
    }

    private void UpdateSnapshots(IReadOnlyList<PlcItemOpt> items, object? value, bool isGood, string? error)
    {
        foreach (var item in items) UpdateSnapshot(item, value, isGood, error);
    }

    private bool IsHeartbeat(string itemKey)
        => !string.IsNullOrWhiteSpace(_hbKey)
        && string.Equals(_hbKey, itemKey, StringComparison.OrdinalIgnoreCase);

    private void Log(PlcLogLevel level, string message, Exception? exception = null)
        => PlcLog.Publish(level, nameof(PlcPollSvc), message, exception);

    private void ThrowIfDisposed()
    {
        if (_disposed) throw new ObjectDisposedException(nameof(PlcPollSvc));
    }

    // ── 내부 데이터 구조 ──────────────────────────────────────────────

    private sealed class CustomReg : IDisposable
    {
        private readonly PlcPollSvc _owner;
        private readonly string[]   _itemKeys;
        private bool _disposed;
        public CustomReg(PlcPollSvc owner, string[] itemKeys) { _owner = owner; _itemKeys = itemKeys; }
        public void Dispose()
        {
            if (_disposed) return;
            _owner.RemoveCustoms(_itemKeys);
            _disposed = true;
        }
    }

    private sealed class WriteRequest
    {
        public WriteRequest(PlcItemOpt item, object value) { Item = item; Value = value; }
        public PlcItemOpt Item  { get; }
        public object     Value { get; }
    }

    private readonly struct AddressToken
    {
        public AddressToken(string prefix, int number) { Prefix = prefix; Number = number; }
        public string Prefix { get; }
        public int    Number { get; }
    }

    private sealed class AddrRead
    {
        public AddrRead(ReadUnit unit, AddressToken token) { Unit = unit; Token = token; }
        public ReadUnit     Unit  { get; }
        public AddressToken Token { get; }
    }

    private sealed class WordAddrRead
    {
        public WordAddrRead(ReadUnit unit, AddressToken token, int wordLength) { Unit = unit; Token = token; WordLength = wordLength; }
        public ReadUnit     Unit       { get; }
        public AddressToken Token      { get; }
        public int          WordLength { get; }
    }

    private sealed class WordWriteReq
    {
        public WordWriteReq(PlcItemOpt item, int sequence, ushort[] words) { Item = item; Sequence = sequence; Words = words; }
        public PlcItemOpt Item     { get; }
        public int        Sequence { get; }
        public ushort[]   Words    { get; }
    }

    private sealed class WordAddrWriteReq
    {
        public WordAddrWriteReq(WordWriteReq request, AddressToken token) { Request = request; Token = token; }
        public WordWriteReq Request { get; }
        public AddressToken Token   { get; }
    }

    private sealed class ReadUnit
    {
        public ReadUnit(PlcItemOpt representative, IReadOnlyList<PlcItemOpt> items) { Representative = representative; Items = items; }
        public PlcItemOpt            Representative { get; }
        public IReadOnlyList<PlcItemOpt> Items      { get; }
    }

    private sealed class ReadStats
    {
        public int BlockReadCount  { get; set; }
        public int RandomReadCount { get; set; }
        public int SingleReadCount { get; set; }
    }

    private sealed class TypedWriteReq<T> where T : unmanaged
    {
        public TypedWriteReq(PlcItemOpt item, T value) { Item = item; Value = value; }
        public PlcItemOpt Item  { get; }
        public T          Value { get; }
    }

    private sealed class TypedAddrWriteReq<T> where T : unmanaged
    {
        public TypedAddrWriteReq(TypedWriteReq<T> request, AddressToken token) { Request = request; Token = token; }
        public TypedWriteReq<T> Request { get; }
        public AddressToken     Token   { get; }
    }
}

// ── PLC 아이템 캐시 스냅샷 ────────────────────────────────────────────

public sealed class PlcItemSnap
{
    public PlcItemSnap(string itemKey, string address, object? value, DateTime timestampUtc, bool isGood, string? error)
    {
        Key          = itemKey;
        Address      = address;
        Value        = value;
        TimestampUtc = timestampUtc;
        IsGood       = isGood;
        Error        = error;
    }

    public string    Key          { get; }
    public string    Address      { get; }
    public object?   Value        { get; }
    public DateTime  TimestampUtc { get; }
    public bool      IsGood       { get; }
    public string?   Error        { get; }
}
