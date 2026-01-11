# HelloRemoteKM

A simple keyboard/mouse sharing tool for controlling a remote PC over your local network. Like a software KVM switch.

## Features

- Control a remote PC's keyboard and mouse from your desktop
- Toggle control with Scroll Lock key
- Minimal latency using UDP
- Single executable runs on both machines
- System tray app - stays out of your way

## Requirements

- Windows 10/11
- .NET 10 Runtime (or build self-contained)
- Both machines on the same local network

## Usage

### On the remote PC (laptop you want to control):
1. Run `HelloRemoteKM.exe`
2. Right-click the tray icon → **Receiver (receive input)**

### On your main PC (desktop with keyboard/mouse):
1. Run `HelloRemoteKM.exe`
2. Right-click the tray icon → **Target IP** → enter the remote PC's IP address
3. Press **Scroll Lock** to start sending input to the remote PC
4. Press **Scroll Lock** again to stop and use your desktop normally

When active, your mouse cursor is locked in place and all keyboard/mouse input is sent to the remote machine.

## Building

```bash
# Build
dotnet build

# Run
dotnet run

# Publish single file
dotnet publish -c Release -r win-x64 --self-contained false -o publish
```

## How It Works

- Uses low-level Windows hooks (`SetWindowsHookEx`) to capture keyboard and mouse input
- Sends input events over UDP (port 9876) for minimal latency
- Remote machine uses `SendInput` to simulate the received input
- Mouse cursor is confined using `ClipCursor` when controlling remote

## Protocol

Simple binary UDP packets:

| Type | Format | Size |
|------|--------|------|
| Mouse Move | `[0x01][dx:i16][dy:i16]` | 5 bytes |
| Mouse Button | `[0x02][button:u8][down:u8]` | 3 bytes |
| Key | `[0x03][vkCode:u8][down:u8]` | 3 bytes |
| Scroll | `[0x04][delta:i16]` | 3 bytes |

## License

MIT
