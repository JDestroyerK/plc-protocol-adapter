namespace PlcLib.Common;

public sealed class ConnArgs : EventArgs
{
    public ConnArgs(bool isConnected) => IsConnected = isConnected;
    public bool IsConnected { get; }
}
