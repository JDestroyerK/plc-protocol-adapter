using System.Globalization;
using ActUtlType64Lib;
using PlcLib.Abstractions;
using PlcLib.Options;

namespace PlcLib.Clients;

/// <summary>
/// 미쯔비시 MX Component(ActUtlType64) COM 기반 클라이언트입니다.
/// MX Component 소프트웨어와 논리 스테이션 번호 설정이 필요합니다.
/// 주소 형식: "D100", "M10" 등 Mitsubishi 디바이스 주소
/// </summary>
public sealed class MxCompPlcClient : IPlcClient
{
    private static readonly PlcProfile ProviderProfile =
        new PlcProfile(960, 3584, 96, 384);

    private readonly object           _sync = new object();
    private readonly ActUtlType64Class _client;
    private bool _disposed;

    public MxCompPlcClient(string deviceName, MxCompOpt opt)
    {
        if (opt == null) throw new ArgumentNullException(nameof(opt));
        if (opt.LogicalStationNo < 0)
            throw new ArgumentOutOfRangeException(nameof(opt), "LogicalStationNo는 0 이상이어야 합니다.");

        Name    = string.IsNullOrWhiteSpace(deviceName) ? "MxComponent" : deviceName;
        _client = new ActUtlType64Class
        {
            ActLogicalStationNumber = opt.LogicalStationNo,
            ActPassword             = opt.Password ?? string.Empty
        };
        PlcLog.Info(nameof(MxCompPlcClient), $"[{Name}] initialized.");
    }

    public string     Name         { get; }
    public string     ProviderName => "MxComponent";
    public PlcProfile Profile      => ProviderProfile;
    public bool       IsConnected  { get; private set; }

    public void Connect()
    {
        lock (_sync) { ThrowIfDisposed(); Check(_client.Open(), "Open"); IsConnected = true; }
        PlcLog.Info(nameof(MxCompPlcClient), $"[{Name}] connected.");
    }

    public void Disconnect()
    {
        lock (_sync)
        {
            if (_disposed) return;
            if (IsConnected) Check(_client.Close(), "Close");
            IsConnected = false;
        }
        PlcLog.Info(nameof(MxCompPlcClient), $"[{Name}] disconnected.");
    }

    public T Read<T>(string device) where T : unmanaged
    {
        ValidateDevice(device);
        lock (_sync) { ThrowIfDisposed(); EnsureConnected(); return ReadCore<T>(device); }
    }

    public void Write<T>(string device, T value) where T : unmanaged
    {
        ValidateDevice(device);
        lock (_sync) { ThrowIfDisposed(); EnsureConnected(); WriteCore(device, value); }
    }

    public T[] BlockRead<T>(string startDevice, ushort length) where T : unmanaged
    {
        if (length == 0) return Array.Empty<T>();
        ValidateDevice(startDevice);
        lock (_sync) { ThrowIfDisposed(); EnsureConnected(); return BlockReadCore<T>(startDevice, length); }
    }

    public void BlockWrite<T>(string startDevice, IReadOnlyList<T> values) where T : unmanaged
    {
        if (values == null) throw new ArgumentNullException(nameof(values));
        if (values.Count == 0) return;
        ValidateDevice(startDevice);
        lock (_sync) { ThrowIfDisposed(); EnsureConnected(); BlockWriteCore(startDevice, values); }
    }

    public IReadOnlyDictionary<string, T> RandomRead<T>(IReadOnlyList<string> devices) where T : unmanaged
    {
        if (devices == null) throw new ArgumentNullException(nameof(devices));
        var result = new Dictionary<string, T>(StringComparer.OrdinalIgnoreCase);
        if (devices.Count == 0) return result;
        lock (_sync)
        {
            ThrowIfDisposed(); EnsureConnected();
            foreach (var d in devices) { ValidateDevice(d); result[d] = ReadCore<T>(d); }
        }
        return result;
    }

    public void RandomWrite<T>(IReadOnlyDictionary<string, T> valuesByDevice) where T : unmanaged
    {
        if (valuesByDevice == null) throw new ArgumentNullException(nameof(valuesByDevice));
        if (valuesByDevice.Count == 0) return;
        lock (_sync)
        {
            ThrowIfDisposed(); EnsureConnected();
            foreach (var pair in valuesByDevice) { ValidateDevice(pair.Key); WriteCore(pair.Key, pair.Value); }
        }
    }

    public void Dispose()
    {
        lock (_sync)
        {
            if (_disposed) return;
            try { if (IsConnected) _client.Close(); }
            catch (Exception ex) { PlcLog.Publish(PlcLogLevel.Warning, nameof(MxCompPlcClient), "Close failed during dispose.", ex); }
            IsConnected = false; _disposed = true;
        }
        PlcLog.Info(nameof(MxCompPlcClient), $"[{Name}] disposed.");
    }

    // ── 읽기/쓰기 코어 ─────────────────────────────────────────────────

    private T ReadCore<T>(string device) where T : unmanaged
    {
        if (typeof(T) == typeof(bool))
        {
            Check(_client.GetDevice(device, out int v), "GetDevice");
            return (T)(object)(v != 0);
        }
        if (typeof(T) == typeof(short) || typeof(T) == typeof(ushort))
        {
            Check(_client.GetDevice2(device, out short v), "GetDevice2");
            return typeof(T) == typeof(short) ? (T)(object)v : (T)(object)(ushort)v;
        }
        if (typeof(T) == typeof(int) || typeof(T) == typeof(uint) || typeof(T) == typeof(float))
        {
            var words = new short[2];
            Check(_client.ReadDeviceBlock2(device, 2, out words[0]), "ReadDeviceBlock2");
            var combined = (uint)((ushort)words[0]) | ((uint)((ushort)words[1]) << 16);
            if (typeof(T) == typeof(int))   return (T)(object)unchecked((int)combined);
            if (typeof(T) == typeof(uint))  return (T)(object)combined;
            return (T)(object)BitConverter.Int32BitsToSingle(unchecked((int)combined));
        }
        throw new NotSupportedException($"MxComponent Read<{typeof(T).Name}>는 지원하지 않습니다.");
    }

    private void WriteCore<T>(string device, T value) where T : unmanaged
    {
        if (typeof(T) == typeof(bool))  { Check(_client.SetDevice(device, (bool)(object)value ? 1 : 0), "SetDevice"); return; }
        if (typeof(T) == typeof(short)) { Check(_client.SetDevice2(device, (short)(object)value), "SetDevice2"); return; }
        if (typeof(T) == typeof(ushort)){ Check(_client.SetDevice2(device, unchecked((short)(ushort)(object)value)), "SetDevice2"); return; }
        if (typeof(T) == typeof(int) || typeof(T) == typeof(uint) || typeof(T) == typeof(float))
        {
            uint bits = typeof(T) == typeof(int)   ? unchecked((uint)(int)(object)value)
                      : typeof(T) == typeof(uint)  ? (uint)(object)value
                      : unchecked((uint)BitConverter.SingleToInt32Bits((float)(object)value));
            var words = new short[] { unchecked((short)(bits & 0xFFFF)), unchecked((short)(bits >> 16)) };
            Check(_client.WriteDeviceBlock2(device, 2, ref words[0]), "WriteDeviceBlock2");
            return;
        }
        throw new NotSupportedException($"MxComponent Write<{typeof(T).Name}>는 지원하지 않습니다.");
    }

    private T[] BlockReadCore<T>(string startDevice, ushort length) where T : unmanaged
    {
        if (typeof(T) == typeof(bool))
        {
            var buf = new int[length];
            Check(_client.ReadDeviceBlock(startDevice, length, out buf[0]), "ReadDeviceBlock");
            return (T[])(object)buf.Select(v => v != 0).ToArray();
        }
        if (typeof(T) == typeof(short) || typeof(T) == typeof(ushort))
        {
            var buf = new short[length];
            Check(_client.ReadDeviceBlock2(startDevice, length, out buf[0]), "ReadDeviceBlock2");
            return typeof(T) == typeof(short)
                ? (T[])(object)buf
                : (T[])(object)buf.Select(v => (ushort)v).ToArray();
        }
        if (typeof(T) == typeof(int) || typeof(T) == typeof(uint) || typeof(T) == typeof(float))
        {
            var buf = new short[length * 2];
            Check(_client.ReadDeviceBlock2(startDevice, buf.Length, out buf[0]), "ReadDeviceBlock2");
            var result = new T[length];
            for (int i = 0; i < length; i++)
            {
                var combined = (uint)((ushort)buf[i * 2]) | ((uint)((ushort)buf[i * 2 + 1]) << 16);
                result[i] = typeof(T) == typeof(int)   ? (T)(object)unchecked((int)combined)
                           : typeof(T) == typeof(uint) ? (T)(object)combined
                           : (T)(object)BitConverter.Int32BitsToSingle(unchecked((int)combined));
            }
            return result;
        }
        throw new NotSupportedException($"MxComponent BlockRead<{typeof(T).Name}>는 지원하지 않습니다.");
    }

    private void BlockWriteCore<T>(string startDevice, IReadOnlyList<T> values) where T : unmanaged
    {
        if (typeof(T) == typeof(bool))
        {
            var buf = values.Select(v => (bool)(object)v ? 1 : 0).ToArray();
            Check(_client.WriteDeviceBlock(startDevice, values.Count, ref buf[0]), "WriteDeviceBlock");
            return;
        }
        if (typeof(T) == typeof(short) || typeof(T) == typeof(ushort))
        {
            var buf = values.Select(v => typeof(T) == typeof(short)
                ? (short)(object)v
                : unchecked((short)(ushort)(object)v)).ToArray();
            Check(_client.WriteDeviceBlock2(startDevice, buf.Length, ref buf[0]), "WriteDeviceBlock2");
            return;
        }
        if (typeof(T) == typeof(int) || typeof(T) == typeof(uint) || typeof(T) == typeof(float))
        {
            var buf = new short[values.Count * 2];
            for (int i = 0; i < values.Count; i++)
            {
                uint bits = typeof(T) == typeof(int)   ? unchecked((uint)(int)(object)values[i])
                           : typeof(T) == typeof(uint) ? (uint)(object)values[i]
                           : unchecked((uint)BitConverter.SingleToInt32Bits((float)(object)values[i]));
                buf[i * 2]     = unchecked((short)(bits & 0xFFFF));
                buf[i * 2 + 1] = unchecked((short)(bits >> 16));
            }
            Check(_client.WriteDeviceBlock2(startDevice, buf.Length, ref buf[0]), "WriteDeviceBlock2");
            return;
        }
        throw new NotSupportedException($"MxComponent BlockWrite<{typeof(T).Name}>는 지원하지 않습니다.");
    }

    // ── 유틸 ──────────────────────────────────────────────────────────

    private static void Check(int code, string op)
    {
        if (code != 0)
            throw new InvalidOperationException(
                $"MxComponent {op} 실패. code=0x{code.ToString("X8", CultureInfo.InvariantCulture)}");
    }

    private void EnsureConnected() { if (!IsConnected) throw new InvalidOperationException("PLC가 연결되지 않았습니다."); }
    private void ThrowIfDisposed() { if (_disposed)    throw new ObjectDisposedException(nameof(MxCompPlcClient)); }

    private static void ValidateDevice(string device)
    {
        if (string.IsNullOrWhiteSpace(device))
            throw new ArgumentException("디바이스 주소가 비어 있습니다.", nameof(device));
    }
}
