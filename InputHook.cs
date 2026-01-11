using System.Diagnostics;
using System.Runtime.InteropServices;
using static HelloRemoteKM.NativeMethods;

namespace HelloRemoteKM;

public enum MouseButton : byte
{
    Left = 0,
    Right = 1,
    Middle = 2,
    X1 = 3,
    X2 = 4
}

public class InputHook : IDisposable
{
    private IntPtr _keyboardHook;
    private IntPtr _mouseHook;
    private readonly LowLevelHookProc _keyboardProc;
    private readonly LowLevelHookProc _mouseProc;
    private bool _isCapturing;
    private POINT _lastMousePos;
    private POINT _lockPoint;

    public bool IsCapturing
    {
        get => _isCapturing;
        set
        {
            if (_isCapturing == value) return;
            _isCapturing = value;

            if (_isCapturing)
            {
                // Lock mouse to current position
                GetCursorPos(out _lockPoint);
                var rect = new RECT
                {
                    Left = _lockPoint.X,
                    Top = _lockPoint.Y,
                    Right = _lockPoint.X + 1,
                    Bottom = _lockPoint.Y + 1
                };
                ClipCursor(ref rect);
                _lastMousePos = _lockPoint;
            }
            else
            {
                // Release mouse
                ClipCursor(IntPtr.Zero);
            }

            CapturingChanged?.Invoke(_isCapturing);
        }
    }

    public event Action<int, int>? MouseMoved;           // dx, dy
    public event Action<MouseButton, bool>? MouseButton; // button, isDown
    public event Action<int>? MouseWheel;                // delta
    public event Action<byte, bool>? KeyPressed;         // vkCode, isDown
    public event Action<bool>? CapturingChanged;         // isCapturing

    public InputHook()
    {
        _keyboardProc = KeyboardHookCallback;
        _mouseProc = MouseHookCallback;
    }

    public void Install()
    {
        using var curProcess = Process.GetCurrentProcess();
        using var curModule = curProcess.MainModule!;
        var moduleHandle = GetModuleHandle(curModule.ModuleName);

        _keyboardHook = SetWindowsHookEx(WH_KEYBOARD_LL, _keyboardProc, moduleHandle, 0);
        _mouseHook = SetWindowsHookEx(WH_MOUSE_LL, _mouseProc, moduleHandle, 0);

        if (_keyboardHook == IntPtr.Zero || _mouseHook == IntPtr.Zero)
        {
            throw new InvalidOperationException("Failed to install input hooks");
        }
    }

    public void Uninstall()
    {
        if (_keyboardHook != IntPtr.Zero)
        {
            UnhookWindowsHookEx(_keyboardHook);
            _keyboardHook = IntPtr.Zero;
        }
        if (_mouseHook != IntPtr.Zero)
        {
            UnhookWindowsHookEx(_mouseHook);
            _mouseHook = IntPtr.Zero;
        }

        // Make sure to release cursor
        ClipCursor(IntPtr.Zero);
    }

    private IntPtr KeyboardHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0)
        {
            var kb = Marshal.PtrToStructure<KBDLLHOOKSTRUCT>(lParam);
            int msg = wParam.ToInt32();
            bool isDown = msg == WM_KEYDOWN || msg == WM_SYSKEYDOWN;

            // Toggle on Scroll Lock key up
            if (kb.vkCode == VK_SCROLL && !isDown)
            {
                IsCapturing = !IsCapturing;
                return (IntPtr)1; // Eat the key
            }

            if (_isCapturing)
            {
                KeyPressed?.Invoke((byte)kb.vkCode, isDown);
                return (IntPtr)1; // Eat the key
            }
        }
        return CallNextHookEx(_keyboardHook, nCode, wParam, lParam);
    }

    private IntPtr MouseHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0 && _isCapturing)
        {
            var ms = Marshal.PtrToStructure<MSLLHOOKSTRUCT>(lParam);
            int msg = wParam.ToInt32();

            switch (msg)
            {
                case WM_MOUSEMOVE:
                    int dx = ms.pt.X - _lastMousePos.X;
                    int dy = ms.pt.Y - _lastMousePos.Y;
                    _lastMousePos = ms.pt;
                    if (dx != 0 || dy != 0)
                    {
                        MouseMoved?.Invoke(dx, dy);
                    }
                    break;

                case WM_LBUTTONDOWN:
                    MouseButton?.Invoke(HelloRemoteKM.MouseButton.Left, true);
                    break;
                case WM_LBUTTONUP:
                    MouseButton?.Invoke(HelloRemoteKM.MouseButton.Left, false);
                    break;

                case WM_RBUTTONDOWN:
                    MouseButton?.Invoke(HelloRemoteKM.MouseButton.Right, true);
                    break;
                case WM_RBUTTONUP:
                    MouseButton?.Invoke(HelloRemoteKM.MouseButton.Right, false);
                    break;

                case WM_MBUTTONDOWN:
                    MouseButton?.Invoke(HelloRemoteKM.MouseButton.Middle, true);
                    break;
                case WM_MBUTTONUP:
                    MouseButton?.Invoke(HelloRemoteKM.MouseButton.Middle, false);
                    break;

                case WM_XBUTTONDOWN:
                    {
                        var xButton = (ms.mouseData >> 16) == 1
                            ? HelloRemoteKM.MouseButton.X1
                            : HelloRemoteKM.MouseButton.X2;
                        MouseButton?.Invoke(xButton, true);
                    }
                    break;
                case WM_XBUTTONUP:
                    {
                        var xButton = (ms.mouseData >> 16) == 1
                            ? HelloRemoteKM.MouseButton.X1
                            : HelloRemoteKM.MouseButton.X2;
                        MouseButton?.Invoke(xButton, false);
                    }
                    break;

                case WM_MOUSEWHEEL:
                    int delta = (short)(ms.mouseData >> 16);
                    MouseWheel?.Invoke(delta);
                    break;
            }

            return (IntPtr)1; // Eat all mouse input when capturing
        }
        return CallNextHookEx(_mouseHook, nCode, wParam, lParam);
    }

    public void Dispose()
    {
        Uninstall();
        GC.SuppressFinalize(this);
    }
}
