using System.Text;
using System.Text.RegularExpressions;
using PlcLib.Abstractions;
using PlcLib.Options;
using S7Net    = S7.Net;
using S7Item   = S7.Net.Types.DataItem;

namespace PlcLib.Clients;

/// <summary>
/// 지멘스 S7 프로토콜(S7netplus) 기반 클라이언트입니다.
///
/// 지원 주소 형식:
///   Word(16bit) : "DB1.DBW10", "MW10", "IW0", "QW0"
///   DWord(32bit): "DB1.DBD4",  "MD4"
///   Bool(bit)   : "DB1.DBX0.0", "M0.0", "I0.1", "Q0.0"
///   Byte/String : "DB1.DBB10"
///
/// PlcPollSvc 연동 시:
///   - DB/M 주소(DBW, DBD)는 RandomRead(ReadMultipleVars, 최대 19개/배치)로 처리됩니다.
///   - Bool 주소는 개별 단건 읽기로 처리됩니다.
///   - S7 특성상 블록 최적화(BlockRead)는 MW/IW/QW 형식의 단순 주소에만 적용됩니다.
/// </summary>
public sealed class S7PlcClient : IPlcClient
{
    private const int BatchSize = 19; // S7 PDU 한계

    // S7-1200 기준 보수적 프로파일
    private static readonly PlcProfile ProviderProfile =
        new PlcProfile(100, 100, 5, 10);

    private readonly object    _sync = new object();
    private readonly S7Opt     _opt;
    private S7Net.Plc?         _plc;
    private bool _disposed;

    public S7PlcClient(string deviceName, S7Opt opt)
    {
        if (opt == null) throw new ArgumentNullException(nameof(opt));
        Name = string.IsNullOrWhiteSpace(deviceName) ? "S7" : deviceName;
        _opt = opt;
        PlcLog.Info(nameof(S7PlcClient), $"[{Name}] initialized.");
    }

    public string     Name         { get; }
    public string     ProviderName => "S7";
    public PlcProfile Profile      => ProviderProfile;
    public bool       IsConnected  => _plc?.IsConnected ?? false;

    // ── 연결 ──────────────────────────────────────────────────────────

    public void Connect()
    {
        lock (_sync)
        {
            ThrowIfDisposed();
            _plc?.Close();
            if (!Enum.TryParse(_opt.CpuType, out S7Net.CpuType cpu)) cpu = S7Net.CpuType.S71200;
            _plc = new S7Net.Plc(cpu, _opt.Ip, _opt.Rack, _opt.Slot);
            _plc.Open();
        }
        PlcLog.Info(nameof(S7PlcClient), $"[{Name}] connected. cpu={_opt.CpuType} ip={_opt.Ip}");
    }

    public void Disconnect()
    {
        lock (_sync) { if (_disposed) return; _plc?.Close(); _plc = null; }
        PlcLog.Info(nameof(S7PlcClient), $"[{Name}] disconnected.");
    }

    // ── 단건 읽기 ─────────────────────────────────────────────────────

    public T Read<T>(string device) where T : unmanaged
    {
        ValidateDevice(device);
        lock (_sync)
        {
            ThrowIfDisposed(); EnsureConnected();
            return ReadSingle<T>(device);
        }
    }

    // ── 단건 쓰기 ─────────────────────────────────────────────────────

    public void Write<T>(string device, T value) where T : unmanaged
    {
        ValidateDevice(device);
        lock (_sync)
        {
            ThrowIfDisposed(); EnsureConnected();
            WriteSingle(device, value);
        }
    }

    // ── 블록 읽기 (연속 주소) ─────────────────────────────────────────

    public T[] BlockRead<T>(string startDevice, ushort length) where T : unmanaged
    {
        if (length == 0) return Array.Empty<T>();
        ValidateDevice(startDevice);
        lock (_sync)
        {
            ThrowIfDisposed(); EnsureConnected();

            if (typeof(T) == typeof(bool))
                return BlockReadBool(startDevice, length) as T[] ?? Array.Empty<T>();

            var (dt, db, byteOffset) = ParseBlockAddress(startDevice);
            var elemBytes = ElemSize<T>();
            var bytes     = _plc!.ReadBytes(dt, db, byteOffset, length * elemBytes);
            var result    = new T[length];
            for (int i = 0; i < length; i++)
                result[i] = BytesToValue<T>(bytes, i * elemBytes);
            return result;
        }
    }

    // ── 블록 쓰기 ─────────────────────────────────────────────────────

    public void BlockWrite<T>(string startDevice, IReadOnlyList<T> values) where T : unmanaged
    {
        if (values == null) throw new ArgumentNullException(nameof(values));
        if (values.Count == 0) return;
        ValidateDevice(startDevice);
        lock (_sync)
        {
            ThrowIfDisposed(); EnsureConnected();
            if (typeof(T) == typeof(bool)) { BlockWriteBool(startDevice, values); return; }
            var (dt, db, byteOffset) = ParseBlockAddress(startDevice);
            var elemBytes = ElemSize<T>();
            var bytes     = new byte[values.Count * elemBytes];
            for (int i = 0; i < values.Count; i++)
                ValueToBytes(values[i], bytes, i * elemBytes);
            _plc!.WriteBytes(dt, db, byteOffset, bytes);
        }
    }

    // ── 랜덤 읽기 (ReadMultipleVars, 최대 19개/배치) ──────────────────

    public IReadOnlyDictionary<string, T> RandomRead<T>(IReadOnlyList<string> devices) where T : unmanaged
    {
        if (devices == null) throw new ArgumentNullException(nameof(devices));
        var result = new Dictionary<string, T>(StringComparer.OrdinalIgnoreCase);
        if (devices.Count == 0) return result;

        lock (_sync)
        {
            ThrowIfDisposed(); EnsureConnected();

            var batchable  = new List<(string addr, S7Item item)>();
            var fallback   = new List<string>();

            foreach (var d in devices)
            {
                var item = TryBuildDataItem(d, typeof(T));
                if (item != null) batchable.Add((d, item));
                else              fallback.Add(d);
            }

            // 배치 읽기
            for (int i = 0; i < batchable.Count; i += BatchSize)
            {
                var chunk = batchable.Skip(i).Take(BatchSize).ToList();
                var items = chunk.Select(c => c.item).ToList();
                try
                {
                    _plc!.ReadMultipleVars(items);
                    for (int j = 0; j < chunk.Count; j++)
                        result[chunk[j].addr] = ExtractValue<T>(items[j]);
                }
                catch { foreach (var (addr, _) in chunk) fallback.Add(addr); }
            }

            // 폴백 단건 읽기
            foreach (var d in fallback)
            {
                try { result[d] = ReadSingle<T>(d); }
                catch { /* 실패한 주소는 결과에서 누락됨 */ }
            }
        }
        return result;
    }

    // ── 랜덤 쓰기 ─────────────────────────────────────────────────────

    public void RandomWrite<T>(IReadOnlyDictionary<string, T> valuesByDevice) where T : unmanaged
    {
        if (valuesByDevice == null) throw new ArgumentNullException(nameof(valuesByDevice));
        if (valuesByDevice.Count == 0) return;
        lock (_sync)
        {
            ThrowIfDisposed(); EnsureConnected();
            foreach (var pair in valuesByDevice)
                WriteSingle(pair.Key, pair.Value);
        }
    }

    public void Dispose()
    {
        lock (_sync)
        {
            if (_disposed) return;
            _plc?.Close(); _plc = null; _disposed = true;
        }
        PlcLog.Info(nameof(S7PlcClient), $"[{Name}] disposed.");
    }

    // ── 내부: 단건 R/W ────────────────────────────────────────────────

    private T ReadSingle<T>(string device) where T : unmanaged
    {
        var raw = _plc!.Read(device);
        if (raw == null) return default;
        return ConvertRaw<T>(raw);
    }

    private void WriteSingle<T>(string device, T value) where T : unmanaged
    {
        if (typeof(T) == typeof(bool))  { _plc!.Write(device, (bool)(object)value);              return; }
        if (typeof(T) == typeof(float)) { _plc!.Write(device, (float)(object)value);             return; }
        if (typeof(T) == typeof(int))   { _plc!.Write(device, (int)(object)value);               return; }
        if (typeof(T) == typeof(uint))  { _plc!.Write(device, unchecked((int)(uint)(object)value)); return; }
        if (typeof(T) == typeof(short)) { _plc!.Write(device, (short)(object)value);             return; }
        if (typeof(T) == typeof(ushort)){ _plc!.Write(device, unchecked((short)(ushort)(object)value)); return; }
        throw new NotSupportedException($"S7 Write<{typeof(T).Name}>는 지원하지 않습니다.");
    }

    // ── 내부: Bool BlockRead/Write ────────────────────────────────────

    private bool[] BlockReadBool(string startDevice, ushort length)
    {
        var result = new bool[length];
        for (int i = 0; i < length; i++)
            result[i] = Convert.ToBoolean(_plc!.Read(IncrementBitAddr(startDevice, i)));
        return result;
    }

    private void BlockWriteBool<T>(string startDevice, IReadOnlyList<T> values) where T : unmanaged
    {
        for (int i = 0; i < values.Count; i++)
            _plc!.Write(IncrementBitAddr(startDevice, i), (bool)(object)values[i]);
    }

    // ── 내부: 주소 파싱 ───────────────────────────────────────────────

    // "DB1.DBW10" → (DataBlock, db=1, byteOffset=10)
    // "MW10"      → (Memory, db=0, byteOffset=10)
    private static (S7Net.DataType dt, int db, int byteOffset) ParseBlockAddress(string device)
    {
        var s = device.Trim().ToUpperInvariant();

        var dbm = Regex.Match(s, @"^DB(\d+)\.DB([BWD])(\d+)$");
        if (dbm.Success)
            return (S7Net.DataType.DataBlock, int.Parse(dbm.Groups[1].Value), int.Parse(dbm.Groups[3].Value));

        var mm = Regex.Match(s, @"^M([WD])(\d+)$");
        if (mm.Success) return (S7Net.DataType.Memory, 0, int.Parse(mm.Groups[2].Value));

        var im = Regex.Match(s, @"^I([WD])(\d+)$");
        if (im.Success) return (S7Net.DataType.Input,  0, int.Parse(im.Groups[2].Value));

        var qm = Regex.Match(s, @"^Q([WD])(\d+)$");
        if (qm.Success) return (S7Net.DataType.Output, 0, int.Parse(qm.Groups[2].Value));

        throw new ArgumentException("BlockRead/Write를 지원하지 않는 S7 주소 형식입니다: " + device);
    }

    // S7 배치 읽기용 DataItem 생성. Bit 타입 및 지원 불가 주소는 null 반환
    private static S7Item? TryBuildDataItem(string device, Type valueType)
    {
        var s = device.Trim().ToUpperInvariant();

        var dbm = Regex.Match(s, @"^DB(\d+)\.DB([BWDX])(\d+)(?:\.(\d+))?$");
        if (dbm.Success)
        {
            var kind = dbm.Groups[2].Value;
            if (kind == "X") return null; // Bit → 개별 폴백

            return new S7Item
            {
                DataType     = S7Net.DataType.DataBlock,
                DB           = int.Parse(dbm.Groups[1].Value),
                StartByteAdr = int.Parse(dbm.Groups[3].Value),
                VarType      = kind == "D" ? S7Net.VarType.DWord : S7Net.VarType.Word,
                Count        = 1,
            };
        }

        var mm = Regex.Match(s, @"^M([WD])(\d+)$");
        if (mm.Success)
            return new S7Item
            {
                DataType     = S7Net.DataType.Memory,
                StartByteAdr = int.Parse(mm.Groups[2].Value),
                VarType      = mm.Groups[1].Value == "D" ? S7Net.VarType.DWord : S7Net.VarType.Word,
                Count        = 1,
            };

        return null; // 비트, 입출력 등 → 개별 폴백
    }

    // ── 내부: 타입 변환 ───────────────────────────────────────────────

    // S7Net.Plc.Read() 반환값(object) → T
    private static T ConvertRaw<T>(object raw) where T : unmanaged
    {
        if (typeof(T) == typeof(bool))   return (T)(object)Convert.ToBoolean(raw);
        if (typeof(T) == typeof(float))
        {
            var bits = Convert.ToUInt32(raw);
            return (T)(object)BitConverter.Int32BitsToSingle(unchecked((int)bits));
        }
        if (typeof(T) == typeof(int))    return (T)(object)unchecked((int)Convert.ToUInt32(raw));
        if (typeof(T) == typeof(uint))   return (T)(object)Convert.ToUInt32(raw);
        if (typeof(T) == typeof(short))  return (T)(object)unchecked((short)Convert.ToUInt16(raw));
        if (typeof(T) == typeof(ushort)) return (T)(object)Convert.ToUInt16(raw);
        throw new NotSupportedException($"S7 Read<{typeof(T).Name}>는 지원하지 않습니다.");
    }

    // DataItem.Value → T (ReadMultipleVars 후)
    private static T ExtractValue<T>(S7Item item) where T : unmanaged
    {
        var val = item.Value;
        if (val == null) return default;
        if (typeof(T) == typeof(float))
        {
            var bits = Convert.ToUInt32(val);
            return (T)(object)BitConverter.Int32BitsToSingle(unchecked((int)bits));
        }
        return ConvertRaw<T>(val);
    }

    // S7 Big-Endian 바이트 배열 → T
    private static T BytesToValue<T>(byte[] bytes, int offset) where T : unmanaged
    {
        if (typeof(T) == typeof(short))
        {
            var v = (ushort)((bytes[offset] << 8) | bytes[offset + 1]);
            return (T)(object)unchecked((short)v);
        }
        if (typeof(T) == typeof(ushort))
            return (T)(object)(ushort)((bytes[offset] << 8) | bytes[offset + 1]);
        if (typeof(T) == typeof(int) || typeof(T) == typeof(uint) || typeof(T) == typeof(float))
        {
            var bits = (uint)((bytes[offset] << 24) | (bytes[offset + 1] << 16)
                             | (bytes[offset + 2] << 8) | bytes[offset + 3]);
            if (typeof(T) == typeof(int))   return (T)(object)unchecked((int)bits);
            if (typeof(T) == typeof(uint))  return (T)(object)bits;
            return (T)(object)BitConverter.Int32BitsToSingle(unchecked((int)bits));
        }
        throw new NotSupportedException($"S7 BlockRead<{typeof(T).Name}>는 지원하지 않습니다.");
    }

    // T → S7 Big-Endian 바이트 배열
    private static void ValueToBytes<T>(T value, byte[] buffer, int offset) where T : unmanaged
    {
        if (typeof(T) == typeof(short) || typeof(T) == typeof(ushort))
        {
            var v = typeof(T) == typeof(short)
                ? unchecked((ushort)(short)(object)value)
                : (ushort)(object)value;
            buffer[offset]     = (byte)(v >> 8);
            buffer[offset + 1] = (byte)(v & 0xFF);
            return;
        }
        if (typeof(T) == typeof(int) || typeof(T) == typeof(uint) || typeof(T) == typeof(float))
        {
            var bits = typeof(T) == typeof(int)   ? unchecked((uint)(int)(object)value)
                     : typeof(T) == typeof(uint)  ? (uint)(object)value
                     : unchecked((uint)BitConverter.SingleToInt32Bits((float)(object)value));
            buffer[offset]     = (byte)(bits >> 24);
            buffer[offset + 1] = (byte)(bits >> 16 & 0xFF);
            buffer[offset + 2] = (byte)(bits >> 8  & 0xFF);
            buffer[offset + 3] = (byte)(bits & 0xFF);
            return;
        }
        throw new NotSupportedException($"S7 BlockWrite<{typeof(T).Name}>는 지원하지 않습니다.");
    }

    private static int ElemSize<T>() where T : unmanaged
    {
        if (typeof(T) == typeof(short) || typeof(T) == typeof(ushort)) return 2;
        if (typeof(T) == typeof(int)   || typeof(T) == typeof(uint) || typeof(T) == typeof(float)) return 4;
        return 2;
    }

    // Bit 주소 증가: "DB1.DBX10.3" + 5 → "DB1.DBX10.8" → "DB1.DBX11.0"
    private static string IncrementBitAddr(string baseAddr, int offset)
    {
        var s = baseAddr.Trim();

        var dbm = Regex.Match(s, @"^(DB\d+\.DBX)(\d+)\.(\d+)$", RegexOptions.IgnoreCase);
        if (dbm.Success)
        {
            var byteNo = int.Parse(dbm.Groups[2].Value);
            var bitNo  = int.Parse(dbm.Groups[3].Value) + offset;
            return $"{dbm.Groups[1].Value}{byteNo + bitNo / 8}.{bitNo % 8}";
        }

        var mm = Regex.Match(s, @"^([MIQQ])(\d+)\.(\d+)$", RegexOptions.IgnoreCase);
        if (mm.Success)
        {
            var byteNo = int.Parse(mm.Groups[2].Value);
            var bitNo  = int.Parse(mm.Groups[3].Value) + offset;
            return $"{mm.Groups[1].Value}{byteNo + bitNo / 8}.{bitNo % 8}";
        }

        throw new ArgumentException("S7 Bit 주소 증가 불가: " + baseAddr);
    }

    private void EnsureConnected() { if (!IsConnected) throw new InvalidOperationException("S7 PLC가 연결되지 않았습니다."); }
    private void ThrowIfDisposed() { if (_disposed)    throw new ObjectDisposedException(nameof(S7PlcClient)); }
    private static void ValidateDevice(string d) { if (string.IsNullOrWhiteSpace(d)) throw new ArgumentException("디바이스 주소가 비어 있습니다."); }
}
