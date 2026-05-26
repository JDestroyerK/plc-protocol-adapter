namespace PlcLib.Clients;

internal sealed class S7MultiItem
{
    public int    DB        { get; set; }
    public int    StartByte { get; set; }
    public int    ByteCount { get; set; }
    public byte[] Data      { get; set; } = Array.Empty<byte>();
}
