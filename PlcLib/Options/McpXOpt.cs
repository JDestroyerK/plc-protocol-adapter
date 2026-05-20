namespace PlcLib.Options;

public sealed class McpXOpt
{
    public string Ip           { get; set; } = "127.0.0.1";
    public int    Port         { get; set; } = 5000;
    public string Password     { get; set; } = string.Empty;
    public bool   IsAscii      { get; set; }
    public bool   IsUdp        { get; set; }
    public string RequestFrame { get; set; } = "E3";
    public int    TimeoutMs    { get; set; } = 5000;
}
