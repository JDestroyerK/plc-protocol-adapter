using System.Net.Sockets;
using PlcLib.Options;

namespace PlcLib.Clients;

/// <summary>
/// Siemens S7 Raw Protocol (ISO-on-TCP, RFC 1006 / COTP / S7 PDU)
/// 포트 102, BYTE transport, Big-Endian. 외부 라이브러리 없음.
/// </summary>
internal sealed class S7RawTransport : IS7Transport
{
    private readonly object _lock = new object();
    private TcpClient? _tcp;
    private NetworkStream? _stream;
    private readonly string _ip;
    private readonly int _port;
    private readonly byte _rack;
    private readonly byte _slot;
    private readonly int _timeoutMs;
    private int _pduSize = 240;
    private byte _pduRef;
    private bool _isConnected;
    private bool _disposed;

    public bool IsConnected => _isConnected;

    public S7RawTransport(S7Opt opt)
    {
        _ip = opt.Ip;
        _port = opt.Port;
        _rack = (byte)opt.Rack;
        _slot = (byte)opt.Slot;
        _timeoutMs = opt.TimeoutMs;
    }

    public bool Connect()
    {
        try
        {
            Disconnect();
            _tcp = new TcpClient { ReceiveTimeout = _timeoutMs, SendTimeout = _timeoutMs };
            _tcp.Connect(_ip, _port);
            _stream = _tcp.GetStream();
            if (!CotpConnect()) return SetConnected(false);
            if (!S7Negotiate()) return SetConnected(false);
            return SetConnected(true);
        }
        catch (Exception ex)
        {
            PlcLog.Publish(PlcLogLevel.Error, nameof(S7RawTransport), $"연결 실패: {_ip}:{_port}", ex);
            return SetConnected(false);
        }
    }

    public void Disconnect()
    {
        try { _stream?.Close(); } catch { }
        try { _tcp?.Close(); } catch { }
        _stream = null; _tcp = null;
        SetConnected(false);
    }

    public bool ReadDB(int db, int startByte, int byteCount, ref byte[] data)
    {
        lock (_lock)
        {
            if (!_isConnected) return false;
            try
            {
                int maxChunk = _pduSize - 40;
                int offset = 0;
                while (offset < byteCount)
                {
                    int chunk = Math.Min(maxChunk, byteCount - offset);
                    if (!ReadChunk(db, startByte + offset, chunk, data, offset)) return false;
                    offset += chunk;
                }
                return true;
            }
            catch (Exception ex) { PlcLog.Publish(PlcLogLevel.Error, nameof(S7RawTransport), "ReadDB 오류", ex); SetConnected(false); return false; }
        }
    }

    public bool WriteDB(int db, int startByte, byte[] data)
    {
        lock (_lock)
        {
            if (!_isConnected) return false;
            try
            {
                int maxChunk = _pduSize - 40;
                int offset = 0;
                while (offset < data.Length)
                {
                    int chunk = Math.Min(maxChunk, data.Length - offset);
                    if (!WriteChunk(db, startByte + offset, data, offset, chunk)) return false;
                    offset += chunk;
                }
                return true;
            }
            catch (Exception ex) { PlcLog.Publish(PlcLogLevel.Error, nameof(S7RawTransport), "WriteDB 오류", ex); SetConnected(false); return false; }
        }
    }

    public bool ReadMultiDB(S7MultiItem[] items)
    {
        lock (_lock)
        {
            if (!_isConnected) return false;
            try
            {
                byte[] req = BuildMultiReadRequest(items);
                _stream!.Write(req, 0, req.Length);
                byte[]? resp = ReceiveTpkt();
                return ParseMultiReadResponse(resp, items);
            }
            catch (Exception ex) { PlcLog.Publish(PlcLogLevel.Error, nameof(S7RawTransport), "ReadMultiDB 오류", ex); SetConnected(false); return false; }
        }
    }

    public bool WriteMultiDB(S7MultiItem[] items)
    {
        lock (_lock)
        {
            if (!_isConnected) return false;
            try
            {
                byte[] req = BuildMultiWriteRequest(items);
                _stream!.Write(req, 0, req.Length);
                byte[]? resp = ReceiveTpkt();
                return ParseMultiWriteResponse(resp, items.Length);
            }
            catch (Exception ex) { PlcLog.Publish(PlcLogLevel.Error, nameof(S7RawTransport), "WriteMultiDB 오류", ex); SetConnected(false); return false; }
        }
    }

    public void Dispose() { if (_disposed) return; _disposed = true; Disconnect(); }

    // ── 핸드셰이크 ─────────────────────────────────────────────────────

    private bool CotpConnect()
    {
        byte dstTsap = (byte)(_rack * 0x20 + _slot);
        byte[] cr = { 0x03, 0x00, 0x00, 0x16, 0x11, 0xE0, 0x00, 0x00, 0x00, 0x01, 0x00,
                       0xC0, 0x01, 0x0A, 0xC1, 0x02, 0x01, 0x00, 0xC2, 0x02, 0x03, dstTsap };
        _stream!.Write(cr, 0, cr.Length);
        byte[]? resp = ReceiveTpkt();
        return resp != null && resp.Length >= 6 && resp[5] == 0xD0;
    }

    private bool S7Negotiate()
    {
        byte[] neg = { 0x03, 0x00, 0x00, 0x19, 0x02, 0xF0, 0x80,
                        0x32, 0x01, 0x00, 0x00, 0x00, 0x00, 0x00, 0x08, 0x00, 0x00,
                        0xF0, 0x00, 0x00, 0x01, 0x00, 0x01, 0x03, 0xC0 };
        _stream!.Write(neg, 0, neg.Length);
        byte[]? resp = ReceiveTpkt();
        if (resp == null || resp.Length < 27 || resp[8] != 0x03) return false;
        _pduSize = (resp[25] << 8) | resp[26];
        PlcLog.Info(nameof(S7RawTransport), $"S7 연결 성공. PDU={_pduSize}");
        return true;
    }

    // ── 단건 청크 ─────────────────────────────────────────────────────

    private bool ReadChunk(int db, int startByte, int byteCount, byte[] dest, int destOffset)
    {
        byte[] req = BuildReadRequest(db, startByte, byteCount);
        _stream!.Write(req, 0, req.Length);
        return ParseReadResponse(ReceiveTpkt(), dest, destOffset, byteCount);
    }

    private bool WriteChunk(int db, int startByte, byte[] src, int srcOffset, int count)
    {
        byte[] req = BuildWriteRequest(db, startByte, src, srcOffset, count);
        _stream!.Write(req, 0, req.Length);
        byte[]? resp = ReceiveTpkt();
        return resp != null && resp.Length >= 22 && resp[21] == 0xFF;
    }

    // ── PDU 빌더 ──────────────────────────────────────────────────────

    private byte[] BuildReadRequest(int db, int startByte, int byteCount)
    {
        const int paramLen = 14, totalLen = 4 + 3 + 10 + paramLen;
        var p = new byte[totalLen]; int o = 0;
        p[o++]=0x03;p[o++]=0x00;p[o++]=(byte)(totalLen>>8);p[o++]=(byte)(totalLen&0xFF);
        p[o++]=0x02;p[o++]=0xF0;p[o++]=0x80;
        p[o++]=0x32;p[o++]=0x01;p[o++]=0x00;p[o++]=0x00;p[o++]=0x00;p[o++]=_pduRef++;
        p[o++]=0x00;p[o++]=(byte)paramLen;p[o++]=0x00;p[o++]=0x00;
        p[o++]=0x04;p[o++]=0x01;
        p[o++]=0x12;p[o++]=0x0A;p[o++]=0x10;p[o++]=0x02;
        p[o++]=(byte)(byteCount>>8);p[o++]=(byte)(byteCount&0xFF);
        p[o++]=(byte)(db>>8);p[o++]=(byte)(db&0xFF);p[o++]=0x84;
        int ba=startByte*8;p[o++]=(byte)(ba>>16);p[o++]=(byte)(ba>>8);p[o++]=(byte)(ba&0xFF);
        return p;
    }

    private byte[] BuildWriteRequest(int db, int startByte, byte[] src, int srcOffset, int count)
    {
        int pad=count%2; const int paramLen=14;
        int dataSecLen=4+count+pad, totalLen=4+3+10+paramLen+dataSecLen;
        var p = new byte[totalLen]; int o = 0;
        p[o++]=0x03;p[o++]=0x00;p[o++]=(byte)(totalLen>>8);p[o++]=(byte)(totalLen&0xFF);
        p[o++]=0x02;p[o++]=0xF0;p[o++]=0x80;
        p[o++]=0x32;p[o++]=0x01;p[o++]=0x00;p[o++]=0x00;p[o++]=0x00;p[o++]=_pduRef++;
        p[o++]=0x00;p[o++]=(byte)paramLen;p[o++]=(byte)(dataSecLen>>8);p[o++]=(byte)(dataSecLen&0xFF);
        p[o++]=0x05;p[o++]=0x01;
        p[o++]=0x12;p[o++]=0x0A;p[o++]=0x10;p[o++]=0x02;
        p[o++]=(byte)(count>>8);p[o++]=(byte)(count&0xFF);
        p[o++]=(byte)(db>>8);p[o++]=(byte)(db&0xFF);p[o++]=0x84;
        int ba=startByte*8;p[o++]=(byte)(ba>>16);p[o++]=(byte)(ba>>8);p[o++]=(byte)(ba&0xFF);
        p[o++]=0x00;p[o++]=0x04;int bl=count*8;p[o++]=(byte)(bl>>8);p[o++]=(byte)(bl&0xFF);
        Array.Copy(src, srcOffset, p, o, count);
        return p;
    }

    private byte[] BuildMultiReadRequest(S7MultiItem[] items)
    {
        int n=items.Length, paramLen=2+12*n, totalLen=4+3+10+paramLen;
        var p = new byte[totalLen]; int o = 0;
        p[o++]=0x03;p[o++]=0x00;p[o++]=(byte)(totalLen>>8);p[o++]=(byte)(totalLen&0xFF);
        p[o++]=0x02;p[o++]=0xF0;p[o++]=0x80;
        p[o++]=0x32;p[o++]=0x01;p[o++]=0x00;p[o++]=0x00;p[o++]=0x00;p[o++]=_pduRef++;
        p[o++]=0x00;p[o++]=(byte)paramLen;p[o++]=0x00;p[o++]=0x00;
        p[o++]=0x04;p[o++]=(byte)n;
        foreach (var item in items) {
            p[o++]=0x12;p[o++]=0x0A;p[o++]=0x10;p[o++]=0x02;
            p[o++]=(byte)(item.ByteCount>>8);p[o++]=(byte)(item.ByteCount&0xFF);
            p[o++]=(byte)(item.DB>>8);p[o++]=(byte)(item.DB&0xFF);p[o++]=0x84;
            int ba=item.StartByte*8;p[o++]=(byte)(ba>>16);p[o++]=(byte)(ba>>8);p[o++]=(byte)(ba&0xFF);
        }
        return p;
    }

    private byte[] BuildMultiWriteRequest(S7MultiItem[] items)
    {
        int n=items.Length, paramLen=2+12*n, dataLen=0;
        foreach (var item in items) dataLen+=4+item.ByteCount+(item.ByteCount%2);
        int totalLen=4+3+10+paramLen+dataLen;
        var p = new byte[totalLen]; int o = 0;
        p[o++]=0x03;p[o++]=0x00;p[o++]=(byte)(totalLen>>8);p[o++]=(byte)(totalLen&0xFF);
        p[o++]=0x02;p[o++]=0xF0;p[o++]=0x80;
        p[o++]=0x32;p[o++]=0x01;p[o++]=0x00;p[o++]=0x00;p[o++]=0x00;p[o++]=_pduRef++;
        p[o++]=0x00;p[o++]=(byte)paramLen;p[o++]=(byte)(dataLen>>8);p[o++]=(byte)(dataLen&0xFF);
        p[o++]=0x05;p[o++]=(byte)n;
        foreach (var item in items) {
            p[o++]=0x12;p[o++]=0x0A;p[o++]=0x10;p[o++]=0x02;
            p[o++]=(byte)(item.ByteCount>>8);p[o++]=(byte)(item.ByteCount&0xFF);
            p[o++]=(byte)(item.DB>>8);p[o++]=(byte)(item.DB&0xFF);p[o++]=0x84;
            int ba=item.StartByte*8;p[o++]=(byte)(ba>>16);p[o++]=(byte)(ba>>8);p[o++]=(byte)(ba&0xFF);
        }
        foreach (var item in items) {
            p[o++]=0x00;p[o++]=0x04;int bl=item.ByteCount*8;
            p[o++]=(byte)(bl>>8);p[o++]=(byte)(bl&0xFF);
            Array.Copy(item.Data, 0, p, o, item.ByteCount);
            o+=item.ByteCount; if(item.ByteCount%2!=0) o++;
        }
        return p;
    }

    // ── 응답 파싱 ─────────────────────────────────────────────────────

    private static bool ParseReadResponse(byte[]? resp, byte[] dest, int destOffset, int expectedBytes)
    {
        if (resp==null||resp.Length<25) return false;
        if (resp[8]!=0x03||resp[17]!=0x00||resp[18]!=0x00||resp[21]!=0xFF) return false;
        int bl=(resp[23]<<8)|resp[24], byteLen=bl/8;
        if (resp.Length<25+byteLen) return false;
        Array.Copy(resp, 25, dest, destOffset, Math.Min(byteLen, expectedBytes));
        return true;
    }

    private static bool ParseMultiReadResponse(byte[]? resp, S7MultiItem[] items)
    {
        if (resp==null||resp.Length<22||resp[8]!=0x03||resp[17]!=0x00||resp[18]!=0x00) return false;
        int pos=21;
        for (int i=0; i<items.Length; i++) {
            if (pos+4>resp.Length||resp[pos]!=0xFF) return false;
            int byteLen=((resp[pos+2]<<8)|resp[pos+3])/8; pos+=4;
            if (pos+byteLen>resp.Length) return false;
            items[i].Data=new byte[items[i].ByteCount];
            Array.Copy(resp, pos, items[i].Data, 0, Math.Min(byteLen, items[i].ByteCount));
            pos+=byteLen; if(byteLen%2!=0) pos++;
        }
        return true;
    }

    private static bool ParseMultiWriteResponse(byte[]? resp, int count)
    {
        if (resp==null||resp.Length<21+count||resp[8]!=0x03||resp[17]!=0x00||resp[18]!=0x00) return false;
        for (int i=0; i<count; i++) if(resp[21+i]!=0xFF) return false;
        return true;
    }

    // ── 소켓 ──────────────────────────────────────────────────────────

    private byte[]? ReceiveTpkt()
    {
        byte[] hdr=new byte[4];
        if (!ReadExact(hdr, 0, 4)||hdr[0]!=0x03) return null;
        int totalLen=(hdr[2]<<8)|hdr[3];
        byte[] pkt=new byte[totalLen];
        hdr.CopyTo(pkt, 0);
        if (!ReadExact(pkt, 4, totalLen-4)) return null;
        return pkt;
    }

    private bool ReadExact(byte[] buf, int offset, int count)
    {
        int received=0;
        while (received<count) {
            int n=_stream!.Read(buf, offset+received, count-received);
            if (n<=0) return false;
            received+=n;
        }
        return true;
    }

    private bool SetConnected(bool state) { _isConnected=state; return state; }
}
