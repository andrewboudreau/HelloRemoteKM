using System.Net;
using System.Net.Sockets;

namespace HelloRemoteKM;

public class InputReceiver : IDisposable
{
    private const byte MSG_MOUSE_MOVE = 0x01;
    private const byte MSG_MOUSE_BUTTON = 0x02;
    private const byte MSG_KEY = 0x03;
    private const byte MSG_SCROLL = 0x04;

    private readonly UdpClient _listener;
    private CancellationTokenSource? _cts;
    private Task? _receiveTask;

    public event Action<string>? ConnectionReceived;

    public InputReceiver(int port)
    {
        _listener = new UdpClient(port);
    }

    public void Start()
    {
        _cts = new CancellationTokenSource();
        _receiveTask = Task.Run(ReceiveLoop);
    }

    public void Stop()
    {
        _cts?.Cancel();
        _listener.Close();
        _receiveTask?.Wait(1000);
    }

    private async Task ReceiveLoop()
    {
        IPEndPoint? lastSender = null;

        while (_cts is { IsCancellationRequested: false })
        {
            try
            {
                var result = await _listener.ReceiveAsync(_cts.Token);
                var data = result.Buffer;

                if (lastSender == null || !lastSender.Equals(result.RemoteEndPoint))
                {
                    lastSender = result.RemoteEndPoint;
                    ConnectionReceived?.Invoke(lastSender.Address.ToString());
                }

                ProcessPacket(data);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (SocketException)
            {
                // Socket closed, exit
                break;
            }
        }
    }

    private void ProcessPacket(byte[] data)
    {
        if (data.Length < 1) return;

        switch (data[0])
        {
            case MSG_MOUSE_MOVE when data.Length >= 5:
                {
                    short dx = BitConverter.ToInt16(data, 1);
                    short dy = BitConverter.ToInt16(data, 3);
                    InputSimulator.MoveMouse(dx, dy);
                }
                break;

            case MSG_MOUSE_BUTTON when data.Length >= 3:
                {
                    var button = (MouseButton)data[1];
                    bool isDown = data[2] != 0;
                    InputSimulator.MouseButtonAction(button, isDown);
                }
                break;

            case MSG_KEY when data.Length >= 3:
                {
                    byte vkCode = data[1];
                    bool isDown = data[2] != 0;
                    InputSimulator.KeyAction(vkCode, isDown);
                }
                break;

            case MSG_SCROLL when data.Length >= 3:
                {
                    short delta = BitConverter.ToInt16(data, 1);
                    InputSimulator.MouseScroll(delta);
                }
                break;
        }
    }

    public void Dispose()
    {
        Stop();
        _listener.Dispose();
        _cts?.Dispose();
        GC.SuppressFinalize(this);
    }
}
