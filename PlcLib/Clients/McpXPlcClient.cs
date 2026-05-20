using System.Text.RegularExpressions;
using McpXLib;
using McpXLib.Enums;
using PlcLib.Abstractions;
using PlcLib.Options;

namespace PlcLib.Clients;

/// <summary>
/// 미쯔비시 MC Protocol 3E/E3 (McpX NuGet) 기반 클라이언트입니다.
/// 주소 형식: "D100", "M10", "W1A0" 등 Mitsubishi 디바이스 주소
/// </summary>
public sealed class McpXPlcClient : IPlcClient
{
    private static readonly Regex DevicePattern =
        new Regex(@"^([A-Za-z]+)([0-9A-Fa-f]+)$", RegexOptions.Compiled);

    private static readonly PlcProfile ProviderProfile =
        new PlcProfile(960, 3584, 96, 384);

    private readonly object  _sync = new object();
    private readonly McpX    _client;
    private bool _disposed;

    public McpXPlcClient(string deviceName, McpXOpt opt)
    {
        if (opt == null) throw new ArgumentNullException(nameof(opt));
        if (string.IsNullOrWhiteSpace(opt.Ip))     throw new ArgumentException("McpX.Ip가 비어 있습니다.");
        if (opt.Port <= 0)                          throw new ArgumentOutOfRangeException(nameof(opt), "McpX.Port는 1 이상이어야 합니다.");
        if (opt.TimeoutMs <= 0 || opt.TimeoutMs > ushort.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(opt), "McpX.TimeoutMs는 1~65535 범위여야 합니다.");

        Name = string.IsNullOrWhiteSpace(deviceName) ? "McpX" : deviceName;

        if (!Enum.TryParse(opt.RequestFrame, true, out RequestFrame frame)) frame = RequestFrame.E3;

        _client = new McpX(
            opt.Ip, opt.Port,
            opt.Password ?? string.Empty,
            opt.IsAscii, opt.IsUdp,
            frame, (ushort)opt.TimeoutMs);

        PlcLog.Info(nameof(McpXPlcClient), $"[{Name}] initialized.");
    }

    public string     Name         { get; }
    public string     ProviderName => "McpX";
    public PlcProfile Profile      => ProviderProfile;
    public bool       IsConnected  { get; private set; }

    public void Connect()
    {
        lock (_sync) { ThrowIfDisposed(); IsConnected = true; }
        PlcLog.Info(nameof(McpXPlcClient), $"[{Name}] connected.");
    }

    public void Disconnect()
    {
        lock (_sync) { if (_disposed) return; IsConnected = false; }
        PlcLog.Info(nameof(McpXPlcClient), $"[{Name}] disconnected.");
    }

    public T Read<T>(string device) where T : unmanaged
    {
        var parsed = Parse(device);
        lock (_sync) { ThrowIfDisposed(); EnsureConnected(); return _client.Read<T>(parsed.Prefix, parsed.Addr); }
    }

    public void Write<T>(string device, T value) where T : unmanaged
    {
        var parsed = Parse(device);
        lock (_sync) { ThrowIfDisposed(); EnsureConnected(); _client.Write(parsed.Prefix, parsed.Addr, value); }
    }

    public T[] BlockRead<T>(string startDevice, ushort length) where T : unmanaged
    {
        if (length == 0) return Array.Empty<T>();
        var parsed = Parse(startDevice);
        lock (_sync) { ThrowIfDisposed(); EnsureConnected(); return _client.BatchRead<T>(parsed.Prefix, parsed.Addr, length); }
    }

    public void BlockWrite<T>(string startDevice, IReadOnlyList<T> values) where T : unmanaged
    {
        if (values == null) throw new ArgumentNullException(nameof(values));
        if (values.Count == 0) return;
        var parsed = Parse(startDevice);
        lock (_sync) { ThrowIfDisposed(); EnsureConnected(); _client.BatchWrite(parsed.Prefix, parsed.Addr, values.ToArray()); }
    }

    public IReadOnlyDictionary<string, T> RandomRead<T>(IReadOnlyList<string> devices) where T : unmanaged
    {
        if (devices == null) throw new ArgumentNullException(nameof(devices));
        var result = new Dictionary<string, T>(StringComparer.OrdinalIgnoreCase);
        if (devices.Count == 0) return result;

        var parsed = devices.Select(Parse).ToArray();
        lock (_sync)
        {
            ThrowIfDisposed(); EnsureConnected();
            if (IsWordRandomType<T>())
            {
                var addrs = parsed.Select(p => (p.Prefix, p.Addr)).ToArray();
                var read  = _client.RandomRead<T, int>(addrs, Array.Empty<(Prefix, string)>());
                for (int i = 0; i < devices.Count; i++) result[devices[i]] = read.Item1[i];
            }
            else if (IsDoubleWordRandomType<T>())
            {
                var addrs = parsed.Select(p => (p.Prefix, p.Addr)).ToArray();
                var read  = _client.RandomRead<short, T>(Array.Empty<(Prefix, string)>(), addrs);
                for (int i = 0; i < devices.Count; i++) result[devices[i]] = read.Item2[i];
            }
            else throw new NotSupportedException("McpX RandomRead는 bool/short/ushort/int/uint/float만 지원합니다.");
        }
        return result;
    }

    public void RandomWrite<T>(IReadOnlyDictionary<string, T> valuesByDevice) where T : unmanaged
    {
        if (valuesByDevice == null) throw new ArgumentNullException(nameof(valuesByDevice));
        if (valuesByDevice.Count == 0) return;
        var pairs = valuesByDevice.Select(kv => new KeyValuePair<DeviceAddr, T>(Parse(kv.Key), kv.Value)).ToArray();
        lock (_sync)
        {
            ThrowIfDisposed(); EnsureConnected();
            if (IsWordRandomType<T>())
            {
                var wd = pairs.Select(p => (p.Key.Prefix, p.Key.Addr, p.Value)).ToArray();
                _client.RandomWrite<T, int>(wd, Array.Empty<(Prefix, string, int)>());
            }
            else if (IsDoubleWordRandomType<T>())
            {
                var dw = pairs.Select(p => (p.Key.Prefix, p.Key.Addr, p.Value)).ToArray();
                _client.RandomWrite<short, T>(Array.Empty<(Prefix, string, short)>(), dw);
            }
            else throw new NotSupportedException("McpX RandomWrite는 bool/short/ushort/int/uint/float만 지원합니다.");
        }
    }

    public void Dispose()
    {
        lock (_sync)
        {
            if (_disposed) return;
            _client.Dispose(); IsConnected = false; _disposed = true;
        }
        PlcLog.Info(nameof(McpXPlcClient), $"[{Name}] disposed.");
    }

    // ── 내부 헬퍼 ─────────────────────────────────────────────────────

    private static DeviceAddr Parse(string device)
    {
        if (string.IsNullOrWhiteSpace(device))
            throw new ArgumentException("디바이스 주소가 비어 있습니다.", nameof(device));
        var m = DevicePattern.Match(device.Trim());
        if (!m.Success)
            throw new ArgumentException("지원하지 않는 디바이스 주소 형식입니다: " + device);
        if (!Enum.TryParse<Prefix>(m.Groups[1].Value, true, out var prefix))
            throw new ArgumentException("지원하지 않는 Prefix입니다: " + m.Groups[1].Value);
        return new DeviceAddr(prefix, m.Groups[2].Value);
    }

    private static bool IsWordRandomType<T>()       where T : unmanaged => typeof(T) == typeof(bool) || typeof(T) == typeof(short) || typeof(T) == typeof(ushort);
    private static bool IsDoubleWordRandomType<T>() where T : unmanaged => typeof(T) == typeof(int)  || typeof(T) == typeof(uint)  || typeof(T) == typeof(float);

    private void EnsureConnected()  { if (!IsConnected) throw new InvalidOperationException("PLC가 연결되지 않았습니다."); }
    private void ThrowIfDisposed()  { if (_disposed)    throw new ObjectDisposedException(nameof(McpXPlcClient)); }

    private readonly struct DeviceAddr
    {
        public DeviceAddr(Prefix prefix, string addr) { Prefix = prefix; Addr = addr; }
        public Prefix Prefix { get; }
        public string Addr   { get; }
    }
}
