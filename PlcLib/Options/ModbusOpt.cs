namespace PlcLib.Options;

public sealed class ModbusOpt
{
    /// <summary>Tcp | Rtu</summary>
    public string Mode      { get; set; } = "Tcp";
    public string Ip        { get; set; } = "192.168.0.1";
    public int    Port      { get; set; } = 502;
    public byte   SlaveId   { get; set; } = 1;
    public string PortName  { get; set; } = "COM1";
    public int    BaudRate  { get; set; } = 9600;
    public int    TimeoutMs { get; set; } = 1000;
}
