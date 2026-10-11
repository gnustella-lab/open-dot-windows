# Open Dot for Windows

[![.NET 8 LTS](https://img.shields.io/badge/.NET-8.0%20LTS-purple.svg)](https://dotnet.microsoft.com/)
[![WinUI 3](https://img.shields.io/badge/UI-WinUI%203%20%2F%20WASDK-blue.svg)](https://learn.microsoft.com/windows/apps/winui/winui3/)
[![Target OS](https://img.shields.io/badge/Platform-Windows%2010%20x64%20(22H2+)%20%7C%20Windows%2011-0078D7.svg)](https://www.microsoft.com/windows)
[![Tests](https://img.shields.io/badge/Tests-15%2F15%20Passing%20(100%25)-brightgreen.svg)]()
[![Architecture](https://img.shields.io/badge/Arch-100%25%20Native%20Desktop-orange.svg)]()
[![Release](https://img.shields.io/badge/Release-v1.0.0-success.svg)](https://github.com/gnustella-lab/open-dot-windows/releases/tag/v1.0.0)

> **Official 100% Native Windows Desktop Port of Open Dot for Windows 10 x64 and Windows 11.**
> Genuine reimplementation in C# 12, .NET 8 LTS, and WinUI 3 with the Windows App SDK.
> **Zero Electron. Zero Tauri. Zero WebView2 UI wrappers. Zero Node.js runtime required in production.**

---

## 🚀 Key Highlights

- **100% Native Graphical Interface (WinUI 3):** Built entirely with native XAML controls (`NavigationView`, `TabView`, `ListView`, `GridView`, `ContentDialog`), hardware acceleration, smooth Fluent animations, and Dark/Light theme switching.
- **Autonomous Multi-Agent Runtime in C#:** Complete agent thread lifecycle (`AgentRuntime`), real-time character-by-character token streaming, pause, cancel, memory persistence, and structured tool calling.
- **Interactive Security Cards:** Native approval cards (`ApprovalCardControl`) for risky actions (Allow / Always Allow / Deny) and question cards (`QuestionCardControl`) for agent queries.
- **Isolated Browser Automation:** Uses `Microsoft.Playwright` for background browser tasks with independent, persistent profiles per Dot under `%LOCALAPPDATA%\OpenDot\dots\<id>\browser`. Includes safe WMI process inspection to manage Dot-specific browser instances without affecting the user's regular browser.
- **Hardware-Backed Security with Windows DPAPI:** API keys (OpenAI, OpenRouter, Composio) and website credentials encrypted with authenticated **AES-256-GCM** sealed by the **Windows Data Protection API (DPAPI)**.
- **Local SQLite Storage with WAL:** High-concurrency local database using `Microsoft.Data.Sqlite` in WAL (Write-Ahead Logging) mode with automatic migrations at `%LOCALAPPDATA%\OpenDot\dots.db`.
- **Scheduled Background Routines:** Autonomous background job execution via cron expressions powered by `Cronos`.
- **Ecosystem & AI Integrations:** Full support for OpenAI (SSE streaming), OpenRouter (hundreds of models), and Composio (Gmail, GitHub, Slack, Notion, etc.).
- **Standalone Distribution & Installer:** Self-contained executable with embedded runtimes. Operates without administrator privileges and requires no pre-installed .NET runtimes from the end user.

---

## 📁 Repository Structure

```text
OpenDot.Windows/
├── src/
│   ├── OpenDot.App/             # WinUI 3 native desktop UI & MVVM ViewModels
│   ├── OpenDot.Core/            # Domain entities, AgentRuntime, ReviewEngine, Scheduler, Interfaces
│   ├── OpenDot.Infrastructure/  # SQLite (WAL), DPAPI + AES-GCM Vault, Windows Notifications, Tray Icon
│   ├── OpenDot.Integrations/    # Async API clients (OpenAI, OpenRouter, Composio)
│   └── OpenDot.Automation/      # Playwright browser manager, Workspaces, Win32 Process inspection
├── tests/
│   └── OpenDot.Tests/           # Automated unit test suite (xUnit, 15/15 passing)
├── packaging/
│   ├── build-package.ps1        # Standalone packaging script generating portable ZIP & installer
│   └── installer.iss            # Inno Setup script for per-user installation without admin rights
├── docs/
│   ├── MIGRATION_AUDIT.md       # Full dependency audit & parity matrix from original repository
│   ├── ARCHITECTURE.md          # Architectural specification and data flow diagrams
│   └── GETTING_STARTED.md       # Developer build, test, and execution guide
├── .github/workflows/
│   └── windows-build.yml        # Automated CI/CD pipeline on GitHub Actions
├── dist/                        # Release output directory
└── OpenDot.Windows.sln          # Visual Studio / .NET 8 solution
```

---

## 🛠️ Build and Run Instructions

### Prerequisites
- Windows 10 Version 22H2 (Build 19045+) or Windows 11.
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) (8.0.400 or later).

### 1. Build the Solution
```powershell
cd OpenDot.Windows
dotnet build OpenDot.Windows.sln -p:WindowsPackageType=None
```

### 2. Run the Unit Test Suite
```powershell
dotnet test tests/OpenDot.Tests/OpenDot.Tests.csproj
```

### 3. Launch the Application
```powershell
dotnet run --project src/OpenDot.App/OpenDot.App.csproj -p:WindowsPackageType=None
```

### 4. Produce Standalone Release Package & Installer
```powershell
powershell -ExecutionPolicy Bypass -File packaging/build-package.ps1
```

The script produces:
- **Installer:** `dist/installer/OpenDot-Windows-Setup-1.0.0.exe`
- **Portable ZIP:** `dist/OpenDot-Windows-v1.0.0-x64.zip`

The executable, main window, and installer use `src/OpenDot.App/Assets/AppIcon.ico`. The icon is embedded in the executable and installer, and copied to `Assets/AppIcon.ico` in the published application for the WinUI window. Keep the `Assets` directory alongside the executable when distributing the portable build.

---

## 📥 Downloads

Ready-to-use binaries are available on the [GitHub Releases page](https://github.com/gnustella-lab/open-dot-windows/releases/tag/v1.0.0):
- **[Windows Setup Installer (.exe)](https://github.com/gnustella-lab/open-dot-windows/releases/download/v1.0.0/OpenDot-Windows-Setup-1.0.0.exe)**
- **[Portable ZIP Package (.zip)](https://github.com/gnustella-lab/open-dot-windows/releases/download/v1.0.0/OpenDot-Windows-v1.0.0-x64.zip)**

---

## 🔒 Security & Privacy

- **Per-User Isolation:** Master keys and credentials are never stored in plaintext; they are protected by the Windows DPAPI subsystem tied to the user's Windows login.
- **Workspace Sandboxing:** Each Dot operates within its own dedicated directory (`%LOCALAPPDATA%\OpenDot\dots\<id>\workspace`). Access to the user's personal file system requires explicit configuration in the Dot's Setup tab and approval via interactive cards.
- **Zero Hidden Telemetry:** All LLM communications occur directly between the user's machine and their configured AI providers.

---

## 📚 Documentation

- [Migration Audit & Dependency Matrix](docs/MIGRATION_AUDIT.md)
- [System Architecture Specification](docs/ARCHITECTURE.md)
- [Getting Started Guide](docs/GETTING_STARTED.md)
