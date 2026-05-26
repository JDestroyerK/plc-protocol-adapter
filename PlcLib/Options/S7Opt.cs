namespace PlcLib.Options;

public sealed class S7Opt
{
    /// <summary>Raw | Sharp7</summary>
    public string ComType   { get; set; } = "Raw";
    public string Ip        { get; set; } = "192.168.0.1";
    public int    Port      { get; set; } = 102;
    public int    Rack      { get; set; } = 0;
    public int    Slot      { get; set; } = 1;
    public int    TimeoutMs { get; set; } = 5000;
}
