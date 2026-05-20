namespace PlcLib.Options;

public sealed class PlcPollOpt
{
    public bool    Enabled  { get; set; } = true;
    public int     PollMs   { get; set; } = 100;
    public int     ReconnMs { get; set; } = 1000;
    public string? HbKey    { get; set; }
    public int     HbMs     { get; set; } = 5000;
}
