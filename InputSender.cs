using System.Net;
using System.Net.Sockets;

namespace HelloRemoteKM;

// Packet format:
// Mouse Move:    [0x01][dx:int16][dy:int16]      = 5 bytes
// Mouse Button:  [0x02][button:byte][down:byte]  = 3 bytes
// Key:           [0x03][vkCode:byte][down:byte]  = 3 bytes
// Scroll:        [0x04][delta:int16]             = 3 bytes

public class InputSender : IDisposable
{
    private const byte MSG_MOUSE_MOVE = 0x01;
    private const byte MSG_MOUSE_BUTTON = 0x02;
    private const byte MSG_KEY = 0x03;
    private const byte MSG_SCROLL = 0x04;

    private readonly UdpClient _client;
    private IPEndPoint? _target;

    public InputSender()
    {
        _client = new UdpClient();
    }

    public void SetTarget(string ip, int port)
    {
        _target = new IPEndPoint(IPAddress.Parse(ip), port);
    }

    public void SendMouseMove(int dx, int dy)
    {
        if (_target == null) return;

        Span<byte> buffer = stackalloc byte[5];
        buffer[0] = MSG_MOUSE_MOVE;
        BitConverter.TryWriteBytes(buffer.Slice(1, 2), (short)dx);
        BitConverter.TryWriteBytes(buffer.Slice(3, 2), (short)dy);

        _client.Send(buffer.ToArray(), buffer.Length, _target);
    }

    public void SendMouseButton(MouseButton button, bool isDown)
    {
        if (_target == null) return;

        byte[] buffer = [MSG_MOUSE_BUTTON, (byte)button, (byte)(isDown ? 1 : 0)];
        _client.Send(buffer, buffer.Length, _target);
    }

    public void SendKey(byte vkCode, bool isDown)
    {
        if (_target == null) return;

        byte[] buffer = [MSG_KEY, vkCode, (byte)(isDown ? 1 : 0)];
        _client.Send(buffer, buffer.Length, _target);
    }

    public void SendScroll(int delta)
    {
        if (_target == null) return;

        Span<byte> buffer = stackalloc byte[3];
        buffer[0] = MSG_SCROLL;
        BitConverter.TryWriteBytes(buffer.Slice(1, 2), (short)delta);

        _client.Send(buffer.ToArray(), buffer.Length, _target);
    }

    public void Dispose()
    {
        _client.Dispose();
        GC.SuppressFinalize(this);
    }
}
