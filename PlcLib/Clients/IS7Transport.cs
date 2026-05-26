namespace PlcLib.Clients;

internal interface IS7Transport : IDisposable
{
    bool IsConnected { get; }
    bool Connect();
    void Disconnect();
    bool ReadDB(int db, int startByte, int byteCount, ref byte[] data);
    bool WriteDB(int db, int startByte, byte[] data);
    bool ReadMultiDB(S7MultiItem[] items);
    bool WriteMultiDB(S7MultiItem[] items);
}
