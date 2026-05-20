using System.Runtime.InteropServices;
using PlcLib.Abstractions;

namespace PlcLib.Clients;

/// <summary>
/// 실제 PLC 없이 메모리로 동작하는 가상 클라이언트입니다. 테스트/시뮬레이션용입니다.
/// </summary>
public sealed class VirtualPlcClient : IPlcClient
{
    private static readonly PlcProfile ProviderProfile =
        new PlcProfile(4096, 8192, 64, 256);

    private readonly object _sync = new object();
    private readonly Dictionary<string, ushort> _mem =
        new Dictionary<string, ushort>(StringComparer.OrdinalIgnoreCase);
    private bool _disposed;

    public VirtualPlcClient(string deviceName, string providerName = "Virtual")
    {
        Name         = string.IsNullOrWhiteSpace(deviceName) ? "VirtualDevice" : deviceName;
        ProviderName = string.IsNullOrWhiteSpace(providerName) ? "Virtual" : providerName;
        PlcLog.Info(nameof(VirtualPlcClient), $"[{Name}] initialized.");
    }

    public string     Name         { get; }
    public string     ProviderName { get; }
    public PlcProfile Profile      => ProviderProfile;
    public bool       IsConnected  { get; private set; }

    public void Connect()
    {
        lock (_sync) { ThrowIfDisposed(); IsConnected = true; }
        PlcLog.Info(nameof(VirtualPlcClient), $"[{Name}] connected.");
    }

    public void Disconnect()
    {
        lock (_sync) { if (_disposed) return; IsConnected = false; }
        PlcLog.Info(nameof(VirtualPlcClient), $"[{Name}] disconnected.");
    }

    public T Read<T>(string device) where T : unmanaged
    {
        ValidateDevice(device);
        lock (_sync)
        {
            ThrowIfDisposed(); EnsureConnected();
            var wc = WordCount<T>();
            if (wc == 1)
            {
                _mem.TryGetValue(device, out var w);
                return FromWord<T>(w);
            }
            var (prefix, num) = ParseNumeric(device);
            var words = new ushort[wc];
            for (int i = 0; i < wc; i++) _mem.TryGetValue(prefix + (num + i), out words[i]);
            return FromWords<T>(words);
        }
    }

    public void Write<T>(string device, T value) where T : unmanaged
    {
        ValidateDevice(device);
        lock (_sync)
        {
            ThrowIfDisposed(); EnsureConnected();
            var wc = WordCount<T>();
            if (wc == 1) { _mem[device] = ToWord(value); return; }
            var (prefix, num) = ParseNumeric(device);
            var words = ToWords(value);
            for (int i = 0; i < words.Length; i++) _mem[prefix + (num + i)] = words[i];
        }
    }

    public T[] BlockRead<T>(string startDevice, ushort length) where T : unmanaged
    {
        ValidateDevice(startDevice);
        if (length == 0) return Array.Empty<T>();
        var result   = new T[length];
        var (prefix, startNo) = ParseNumeric(startDevice);
        var wc = WordCount<T>();
        lock (_sync)
        {
            ThrowIfDisposed(); EnsureConnected();
            for (int i = 0; i < length; i++)
            {
                var addr = startNo + i * wc;
                if (wc == 1)
                {
                    _mem.TryGetValue(prefix + addr, out var w);
                    result[i] = FromWord<T>(w);
                }
                else
                {
                    var words = new ushort[wc];
                    for (int j = 0; j < wc; j++) _mem.TryGetValue(prefix + (addr + j), out words[j]);
                    result[i] = FromWords<T>(words);
                }
            }
        }
        return result;
    }

    public void BlockWrite<T>(string startDevice, IReadOnlyList<T> values) where T : unmanaged
    {
        if (values == null) throw new ArgumentNullException(nameof(values));
        ValidateDevice(startDevice);
        if (values.Count == 0) return;
        var (prefix, startNo) = ParseNumeric(startDevice);
        var wc = WordCount<T>();
        lock (_sync)
        {
            ThrowIfDisposed(); EnsureConnected();
            for (int i = 0; i < values.Count; i++)
            {
                var addr = startNo + i * wc;
                if (wc == 1) { _mem[prefix + addr] = ToWord(values[i]); }
                else
                {
                    var words = ToWords(values[i]);
                    for (int j = 0; j < words.Length; j++) _mem[prefix + (addr + j)] = words[j];
                }
            }
        }
    }

    public IReadOnlyDictionary<string, T> RandomRead<T>(IReadOnlyList<string> devices) where T : unmanaged
    {
        if (devices == null) throw new ArgumentNullException(nameof(devices));
        var result = new Dictionary<string, T>(StringComparer.OrdinalIgnoreCase);
        if (devices.Count == 0) return result;
        lock (_sync)
        {
            ThrowIfDisposed(); EnsureConnected();
            foreach (var d in devices) { ValidateDevice(d); result[d] = Read<T>(d); }
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
            foreach (var pair in valuesByDevice) { ValidateDevice(pair.Key); Write(pair.Key, pair.Value); }
        }
    }

    public void Dispose()
    {
        lock (_sync)
        {
            if (_disposed) return;
            _mem.Clear(); IsConnected = false; _disposed = true;
        }
        PlcLog.Info(nameof(VirtualPlcClient), $"[{Name}] disposed.");
    }

    // ── 내부 헬퍼 ─────────────────────────────────────────────────────

    private static int WordCount<T>() where T : unmanaged
        => (Marshal.SizeOf<T>() + 1) / 2;

    private static ushort ToWord<T>(T v) where T : unmanaged
    {
        var b = new byte[Marshal.SizeOf<T>()];
        MemoryMarshal.Write(b, ref v);
        return BitConverter.ToUInt16(b, 0);
    }

    private static ushort[] ToWords<T>(T v) where T : unmanaged
    {
        var size  = Marshal.SizeOf<T>();
        var bytes = new byte[size];
        MemoryMarshal.Write(bytes, ref v);
        var words = new ushort[(size + 1) / 2];
        for (int i = 0; i < words.Length; i++) words[i] = BitConverter.ToUInt16(bytes, i * 2);
        return words;
    }

    private static T FromWord<T>(ushort word) where T : unmanaged
    {
        var bytes  = BitConverter.GetBytes(word);
        var padded = new byte[Marshal.SizeOf<T>()];
        Array.Copy(bytes, padded, Math.Min(bytes.Length, padded.Length));
        return MemoryMarshal.Read<T>(padded);
    }

    private static T FromWords<T>(ushort[] words) where T : unmanaged
    {
        var size  = Marshal.SizeOf<T>();
        var bytes = new byte[size];
        for (int i = 0; i < words.Length && i * 2 < size; i++)
        {
            var wb = BitConverter.GetBytes(words[i]);
            Array.Copy(wb, 0, bytes, i * 2, Math.Min(2, size - i * 2));
        }
        return MemoryMarshal.Read<T>(bytes);
    }

    private static (string Prefix, int Number) ParseNumeric(string device)
    {
        var t = device.Trim();
        int pLen = 0;
        while (pLen < t.Length && char.IsLetter(t[pLen])) pLen++;
        if (pLen == 0 || pLen == t.Length)
            throw new ArgumentException("블럭 접근은 Prefix+숫자 형식이 필요합니다: " + device);
        if (!int.TryParse(t[pLen..], out var num) || num < 0)
            throw new ArgumentException("숫자 주소가 유효하지 않습니다: " + device);
        return (t[..pLen], num);
    }

    private void EnsureConnected()
    {
        if (!IsConnected) throw new InvalidOperationException("Virtual PLC가 연결되지 않았습니다.");
    }

    private void ThrowIfDisposed()
    {
        if (_disposed) throw new ObjectDisposedException(nameof(VirtualPlcClient));
    }

    private static void ValidateDevice(string device)
    {
        if (string.IsNullOrWhiteSpace(device))
            throw new ArgumentException("디바이스 주소가 비어 있습니다.", nameof(device));
    }
}
