namespace HelloRemoteKM;

public enum AppMode
{
    Controller,
    Receiver
}

public class TrayApp : ApplicationContext
{
    private const int DefaultPort = 9876;

    private readonly NotifyIcon _trayIcon;
    private readonly ToolStripMenuItem _modeControllerItem;
    private readonly ToolStripMenuItem _modeReceiverItem;
    private readonly ToolStripMenuItem _statusItem;
    private readonly ToolStripMenuItem _targetIpItem;
    private readonly SynchronizationContext _syncContext;

    private AppMode _mode = AppMode.Controller;
    private string _targetIp = "192.168.1.100";
    private int _port = DefaultPort;

    private InputHook? _hook;
    private InputSender? _sender;
    private InputReceiver? _receiver;

    public TrayApp()
    {
        _syncContext = SynchronizationContext.Current ?? new SynchronizationContext();

        _modeControllerItem = new ToolStripMenuItem("Controller (send input)", null, OnSetController);
        _modeReceiverItem = new ToolStripMenuItem("Receiver (receive input)", null, OnSetReceiver);
        _statusItem = new ToolStripMenuItem("Status: Ready") { Enabled = false };
        _targetIpItem = new ToolStripMenuItem($"Target IP: {_targetIp}", null, OnSetTargetIp);

        var contextMenu = new ContextMenuStrip();
        contextMenu.Items.Add(_statusItem);
        contextMenu.Items.Add(new ToolStripSeparator());
        contextMenu.Items.Add(_modeControllerItem);
        contextMenu.Items.Add(_modeReceiverItem);
        contextMenu.Items.Add(new ToolStripSeparator());
        contextMenu.Items.Add(_targetIpItem);
        contextMenu.Items.Add(new ToolStripSeparator());
        contextMenu.Items.Add("Exit", null, OnExit);

        _trayIcon = new NotifyIcon
        {
            Icon = SystemIcons.Application,
            Text = "HelloRemoteKM",
            ContextMenuStrip = contextMenu,
            Visible = true
        };

        _trayIcon.DoubleClick += (_, _) => ToggleCapture();

        SetMode(AppMode.Controller);
    }

    private void SetMode(AppMode mode)
    {
        // Cleanup previous mode
        _hook?.Dispose();
        _sender?.Dispose();
        _receiver?.Dispose();
        _hook = null;
        _sender = null;
        _receiver = null;

        _mode = mode;
        _modeControllerItem.Checked = mode == AppMode.Controller;
        _modeReceiverItem.Checked = mode == AppMode.Receiver;
        _targetIpItem.Visible = mode == AppMode.Controller;

        if (mode == AppMode.Controller)
        {
            _hook = new InputHook();
            _sender = new InputSender();
            _sender.SetTarget(_targetIp, _port);

            _hook.MouseMoved += (dx, dy) => _sender.SendMouseMove(dx, dy);
            _hook.MouseButton += (btn, down) => _sender.SendMouseButton(btn, down);
            _hook.MouseWheel += delta => _sender.SendScroll(delta);
            _hook.KeyPressed += (vk, down) => _sender.SendKey(vk, down);
            _hook.CapturingChanged += OnCapturingChanged;

            _hook.Install();
            UpdateStatus("Ready - Press Scroll Lock to control");
        }
        else
        {
            _receiver = new InputReceiver(_port);
            _receiver.ConnectionReceived += ip =>
            {
                _syncContext.Post(_ => UpdateStatus($"Receiving from {ip}"), null);
            };
            _receiver.Start();
            UpdateStatus($"Listening on port {_port}");
        }
    }

    private void OnCapturingChanged(bool isCapturing)
    {
        _syncContext.Post(_ =>
        {
            if (isCapturing)
            {
                UpdateStatus($"ACTIVE - Sending to {_targetIp}");
                _trayIcon.Icon = SystemIcons.Hand;
            }
            else
            {
                UpdateStatus("Ready - Press Scroll Lock to control");
                _trayIcon.Icon = SystemIcons.Application;
            }
        }, null);
    }

    private void ToggleCapture()
    {
        if (_mode == AppMode.Controller && _hook != null)
        {
            _hook.IsCapturing = !_hook.IsCapturing;
        }
    }

    private void UpdateStatus(string status)
    {
        _statusItem.Text = $"Status: {status}";
        // Truncate for tray tooltip (max 64 chars)
        var trayText = $"HelloRemoteKM - {status}";
        _trayIcon.Text = trayText.Length > 63 ? trayText[..63] : trayText;
    }

    private void OnSetController(object? sender, EventArgs e) => SetMode(AppMode.Controller);
    private void OnSetReceiver(object? sender, EventArgs e) => SetMode(AppMode.Receiver);

    private void OnSetTargetIp(object? sender, EventArgs e)
    {
        var input = ShowInputDialog("Enter target IP address:", "Target IP", _targetIp);
        if (!string.IsNullOrWhiteSpace(input))
        {
            _targetIp = input.Trim();
            _targetIpItem.Text = $"Target IP: {_targetIp}";
            _sender?.SetTarget(_targetIp, _port);
        }
    }

    private static string? ShowInputDialog(string prompt, string title, string defaultValue)
    {
        using var form = new Form
        {
            Text = title,
            Width = 300,
            Height = 140,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            StartPosition = FormStartPosition.CenterScreen,
            MaximizeBox = false,
            MinimizeBox = false
        };

        var label = new Label { Left = 10, Top = 10, Width = 260, Text = prompt };
        var textBox = new TextBox { Left = 10, Top = 35, Width = 260, Text = defaultValue };
        var okButton = new Button { Text = "OK", Left = 110, Top = 65, Width = 75, DialogResult = DialogResult.OK };
        var cancelButton = new Button { Text = "Cancel", Left = 195, Top = 65, Width = 75, DialogResult = DialogResult.Cancel };

        form.Controls.AddRange([label, textBox, okButton, cancelButton]);
        form.AcceptButton = okButton;
        form.CancelButton = cancelButton;

        return form.ShowDialog() == DialogResult.OK ? textBox.Text : null;
    }

    private void OnExit(object? sender, EventArgs e)
    {
        _hook?.Dispose();
        _sender?.Dispose();
        _receiver?.Dispose();
        _trayIcon.Visible = false;
        Application.Exit();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _hook?.Dispose();
            _sender?.Dispose();
            _receiver?.Dispose();
            _trayIcon.Dispose();
        }
        base.Dispose(disposing);
    }
}
