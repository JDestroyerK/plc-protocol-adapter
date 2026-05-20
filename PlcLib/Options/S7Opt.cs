namespace PlcLib.Options;

public sealed class S7Opt
{
    /// <summary>S71200 | S71500 | S7300 | S7400 | Logo0BA8 등</summary>
    public string CpuType   { get; set; } = "S71200";
    public string Ip        { get; set; } = "192.168.0.1";
    public short  Rack      { get; set; } = 0;
    public short  Slot      { get; set; } = 1;
    public int    TimeoutMs { get; set; } = 5000;
}
