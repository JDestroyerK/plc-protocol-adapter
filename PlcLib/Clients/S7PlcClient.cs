using System.Text.RegularExpressions;
using PlcLib.Abstractions;
using PlcLib.Options;

namespace PlcLib.Clients;

/// <summary>
/// Siemens S7 클라이언트. S7Opt.ComType에 따라 Raw(직접 구현) 또는 Sharp7 라이브러리를 선택합니다.
///
/// 지원 주소 형식 (DB 영역만):
///   Bool  : "DB1.DBX0.0"
///   Word  : "DB1.DBW10"
///   DWord : "DB1.DBD4"
///   Byte  : "DB1.DBB5"
/// </summary>
public sealed class S7PlcClient : IPlcClient
{
    private const int BatchSize = 19; // S7 PDU 한계

    private static readonly PlcProfile ProviderProfile = new PlcProfile(100, 100, 5, 10);

    private readonly object _sync = new object();
    private readonly IS7Transport _transport;
    private bool _disposed;

    public S7PlcClient(string deviceName, S7Opt opt)
    {
        if (opt == null) throw new ArgumentNullException(nameof(opt));
        Name = string.IsNullOrWhiteSpace(deviceName) ? "S7" : deviceName;
        _transport = opt.ComType.Equals("Sharp7", StringComparison.OrdinalIgnoreCase)
            ? new S7Sharp7Transport(opt)
            : (IS7Transport)new S7RawTransport(opt);
        PlcLog.Info(nameof(S7PlcClient), $"[{Name}] initialized. comType={opt.ComType}");
    }

    public string     Name         { get; }
    public string     ProviderName => "S7";
    public PlcProfile Profile      => ProviderProfile;
    public bool       IsConnected  => _transport.IsConnected;

    // ── 연결 ──────────────────────────────────────────────────────────

    public void Connect()
    {
        lock (_sync) ThrowIfDisposed();
        if (!_transport.Connect())
            throw new InvalidOperationException($"[{Name}] S7 PLC 연결 실패.");
        PlcLog.Info(nameof(S7PlcClient), $"[{Name}] connected.");
    }

    public void Disconnect()
    {
        _transport.Disconnect();
        PlcLog.Info(nameof(S7PlcClient), $"[{Name}] disconnected.");
    }

    // ── 단건 읽기 ─────────────────────────────────────────────────────

    public T Read<T>(string device) where T : unmanaged
    {
        ValidateDevice(device);
        lock (_sync) { ThrowIfDisposed(); EnsureConnected(); return ReadSingle<T>(device); }
    }

    // ── 단건 쓰기 ─────────────────────────────────────────────────────

    public void Write<T>(string device, T value) where T : unmanaged
    {
        ValidateDevice(device);
        lock (_sync) { ThrowIfDisposed(); EnsureConnected(); WriteSingle(device, value); }
    }

    // ── 블록 읽기 ─────────────────────────────────────────────────────

    public T[] BlockRead<T>(string startDevice, ushort length) where T : unmanaged
    {
        if (length == 0) return Array.Empty<T>();
        ValidateDevice(startDevice);
        lock (_sync)
        {
            ThrowIfDisposed(); EnsureConnected();
            ParseAddress(startDevice, out int db, out int byteOff, out int bitIdx);

            if (typeof(T) == typeof(bool))
            {
                int bytesNeeded = (bitIdx + length + 7) / 8;
                byte[] raw = new byte[bytesNeeded];
                if (!_transport.ReadDB(db, byteOff, bytesNeeded, ref raw))
                    throw new InvalidOperationException($"BlockRead 실패: {startDevice}");
                var result = new T[length];
                for (int i = 0; i < length; i++)
                {
                    int abs = bitIdx + i;
                    result[i] = (T)(object)((raw[abs / 8] & (1 << (abs % 8))) != 0);
                }
                return result;
            }
            else
            {
                int elemSize = ElemSize<T>();
                byte[] raw = new byte[length * elemSize];
                if (!_transport.ReadDB(db, byteOff, raw.Length, ref raw))
                    throw new InvalidOperationException($"BlockRead 실패: {startDevice}");
                var result = new T[length];
                for (int i = 0; i < length; i++)
                    result[i] = BytesToValue<T>(raw, i * elemSize);
                return result;
            }
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
            ParseAddress(startDevice, out int db, out int byteOff, out int bitIdx);

            if (typeof(T) == typeof(bool))
            {
                int bytesNeeded = (bitIdx + values.Count + 7) / 8;
                byte[] raw = new byte[bytesNeeded];
                _transport.ReadDB(db, byteOff, bytesNeeded, ref raw);
                for (int i = 0; i < values.Count; i++)
                {
                    int abs = bitIdx + i;
                    if ((bool)(object)values[i]) raw[abs / 8] |=  (byte)(1 << (abs % 8));
                    else                         raw[abs / 8] &= (byte)~(1 << (abs % 8));
                }
                if (!_transport.WriteDB(db, byteOff, raw))
                    throw new InvalidOperationException($"BlockWrite 실패: {startDevice}");
            }
            else
            {
                int elemSize = ElemSize<T>();
                byte[] raw = new byte[values.Count * elemSize];
                for (int i = 0; i < values.Count; i++)
                    ValueToBytes(values[i], raw, i * elemSize);
                if (!_transport.WriteDB(db, byteOff, raw))
                    throw new InvalidOperationException($"BlockWrite 실패: {startDevice}");
            }
        }
    }

    // ── 랜덤 읽기 (ReadMultiDB 배치) ─────────────────────────────────

    public IReadOnlyDictionary<string, T> RandomRead<T>(IReadOnlyList<string> devices) where T : unmanaged
    {
        if (devices == null) throw new ArgumentNullException(nameof(devices));
        var result = new Dictionary<string, T>(StringComparer.OrdinalIgnoreCase);
        if (devices.Count == 0) return result;

        lock (_sync)
        {
            ThrowIfDisposed(); EnsureConnected();

            for (int start = 0; start < devices.Count; start += BatchSize)
            {
                int end = Math.Min(start + BatchSize, devices.Count);
                int chunkLen = end - start;

                var items   = new S7MultiItem[chunkLen];
                var bitIdxs = new int[chunkLen];

                for (int j = 0; j < chunkLen; j++)
                {
                    ParseAddress(devices[start + j], out int db, out int byteOff, out int bitIdx);
                    bitIdxs[j] = bitIdx;
                    items[j]   = new S7MultiItem
                    {
                        DB        = db,
                        StartByte = byteOff,
                        ByteCount = (typeof(T) == typeof(bool)) ? 1 : ElemSize<T>(),
                    };
                }

                bool ok = _transport.ReadMultiDB(items);

                for (int j = 0; j < chunkLen; j++)
                {
                    string addr = devices[start + j];
                    if (!ok || items[j].Data == null || items[j].Data.Length == 0)
                    {
                        try { result[addr] = ReadSingle<T>(addr); } catch { }
                        continue;
                    }
                    result[addr] = (typeof(T) == typeof(bool))
                        ? (T)(object)((items[j].Data[0] & (1 << bitIdxs[j])) != 0)
                        : BytesToValue<T>(items[j].Data, 0);
                }
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
            _transport.Dispose();
            _disposed = true;
        }
        PlcLog.Info(nameof(S7PlcClient), $"[{Name}] disposed.");
    }

    // ── 내부: 단건 R/W ────────────────────────────────────────────────

    private T ReadSingle<T>(string device) where T : unmanaged
    {
        ParseAddress(device, out int db, out int byteOff, out int bitIdx);

        if (typeof(T) == typeof(bool))
        {
            byte[] raw = new byte[1];
            if (!_transport.ReadDB(db, byteOff, 1, ref raw))
                throw new InvalidOperationException($"ReadDB 실패: {device}");
            return (T)(object)((raw[0] & (1 << bitIdx)) != 0);
        }

        int byteCount = ElemSize<T>();
        byte[] data = new byte[byteCount];
        if (!_transport.ReadDB(db, byteOff, byteCount, ref data))
            throw new InvalidOperationException($"ReadDB 실패: {device}");
        return BytesToValue<T>(data, 0);
    }

    private void WriteSingle<T>(string device, T value) where T : unmanaged
    {
        ParseAddress(device, out int db, out int byteOff, out int bitIdx);

        if (typeof(T) == typeof(bool))
        {
            byte[] raw = new byte[1];
            _transport.ReadDB(db, byteOff, 1, ref raw);
            if ((bool)(object)value) raw[0] |=  (byte)(1 << bitIdx);
            else                     raw[0] &= (byte)~(1 << bitIdx);
            if (!_transport.WriteDB(db, byteOff, raw))
                throw new InvalidOperationException($"WriteDB 실패: {device}");
            return;
        }

        int byteCount = ElemSize<T>();
        byte[] data = new byte[byteCount];
        ValueToBytes(value, data, 0);
        if (!_transport.WriteDB(db, byteOff, data))
            throw new InvalidOperationException($"WriteDB 실패: {device}");
    }

    // ── 내부: 주소 파싱 ───────────────────────────────────────────────

    // "DB1.DBX0.0" → db=1, byteOff=0, bitIdx=0
    // "DB1.DBW10"  → db=1, byteOff=10, bitIdx=0
    // "DB1.DBD4"   → db=1, byteOff=4,  bitIdx=0
    // "DB1.DBB5"   → db=1, byteOff=5,  bitIdx=0
    private static void ParseAddress(string device, out int db, out int byteOff, out int bitIdx)
    {
        var s = device.Trim().ToUpperInvariant();

        var mBit = Regex.Match(s, @"^DB(\d+)\.DBX(\d+)\.(\d+)$");
        if (mBit.Success)
        {
            db      = int.Parse(mBit.Groups[1].Value);
            byteOff = int.Parse(mBit.Groups[2].Value);
            bitIdx  = int.Parse(mBit.Groups[3].Value);
            return;
        }

        var mOther = Regex.Match(s, @"^DB(\d+)\.DB[BWD](\d+)$");
        if (mOther.Success)
        {
            db      = int.Parse(mOther.Groups[1].Value);
            byteOff = int.Parse(mOther.Groups[2].Value);
            bitIdx  = 0;
            return;
        }

        throw new ArgumentException(
            $"지원하지 않는 S7 주소 형식입니다: '{device}'. " +
            "지원 형식: DB{n}.DBX{byte}.{bit}, DB{n}.DBW{byte}, DB{n}.DBD{byte}, DB{n}.DBB{byte}");
    }

    // ── 내부: 타입 변환 (Siemens Big-Endian) ─────────────────────────

    private static T BytesToValue<T>(byte[] bytes, int offset) where T : unmanaged
    {
        if (typeof(T) == typeof(bool))   return (T)(object)(bytes[offset] != 0);
        if (typeof(T) == typeof(byte))   return (T)(object)bytes[offset];
        if (typeof(T) == typeof(short))
            return (T)(object)unchecked((short)((bytes[offset] << 8) | bytes[offset + 1]));
        if (typeof(T) == typeof(ushort))
            return (T)(object)(ushort)((bytes[offset] << 8) | bytes[offset + 1]);
        if (typeof(T) == typeof(int) || typeof(T) == typeof(uint) || typeof(T) == typeof(float))
        {
            var bits = (uint)((bytes[offset] << 24) | (bytes[offset + 1] << 16)
                             | (bytes[offset + 2] << 8) | bytes[offset + 3]);
            if (typeof(T) == typeof(int))  return (T)(object)unchecked((int)bits);
            if (typeof(T) == typeof(uint)) return (T)(object)bits;
            return (T)(object)BitConverter.Int32BitsToSingle(unchecked((int)bits));
        }
        throw new NotSupportedException($"S7 Read<{typeof(T).Name}>는 지원하지 않습니다.");
    }

    private static void ValueToBytes<T>(T value, byte[] buffer, int offset) where T : unmanaged
    {
        if (typeof(T) == typeof(byte))
            { buffer[offset] = (byte)(object)value; return; }
        if (typeof(T) == typeof(short) || typeof(T) == typeof(ushort))
        {
            var v = typeof(T) == typeof(short)
                ? unchecked((ushort)(short)(object)value)
                : (ushort)(object)value;
            buffer[offset] = (byte)(v >> 8); buffer[offset + 1] = (byte)(v & 0xFF);
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
        throw new NotSupportedException($"S7 Write<{typeof(T).Name}>는 지원하지 않습니다.");
    }

    private static int ElemSize<T>() where T : unmanaged
    {
        if (typeof(T) == typeof(bool) || typeof(T) == typeof(byte)) return 1;
        if (typeof(T) == typeof(short) || typeof(T) == typeof(ushort)) return 2;
        if (typeof(T) == typeof(int) || typeof(T) == typeof(uint) || typeof(T) == typeof(float)) return 4;
        return 2;
    }

    private void EnsureConnected() { if (!IsConnected) throw new InvalidOperationException("S7 PLC가 연결되지 않았습니다."); }
    private void ThrowIfDisposed() { if (_disposed) throw new ObjectDisposedException(nameof(S7PlcClient)); }
    private static void ValidateDevice(string d) { if (string.IsNullOrWhiteSpace(d)) throw new ArgumentException("디바이스 주소가 비어 있습니다."); }
}
