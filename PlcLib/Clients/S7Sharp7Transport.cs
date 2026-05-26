using System.Runtime.InteropServices;
using Sharp7;
using PlcLib.Options;

namespace PlcLib.Clients;

/// <summary>
/// Sharp7 라이브러리 기반 Siemens S7 클라이언트.
/// AnyCPU, 네이티브 DLL 불필요.
/// </summary>
internal sealed class S7Sharp7Transport : IS7Transport
{
    private readonly object _lock = new object();
    private readonly S7Client _client;
    private readonly string _ip;
    private readonly int _rack;
    private readonly int _slot;
    private bool _isConnected;
    private bool _disposed;

    public bool IsConnected => _isConnected;

    public S7Sharp7Transport(S7Opt opt)
    {
        _ip   = opt.Ip;
        _rack = opt.Rack;
        _slot = opt.Slot;
        _client = new S7Client();
        _client.RecvTimeout = opt.TimeoutMs;
        _client.SendTimeout = opt.TimeoutMs;
    }

    public bool Connect()
    {
        try
        {
            Disconnect();
            int ret = _client.ConnectTo(_ip, _rack, _slot);
            if (ret != 0)
            {
                PlcLog.Publish(PlcLogLevel.Error, nameof(S7Sharp7Transport), $"연결 실패: {_client.ErrorText(ret)}");
                return SetConnected(false);
            }
            PlcLog.Info(nameof(S7Sharp7Transport), $"Sharp7 연결 성공. ip={_ip} rack={_rack} slot={_slot}");
            return SetConnected(true);
        }
        catch (Exception ex)
        {
            PlcLog.Publish(PlcLogLevel.Error, nameof(S7Sharp7Transport), $"연결 예외", ex);
            return SetConnected(false);
        }
    }

    public void Disconnect()
    {
        try { _client.Disconnect(); } catch { }
        SetConnected(false);
    }

    public bool ReadDB(int db, int startByte, int byteCount, ref byte[] data)
    {
        lock (_lock)
        {
            if (!_isConnected) return false;
            try
            {
                int ret = _client.ReadArea((int)S7Area.DB, db, startByte, byteCount, (int)S7WordLength.Byte, data);
                if (ret != 0) { PlcLog.Publish(PlcLogLevel.Error, nameof(S7Sharp7Transport), $"ReadDB 오류: {_client.ErrorText(ret)}"); return false; }
                return true;
            }
            catch (Exception ex) { PlcLog.Publish(PlcLogLevel.Error, nameof(S7Sharp7Transport), "ReadDB 예외", ex); SetConnected(false); return false; }
        }
    }

    public bool WriteDB(int db, int startByte, byte[] data)
    {
        lock (_lock)
        {
            if (!_isConnected) return false;
            try
            {
                int ret = _client.WriteArea((int)S7Area.DB, db, startByte, data.Length, (int)S7WordLength.Byte, data);
                if (ret != 0) { PlcLog.Publish(PlcLogLevel.Error, nameof(S7Sharp7Transport), $"WriteDB 오류: {_client.ErrorText(ret)}"); return false; }
                return true;
            }
            catch (Exception ex) { PlcLog.Publish(PlcLogLevel.Error, nameof(S7Sharp7Transport), "WriteDB 예외", ex); SetConnected(false); return false; }
        }
    }

    public bool ReadMultiDB(S7MultiItem[] items)
    {
        lock (_lock)
        {
            if (!_isConnected) return false;
            var handles = new GCHandle[items.Length];
            try
            {
                var s7Items = new S7Client.S7DataItem[items.Length];
                for (int i = 0; i < items.Length; i++)
                {
                    items[i].Data = new byte[items[i].ByteCount];
                    handles[i]    = GCHandle.Alloc(items[i].Data, GCHandleType.Pinned);
                    s7Items[i]    = new S7Client.S7DataItem
                    {
                        Area     = (int)S7Area.DB,
                        WordLen  = (int)S7WordLength.Byte,
                        DBNumber = items[i].DB,
                        Start    = items[i].StartByte,
                        Amount   = items[i].ByteCount,
                        pData    = handles[i].AddrOfPinnedObject(),
                    };
                }
                int ret = _client.ReadMultiVars(s7Items, items.Length);
                if (ret != 0) { PlcLog.Publish(PlcLogLevel.Error, nameof(S7Sharp7Transport), $"ReadMultiDB 오류: {_client.ErrorText(ret)}"); return false; }
                return true;
            }
            catch (Exception ex) { PlcLog.Publish(PlcLogLevel.Error, nameof(S7Sharp7Transport), "ReadMultiDB 예외", ex); SetConnected(false); return false; }
            finally { foreach (var h in handles) if (h.IsAllocated) h.Free(); }
        }
    }

    public bool WriteMultiDB(S7MultiItem[] items)
    {
        lock (_lock)
        {
            if (!_isConnected) return false;
            var handles = new GCHandle[items.Length];
            try
            {
                var s7Items = new S7Client.S7DataItem[items.Length];
                for (int i = 0; i < items.Length; i++)
                {
                    handles[i] = GCHandle.Alloc(items[i].Data, GCHandleType.Pinned);
                    s7Items[i] = new S7Client.S7DataItem
                    {
                        Area     = (int)S7Area.DB,
                        WordLen  = (int)S7WordLength.Byte,
                        DBNumber = items[i].DB,
                        Start    = items[i].StartByte,
                        Amount   = items[i].ByteCount,
                        pData    = handles[i].AddrOfPinnedObject(),
                    };
                }
                int ret = _client.WriteMultiVars(s7Items, items.Length);
                if (ret != 0) { PlcLog.Publish(PlcLogLevel.Error, nameof(S7Sharp7Transport), $"WriteMultiDB 오류: {_client.ErrorText(ret)}"); return false; }
                return true;
            }
            catch (Exception ex) { PlcLog.Publish(PlcLogLevel.Error, nameof(S7Sharp7Transport), "WriteMultiDB 예외", ex); SetConnected(false); return false; }
            finally { foreach (var h in handles) if (h.IsAllocated) h.Free(); }
        }
    }

    public void Dispose() { if (_disposed) return; _disposed = true; Disconnect(); }

    private bool SetConnected(bool state) { _isConnected = state; return state; }
}
