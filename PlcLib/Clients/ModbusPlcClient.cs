using FluentModbus;
using System.Net;
using PlcLib.Abstractions;
using PlcLib.Options;

namespace PlcLib.Clients;

/// <summary>
/// Modbus TCP/RTU (FluentModbus 5.x) 기반 클라이언트입니다.
///
/// 주소 형식:
///   Holding Register: "HR100" 또는 "R100" (읽기/쓰기)
///   Input Register  : "IR100"             (읽기 전용)
///   Coil            : "C10"               (읽기/쓰기, bool)
///   Discrete Input  : "DI10"              (읽기 전용, bool)
///
/// 지원 타입: bool(Coil/DI), short, ushort, int, uint, float (HR/IR)
/// 32bit 타입은 연속된 레지스터 2개(Little-Endian 워드 순)로 저장됩니다.
/// </summary>
public sealed class ModbusPlcClient : IPlcClient
{
    private static readonly PlcProfile ProviderProfile =
        new PlcProfile(125, 2000, 10, 20);

    private readonly object      _sync = new object();
    private readonly ModbusOpt   _opt;
    private ModbusTcpClient?     _tcp;
    private ModbusRtuClient?     _rtu;
    private bool _rtuConnected;
    private bool _disposed;

    private bool IsTcp => string.Equals(_opt.Mode, "Tcp", StringComparison.OrdinalIgnoreCase);

    public ModbusPlcClient(string deviceName, ModbusOpt opt)
    {
        if (opt == null) throw new ArgumentNullException(nameof(opt));
        Name = string.IsNullOrWhiteSpace(deviceName) ? "Modbus" : deviceName;
        _opt = opt;
        PlcLog.Info(nameof(ModbusPlcClient), $"[{Name}] initialized.");
    }

    public string     Name         { get; }
    public string     ProviderName => "Modbus";
    public PlcProfile Profile      => ProviderProfile;
    public bool       IsConnected  => IsTcp ? (_tcp?.IsConnected ?? false) : _rtuConnected;

    // ── 연결 ──────────────────────────────────────────────────────────

    public void Connect()
    {
        lock (_sync)
        {
            ThrowIfDisposed();
            if (IsTcp)
            {
                _tcp?.Disconnect(); _tcp = null;
                _tcp = new ModbusTcpClient();
                _tcp.Connect(new IPEndPoint(IPAddress.Parse(_opt.Ip), _opt.Port));
            }
            else
            {
                _rtu?.Close(); _rtu = null;
                _rtu = new ModbusRtuClient { BaudRate = _opt.BaudRate };
                _rtu.Connect(_opt.PortName, ModbusEndianness.BigEndian);
                _rtuConnected = true;
            }
        }
        PlcLog.Info(nameof(ModbusPlcClient), $"[{Name}] connected. mode={_opt.Mode}");
    }

    public void Disconnect()
    {
        lock (_sync)
        {
            if (_disposed) return;
            _tcp?.Disconnect(); _tcp = null;
            _rtu?.Close();      _rtu = null;
            _rtuConnected = false;
        }
        PlcLog.Info(nameof(ModbusPlcClient), $"[{Name}] disconnected.");
    }

    // ── 단건 읽기/쓰기 ────────────────────────────────────────────────

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

    // ── 블록 읽기/쓰기 ────────────────────────────────────────────────

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

    // ── 랜덤 읽기/쓰기 ────────────────────────────────────────────────

    public IReadOnlyDictionary<string, T> RandomRead<T>(IReadOnlyList<string> devices) where T : unmanaged
    {
        if (devices == null) throw new ArgumentNullException(nameof(devices));
        var result = new Dictionary<string, T>(StringComparer.OrdinalIgnoreCase);
        if (devices.Count == 0) return result;
        lock (_sync)
        {
            ThrowIfDisposed(); EnsureConnected();
            foreach (var d in devices)
            {
                try { result[d] = ReadCore<T>(d); }
                catch { /* 실패한 주소는 결과에서 누락됨 */ }
            }
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
            foreach (var pair in valuesByDevice)
                WriteCore(pair.Key, pair.Value);
        }
    }

    public void Dispose()
    {
        lock (_sync)
        {
            if (_disposed) return;
            try { _tcp?.Disconnect(); _rtu?.Close(); }
            catch (Exception ex) { PlcLog.Publish(PlcLogLevel.Warning, nameof(ModbusPlcClient), "Close failed during dispose.", ex); }
            _tcp = null; _rtu = null; _rtuConnected = false; _disposed = true;
        }
        PlcLog.Info(nameof(ModbusPlcClient), $"[{Name}] disposed.");
    }

    // ── 읽기 코어 ─────────────────────────────────────────────────────

    private T ReadCore<T>(string device) where T : unmanaged
    {
        var (regType, addr) = ParseAddress(device);
        if (typeof(T) == typeof(bool))
        {
            var raw = regType == 'D' ? ReadDiscreteInputsRaw(addr, 1) : ReadCoilsRaw(addr, 1);
            return (T)(object)ExtractBit(raw, 0);
        }
        if (typeof(T) == typeof(short) || typeof(T) == typeof(ushort))
        {
            var regs = regType == 'I' ? ReadInputRegs(addr, 1) : ReadHoldingRegs(addr, 1);
            return typeof(T) == typeof(short) ? (T)(object)regs[0] : (T)(object)(ushort)regs[0];
        }
        if (typeof(T) == typeof(int) || typeof(T) == typeof(uint) || typeof(T) == typeof(float))
        {
            var regs = regType == 'I' ? ReadInputRegs(addr, 2) : ReadHoldingRegs(addr, 2);
            var bits = (uint)(ushort)regs[0] | ((uint)(ushort)regs[1] << 16);
            if (typeof(T) == typeof(int))  return (T)(object)unchecked((int)bits);
            if (typeof(T) == typeof(uint)) return (T)(object)bits;
            return (T)(object)BitConverter.Int32BitsToSingle(unchecked((int)bits));
        }
        throw new NotSupportedException($"Modbus Read<{typeof(T).Name}>는 지원하지 않습니다.");
    }

    // ── 쓰기 코어 ─────────────────────────────────────────────────────

    private void WriteCore<T>(string device, T value) where T : unmanaged
    {
        var (_, addr) = ParseAddress(device);
        if (typeof(T) == typeof(bool))
        {
            var b = (bool)(object)value;
            if (IsTcp) _tcp!.WriteSingleCoil(_opt.SlaveId, (ushort)addr, b);
            else       _rtu!.WriteSingleCoil(_opt.SlaveId, (ushort)addr, b);
            return;
        }
        if (typeof(T) == typeof(short))
        {
            var v = (short)(object)value;
            if (IsTcp) _tcp!.WriteSingleRegister(_opt.SlaveId, (ushort)addr, v);
            else       _rtu!.WriteSingleRegister(_opt.SlaveId, (ushort)addr, v);
            return;
        }
        if (typeof(T) == typeof(ushort))
        {
            var v = unchecked((short)(ushort)(object)value);
            if (IsTcp) _tcp!.WriteSingleRegister(_opt.SlaveId, (ushort)addr, v);
            else       _rtu!.WriteSingleRegister(_opt.SlaveId, (ushort)addr, v);
            return;
        }
        if (typeof(T) == typeof(int) || typeof(T) == typeof(uint) || typeof(T) == typeof(float))
        {
            var bits = typeof(T) == typeof(int)   ? unchecked((uint)(int)(object)value)
                     : typeof(T) == typeof(uint)  ? (uint)(object)value
                     : unchecked((uint)BitConverter.SingleToInt32Bits((float)(object)value));
            var buf = new short[] { unchecked((short)(bits & 0xFFFF)), unchecked((short)(bits >> 16)) };
            if (IsTcp) _tcp!.WriteMultipleRegisters(_opt.SlaveId, (ushort)addr, buf);
            else       _rtu!.WriteMultipleRegisters(_opt.SlaveId, (ushort)addr, buf);
            return;
        }
        throw new NotSupportedException($"Modbus Write<{typeof(T).Name}>는 지원하지 않습니다.");
    }

    // ── 블록 읽기/쓰기 코어 ───────────────────────────────────────────

    private T[] BlockReadCore<T>(string startDevice, ushort length) where T : unmanaged
    {
        var (regType, addr) = ParseAddress(startDevice);
        if (typeof(T) == typeof(bool))
        {
            var raw = regType == 'D' ? ReadDiscreteInputsRaw(addr, length) : ReadCoilsRaw(addr, length);
            var result = new T[length];
            for (int i = 0; i < length; i++)
                result[i] = (T)(object)ExtractBit(raw, i);
            return result;
        }
        if (typeof(T) == typeof(short) || typeof(T) == typeof(ushort))
        {
            var regs = regType == 'I' ? ReadInputRegs(addr, length) : ReadHoldingRegs(addr, length);
            if (typeof(T) == typeof(short)) return (T[])(object)regs;
            return (T[])(object)regs.Select(v => (ushort)v).ToArray();
        }
        if (typeof(T) == typeof(int) || typeof(T) == typeof(uint) || typeof(T) == typeof(float))
        {
            var regs = regType == 'I' ? ReadInputRegs(addr, length * 2) : ReadHoldingRegs(addr, length * 2);
            var result = new T[length];
            for (int i = 0; i < length; i++)
            {
                var bits = (uint)(ushort)regs[i * 2] | ((uint)(ushort)regs[i * 2 + 1] << 16);
                result[i] = typeof(T) == typeof(int)   ? (T)(object)unchecked((int)bits)
                          : typeof(T) == typeof(uint)  ? (T)(object)bits
                          : (T)(object)BitConverter.Int32BitsToSingle(unchecked((int)bits));
            }
            return result;
        }
        throw new NotSupportedException($"Modbus BlockRead<{typeof(T).Name}>는 지원하지 않습니다.");
    }

    private void BlockWriteCore<T>(string startDevice, IReadOnlyList<T> values) where T : unmanaged
    {
        var (_, addr) = ParseAddress(startDevice);
        if (typeof(T) == typeof(bool))
        {
            var buf = values.Select(v => (bool)(object)v).ToArray();
            if (IsTcp) _tcp!.WriteMultipleCoils(_opt.SlaveId, (ushort)addr, buf);
            else       _rtu!.WriteMultipleCoils(_opt.SlaveId, (ushort)addr, buf);
            return;
        }
        if (typeof(T) == typeof(short) || typeof(T) == typeof(ushort))
        {
            var buf = values.Select(v => typeof(T) == typeof(short)
                ? (short)(object)v
                : unchecked((short)(ushort)(object)v)).ToArray();
            if (IsTcp) _tcp!.WriteMultipleRegisters(_opt.SlaveId, (ushort)addr, buf);
            else       _rtu!.WriteMultipleRegisters(_opt.SlaveId, (ushort)addr, buf);
            return;
        }
        if (typeof(T) == typeof(int) || typeof(T) == typeof(uint) || typeof(T) == typeof(float))
        {
            var buf = new short[values.Count * 2];
            for (int i = 0; i < values.Count; i++)
            {
                var bits = typeof(T) == typeof(int)   ? unchecked((uint)(int)(object)values[i])
                         : typeof(T) == typeof(uint)  ? (uint)(object)values[i]
                         : unchecked((uint)BitConverter.SingleToInt32Bits((float)(object)values[i]));
                buf[i * 2]     = unchecked((short)(bits & 0xFFFF));
                buf[i * 2 + 1] = unchecked((short)(bits >> 16));
            }
            if (IsTcp) _tcp!.WriteMultipleRegisters(_opt.SlaveId, (ushort)addr, buf);
            else       _rtu!.WriteMultipleRegisters(_opt.SlaveId, (ushort)addr, buf);
            return;
        }
        throw new NotSupportedException($"Modbus BlockWrite<{typeof(T).Name}>는 지원하지 않습니다.");
    }

    // ── 로우레벨 헬퍼 ─────────────────────────────────────────────────

    private short[] ReadHoldingRegs(int start, int count)
    {
        var sp = IsTcp
            ? _tcp!.ReadHoldingRegisters<short>(_opt.SlaveId, (ushort)start, (ushort)count)
            : _rtu!.ReadHoldingRegisters<short>(_opt.SlaveId, (ushort)start, (ushort)count);
        return sp.ToArray();
    }

    private short[] ReadInputRegs(int start, int count)
    {
        var sp = IsTcp
            ? _tcp!.ReadInputRegisters<short>(_opt.SlaveId, (ushort)start, (ushort)count)
            : _rtu!.ReadInputRegisters<short>(_opt.SlaveId, (ushort)start, (ushort)count);
        return sp.ToArray();
    }

    private byte[] ReadCoilsRaw(int start, int count)
    {
        var sp = IsTcp
            ? _tcp!.ReadCoils(_opt.SlaveId, (ushort)start, (ushort)count)
            : _rtu!.ReadCoils(_opt.SlaveId, (ushort)start, (ushort)count);
        return sp.ToArray();
    }

    private byte[] ReadDiscreteInputsRaw(int start, int count)
    {
        var sp = IsTcp
            ? _tcp!.ReadDiscreteInputs(_opt.SlaveId, (ushort)start, (ushort)count)
            : _rtu!.ReadDiscreteInputs(_opt.SlaveId, (ushort)start, (ushort)count);
        return sp.ToArray();
    }

    private static bool ExtractBit(byte[] packed, int bitIndex)
        => (packed[bitIndex / 8] & (1 << (bitIndex % 8))) != 0;

    // "HR100"→('R',100), "IR100"→('I',100), "C10"→('C',10), "DI10"→('D',10), "R100"→('R',100)
    private static (char type, int addr) ParseAddress(string device)
    {
        var s = device.Trim().ToUpperInvariant();
        if (s.StartsWith("HR", StringComparison.Ordinal)) return ('R', ParseInt(s.Substring(2)));
        if (s.StartsWith("IR", StringComparison.Ordinal)) return ('I', ParseInt(s.Substring(2)));
        if (s.StartsWith("DI", StringComparison.Ordinal)) return ('D', ParseInt(s.Substring(2)));
        if (s.Length > 0 && s[0] == 'C')                 return ('C', ParseInt(s.Substring(1)));
        if (s.Length > 0 && s[0] == 'R')                 return ('R', ParseInt(s.Substring(1)));
        throw new ArgumentException("지원하지 않는 Modbus 주소 형식입니다: " + device);
    }

    private static int ParseInt(string s) => int.TryParse(s, out int n) ? n : 0;

    private void EnsureConnected() { if (!IsConnected) throw new InvalidOperationException("Modbus PLC가 연결되지 않았습니다."); }
    private void ThrowIfDisposed() { if (_disposed)    throw new ObjectDisposedException(nameof(ModbusPlcClient)); }
    private static void ValidateDevice(string d) { if (string.IsNullOrWhiteSpace(d)) throw new ArgumentException("디바이스 주소가 비어 있습니다."); }
}
