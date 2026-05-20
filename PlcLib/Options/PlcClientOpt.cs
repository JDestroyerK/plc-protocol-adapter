namespace PlcLib.Options;

public sealed class PlcClientOpt
{
    public int    No      { get; set; } = 1;
    public string Name    { get; set; } = "PLC";
    public bool   Enabled { get; set; } = true;

    /// <summary>McpX | MxComponent | S7 | Modbus | Virtual</summary>
    public string  Provider { get; set; } = "McpX";

    /// <summary>리플렉션으로 커스텀 IPlcClient 구현체를 지정합니다. 설정 시 Provider보다 우선합니다.</summary>
    public string? ImplType { get; set; }

    public PlcPollOpt Poll { get; set; } = new PlcPollOpt();

    public McpXOpt?   McpX        { get; set; }
    public MxCompOpt? MxComponent { get; set; }
    public S7Opt?     S7          { get; set; }
    public ModbusOpt? Modbus      { get; set; }
}
