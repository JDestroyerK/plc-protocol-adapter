namespace PlcLib.Options;

public sealed class PlcItemOpt
{
    public string   Category        { get; set; } = "monitor";
    public int      No              { get; set; } = 1;
    public string   Key             { get; set; } = string.Empty;
    public string   Name            { get; set; } = string.Empty;
    public string   Address         { get; set; } = string.Empty;
    public string?  Description     { get; set; }
    public PlcValueType  Type       { get; set; } = PlcValueType.Bool;
    public ushort   StringWordLength { get; set; }
    public PlcByteOrder ByteOrder   { get; set; } = PlcByteOrder.LittleEndian;
    public bool     Writable        { get; set; }
    public double   Scale           { get; set; } = 1.0;
}
