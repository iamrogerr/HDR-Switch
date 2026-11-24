# HDR Switch

A lightweight, portable Windows utility that lives in your system notification tray to quickly turn HDR on or off.

## 🌟 Key Features

- **System Tray Native**: Runs in the background and sits quietly in the notification area.
- **Dynamic Status Icon**: 
  - **On**: Blue badge with a green status indicator.
  - **Off**: Slate gray badge with a gray status indicator.
- **One-Click Toggle**: Left-click the tray icon to switch HDR mode on or off.
- **Quick Menu**: Right-click to access direct toggles (*On*, *Off*), *Settings*, and *Exit*.
- **Automatic Language Detection**: Automatically follows your Windows display language (supports Portuguese and English).
- **Windows Startup Option**: Configure whether the application starts automatically when signing in to Windows (via user registry, no administrator privileges needed).
- **Modern Display API Support**: Compatible with Windows 10 and Windows 11 (including 24H2+ split color management APIs).


## ⚙️ Requirements

- Windows 10 (version 1709 or newer) or Windows 11.
- An HDR-compatible display.


## 🚀 How to Run

Download or build `HDRSwitch.exe` and launch it directly. No installation or third-party dependencies required.


## 🛠️ Building from Source

You can build the project using the native Windows C# compiler (`csc.exe`):

```cmd
build.bat
```

Or manually via Command Prompt:

```cmd
C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe /target:winexe /platform:anycpu /optimize+ /win32manifest:app.manifest /win32icon:app.ico /out:HDRSwitch.exe Program.cs
```


## 🤖 AI Disclosure

This project was developed with assistance from Google Antigravity, an AI-powered development environment.

AI assistance was used during the development process, including code generation, implementation suggestions, debugging, and code refinement.


## 📜 License

This project is licensed under the GNU General Public License v3.0 (GPL-3.0). See the [LICENSE](LICENSE) file for details.


