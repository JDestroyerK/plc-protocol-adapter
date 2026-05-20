namespace PlcLib.Abstractions;

public interface IPlcClient : IDisposable
{
    string ProviderName { get; }
    string Name         { get; }
    PlcProfile Profile  { get; }
    bool IsConnected    { get; }

    void Connect();
    void Disconnect();

    T    Read<T>(string device)              where T : unmanaged;
    void Write<T>(string device, T value)    where T : unmanaged;

    T[]  BlockRead<T>(string startDevice, ushort length)              where T : unmanaged;
    void BlockWrite<T>(string startDevice, IReadOnlyList<T> values)   where T : unmanaged;

    IReadOnlyDictionary<string, T> RandomRead<T>(IReadOnlyList<string> devices)               where T : unmanaged;
    void RandomWrite<T>(IReadOnlyDictionary<string, T> valuesByDevice)                        where T : unmanaged;
}
