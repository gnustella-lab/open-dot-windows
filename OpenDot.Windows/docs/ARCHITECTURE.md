# Open Dot for Windows — System Architecture

## 1. System Overview

**Open Dot for Windows** is a 100% native implementation of the Open Dot autonomous AI assistant system for **Windows 10 x64 (Version 22H2 / Build 19045+)** and **Windows 11**.

Unlike web-wrapped solutions (Electron, Tauri, or WebView2 wrappers), the application is built from scratch in **C# 12**, **.NET 8 LTS**, and **WinUI 3 with the Windows App SDK**. It completely removes any runtime dependency on Node.js, pnpm, and local web servers, delivering an authentic desktop software experience with minimal memory footprint, instant startup, and hardware-accelerated rendering.

```
┌────────────────────────────────────────────────────────────────────────┐
│                   OpenDot.App (WinUI 3 / XAML)                         │
│  - MainWindow, Navigation, Mica/Acrylic Backdrops                      │
│  - Home, Dot Workspace, Channels, Integrations, Settings               │
│  - MVVM via CommunityToolkit.Mvvm & Microsoft.Extensions.DI             │
└─────────────────────────────────┬──────────────────────────────────────┘
                                  │
┌─────────────────────────────────▼──────────────────────────────────────┐
│                   OpenDot.Core (Class Library net8.0)                  │
│  - Models (Dot, Look, Message, CardData, Channel, Memory, Skill, Rule) │
│  - AgentRuntime (Streaming, Thread Loop, Pausability, Tool Engine)     │
│  - ReviewEngine (Security policies, card approvals, Always-Allow)      │
│  - RoutineScheduler (Cron scheduling via Cronos)                       │
│  - PromptBuilder, ToolRegistry & Interfaces (IDotRepository, etc.)     │
└─────────┬───────────────────────┬────────────────────────────┬─────────┘
          │                       │                            │
┌─────────▼───────────────┐ ┌─────▼──────────────────┐ ┌───────▼─────────┐
│ OpenDot.Infrastructure  │ │ OpenDot.Integrations   │ │OpenDot.Automation│
│ (net8.0-windows10.0)    │ │ (net8.0)               │ │(net8.0-windows) │
│ - SqliteDotRepository   │ │ - OpenAiClient         │ │- PlaywrightMgr  │
│   (WAL, Migrations)     │ │ - OpenRouterClient     │ │  (Isolated prof)│
│ - DpapiVault            │ │ - ComposioClient       │ │- CommandRunner  │
│   (AES-256-GCM + DPAPI) │ │   (REST/OAuth Adapter) │ │  (CMD/PowerShell│
│ - NotificationService   │ │                        │ │- ComputerManager│
│ - SystemTrayManager     │ │                        │ │  (Screenshots,  │
│   (Shell_NotifyIcon)    │ │                        │ │   Lock-free WMI)│
└─────────────────────────┘ └────────────────────────┘ └─────────────────┘
```

---

## 2. Layered Architecture

### 2.1. OpenDot.App (WinUI 3 Presentation Layer)
- **Technology:** WinUI 3 (Windows App SDK 1.5+), compiled native XAML.
- **Pattern:** Strict MVVM (Model-View-ViewModel).
- **Dependency Injection:** Configured in `AppServices.cs` with `Microsoft.Extensions.DependencyInjection`.
- **Views & Windows:**
  - `MainWindow`: `NavigationView` container with seamless page transitions, agent search, and quick Dot creation.
  - `HomePage`: Overview of active Dots, system statistics, and quick-access cards.
  - `DotPage`: Comprehensive agent workspace featuring 4 tabs (*Chat*, *Computer*, *Files*, *Setup*).
  - `ChannelsPage`: Multi-agent channel manager facilitating inter-Dot collaboration.
  - `AppsPage`: Integration hub for Composio apps (Gmail, GitHub, Slack, Notion, Calendar, etc.).
  - `SettingsPage`: Management of API keys, default LLM provider, and theme settings.
  - `VoiceDialog`: Native voice dialog with real-time waveform animation.
  - `ApprovalCardControl` & `QuestionCardControl`: Embedded interactive cards within the conversation stream for approving sensitive actions and answering agent questions.

### 2.2. OpenDot.Core (Cognitive & Domain Layer)
- **Decoupled from UI and operating system dependencies.**
- **`AgentRuntime`:**
  - Manages agent thread lifecycles: `Idle` -> `Thinking` -> `ToolCalling` -> `AwaitingApproval` -> `Paused` -> `Error`.
  - Real-time token streaming via `IAsyncEnumerable<string>`.
  - Pause and resume idempotency.
  - Graceful cancellation using `CancellationTokenSource`.
- **`ReviewEngine`:**
  - Evaluates security rules before executing external actions (e.g., executing commands on the host machine, file mutations, external API calls).
  - Emits approval cards (`CardData`) and persists user preferences (*Always Allow* rules).
- **`RoutineScheduler`:**
  - Manages background scheduled routines using standard cron expressions (e.g., `0 9 * * *`).
  - Powered by the `Cronos` library.

### 2.3. OpenDot.Infrastructure (Persistence, Security, & Windows APIs)
- **`SqliteDotRepository`:**
  - Relational persistence in `%LOCALAPPDATA%\OpenDot\dots.db`.
  - Configured with WAL (*Write-Ahead Logging*) mode for high-concurrency read/write operations.
  - Automatic database schema migrations upon initialization.
- **`DpapiVault`:**
  - Hardware-backed master key protection via **Windows DPAPI** (`ProtectedData.Protect` with `DataProtectionScope.CurrentUser`).
  - Authenticated **AES-256-GCM** encryption for all stored credentials (OpenAI, OpenRouter, Composio API keys, saved website logins).
  - Isolated per Windows user account with zero plaintext tokens written to disk.
- **`SystemTrayManager` & `WindowsNotificationService`:**
  - Win32 notification icon (`Shell_NotifyIcon`) allowing the application to run quietly in the background, complemented by Windows Toast notifications.

### 2.4. OpenDot.Automation (Browser & Computer Automation)
- **`PlaywrightBrowserManager`:**
  - Independent Chromium browser instances per Dot stored under `%LOCALAPPDATA%\OpenDot\dots\<dotId>\browser`.
  - Complete persistence of cookies, sessions, and authentication state.
  - Safe process management via WMI (`Win32_Process` filtered by the Dot's unique `--user-data-dir`), ensuring that only the specific agent's browser is recycled without terminating the user's personal browser.
- **`CommandRunner` & `ComputerManager`:**
  - Executes shell commands within each Dot's sandboxed workspace (`%LOCALAPPDATA%\OpenDot\dots\<dotId>\workspace`) or on the host machine when explicitly granted permission.
  - Periodic viewport capture rendered inside the native *Computer* tab.

### 2.5. OpenDot.Integrations (AI & External Ecosystem)
- **`OpenAiClient`:** Native asynchronous HTTP client supporting Server-Sent Events (SSE) streaming and structured function calling.
- **`OpenRouterClient`:** Seamless access to open and proprietary LLMs (Claude, Llama, DeepSeek, Mistral, Qwen).
- **`ComposioClient`:** Direct integration adapter for Composio toolkits, OAuth management, and external action execution.

---

## 3. Data Flow Diagram

```
[ User Input in WinUI Chat ]
             │
             ▼
[ DotViewModel.SendMessageAsync ]
             │
             ▼
[ AgentRuntime.RunThreadAsync ]
             │
             ├───▶ Build Context Prompt (PromptBuilder + Memories + Rules)
             │
             ├───▶ Call LLM (OpenAI / OpenRouter) via SSE Streaming
             │      │
             │      └──▶ Update UI in real time (token by token)
             │
             ├───▶ Detect Tool Calling intent
             │      │
             │      ▼
             │   [ ReviewEngine.EvaluateActionAsync ]
             │      ├── Allowed?  ───▶ Execute Tool immediately
             │      └── Sensitive? ──▶ Display ApprovalCardControl in UI
             │                          (Awaits user confirmation)
             │
             ├───▶ Execute Tool (PlaywrightBrowser / CommandRunner / Composio)
             │
             └───▶ Persist thread state and messages to SQLite (SqliteDotRepository)
```

---

## 4. Platform Compatibility & Distribution Specifications

| Requirement | Specification |
|---|---|
| **Target OS** | Windows 10 Version 22H2 (Build 19045+) & Windows 11 (Build 22000+) |
| **Architecture** | x64 (AMD64 / Intel 64-bit) |
| **Execution Privileges** | Standard User (No administrator privileges required) |
| **Package Format** | Standalone unpackaged (`WindowsPackageType=None`) & Inno Setup installer |
| **Production Runtime** | Self-Contained (.NET 8 runtime & Windows App SDK binaries embedded) |
| **Local App Storage** | `%LOCALAPPDATA%\OpenDot\` |
