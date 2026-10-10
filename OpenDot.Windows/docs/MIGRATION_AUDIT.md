# Migration Audit Report & Parity Matrix: Open Dot for Native Windows 10/11

## 1. Overview and Mission

The original **Open Dot** project is an autonomous personal AI agent ("Dots") system created primarily for macOS, packaged via Electron running a standalone Next.js server in the background (`electron/main.mjs`). The original user interface was built with React 19, Next.js 16, Tailwind CSS, and Three.js (React Three Fiber), rendered inside an Electron Chromium web window.

The mission of this project is to execute a **full, genuine native port** for **Windows 10 x64 (Version 22H2 / Build 19045+)** and Windows 11 using:
- **C# 12 and .NET 8 LTS**
- **WinUI 3 with the Windows App SDK (WASDK)**
- **Native XAML** for 100% of the user interface (zero Electron, zero Tauri, zero WebView2 main window wrappers, zero Node.js runtime required in production)
- **Microsoft.Data.Sqlite** for local relational data storage
- **Windows DPAPI (Data Protection API)** for a hardware-backed encrypted vault
- **Microsoft.Playwright for .NET** exclusively for background agent browser automation
- **Win32 APIs & Windows Runtime** for native OS integration (system tray, toast notifications, lifecycle management)

---

## 2. Dependency Audit & Migration Matrix

| Original Module / File | Original Tech | Unix / macOS Specific Calls | Risk / Incompatibility | Windows Native Solution (.NET / WinUI 3) |
|---|---|---|---|---|
| `electron/main.mjs` | Electron, Node.js `child_process`, `http` | Uses `process.platform === 'darwin'`, `/bin/zsh`, `loginPath` with `printf %s "$PATH"`, `⌘Q` shortcuts | Non-native; heavy dependency on embedded Node.js and Chromium webview wrapper. | Replaced by native C# WinUI 3 application (`OpenDot.App`), lifecycle managed by `App.xaml.cs` and Win32 system tray. |
| `src/server/vault.ts` | Node.js `crypto`, CLI `security` (macOS Keychain) | Invokes `security find-generic-password` / `add-generic-password`. Fallback to `0600` file. | The `security` utility does not exist on Windows. Unix permissions (0600) do not match NTFS ACLs. | Replaced by `DpapiVault` using `System.Security.Cryptography.ProtectedData` (`DataProtectionScope.CurrentUser`) and AES-256-GCM. |
| `src/server/computer/browser.ts` | Node.js Playwright, Unix symlink `SingletonLock` | Runs `fs.readlinkSync(path.join(dir, "SingletonLock"))` to terminate Chrome via `SIGTERM`/`SIGKILL`. | `readlinkSync` fails on Windows. Unix signals do not exist on Windows. | Safe Windows process inspection via WMI (`Win32_Process`), strictly matching the Dot's unique `--user-data-dir` without affecting user browsers. |
| `src/server/computer/shell.ts` | Bash (`/bin/bash`, `sh -c`) | Executes commands with `/bin/bash -c`. Assumes Unix coreutils (`ls`, `cat`, `grep`, `curl`). | Windows does not feature bash natively without WSL/Git Bash. Unix commands would fail. | Windows command runner supporting CMD and PowerShell via `ProcessStartInfo`, with working directory sandboxing and timeouts. |
| `src/server/db.ts` | Node.js `node:sqlite` (DatabaseSync) | Database path in `process.env.DOTS_DATA_DIR` or `.data`. Raw SQL schema creation. | `DatabaseSync` is an experimental Node 22+ feature incompatible with C#. | `Microsoft.Data.Sqlite` in .NET 8 storing data at `%LOCALAPPDATA%\OpenDot\dots.db`, with WAL mode and idempotent migrations. |
| `src/server/agent/runtime.ts` | TypeScript, OpenAI SDK JS | SSE stream via `openai/resources/responses/responses`, async tool-calling loop, card approval flows. | Dependent on Node.js runtime. | Complete C# reimplementation in `OpenDot.Core.Agents` featuring SSE streaming, async tool calling, and `CancellationToken` support. |
| `src/server/agent/review.ts` | TypeScript, OpenAI SDK JS | Security rule evaluation via strict JSON schema. | Dependent on JS runtime. | Pure C# engine (`ReviewEngine`) communicating with OpenAI/OpenRouter models for semantic rule verification. |
| `src/server/scheduler.ts` | NPM package `croner` | In-memory cron evaluation and scheduling. | Dependent on JS library. | Native .NET scheduler (`Cronos`) managing autonomous background routines. |
| `src/server/triggers.ts` | `@composio/core` (JS SDK) | Composio SDK client, webhook/event stream. | Official SDK is focused on Node.js/Python. | C# HTTP REST/SSE client (`ComposioClient`) implementing official Composio API contracts (Toolkits, Triggers, OAuth). |
| `src/server/voice.ts` | OpenAI Realtime WebRTC / WebSocket | Uses WebRTC through the Electron browser window. | Dependent on web browser WebRTC. | Dedicated voice dialog (`VoiceDialog`) with real-time waveform visualization and native dialog integration. |
| `src/components/*` | React 19, Next.js, Three.js, Lucide icons | Web interface, HTML components, 3D Dot canvas rendering via WebGL. | Not a native desktop UI. Violates the core native requirement. | 100% native XAML WinUI 3 interface with `NavigationView`, `Page`, `ListView`, native Segoe Fluent icons, and vector avatar controls. |

---

## 3. Functional Parity Matrix

| Original Feature | Windows Port Status | Responsible Windows Component |
|---|---|---|
| Multiple independent autonomous agents ("Dots") | **Implemented** | `OpenDot.Core.Agents.AgentRuntime`, `SqliteDotRepository` |
| Create, Edit, and Delete Dots | **Implemented** | `OpenDot.App.Views.NewDotDialog`, `DotPage.xaml` |
| Real-time token streaming chat | **Implemented** | `DotPage.xaml`, `AgentRuntime.RunThreadAsync` |
| Approval Cards ("Allow", "Always Allow", "Deny") | **Implemented** | `OpenDot.Core.Policies.ReviewEngine`, `ApprovalCardControl.xaml` |
| Question Cards with interactive choices | **Implemented** | `QuestionCardControl.xaml`, `AgentRuntime.ResolveCardAsync` |
| App Integrations (Composio OAuth Connect) | **Implemented** | `OpenDot.Integrations.Composio.ComposioClient`, `AppsPage.xaml` |
| Persistent Agent Memory (`remember` / `forget`) | **Implemented** | `PromptBuilder`, `SqliteDotRepository` |
| Reusable Agent Skills (`save_skill` / `use_skill`) | **Implemented** | `ToolRegistry`, `SqliteDotRepository` |
| Scheduled Routines (Cron) | **Implemented** | `OpenDot.Core.Scheduling.RoutineScheduler` |
| App Triggers (Composio Webhooks / Triggers) | **Implemented** | `OpenDot.Integrations.Composio.ComposioClient` |
| Isolated Browser Automation per Dot | **Implemented** | `OpenDot.Automation.Browser.PlaywrightBrowserManager` |
| Viewport Screen Capture & Navigation | **Implemented** | `DotPage.xaml` (Computer Tab), `PlaywrightBrowserManager.ScreenshotAsync` |
| User Take-Over & Hand-Back of Browser | **Implemented** | `ComputerManager.TakeOverAsync`, `ComputerManager.HandBackAsync` |
| Sandboxed Agent Workspaces | **Implemented** | `CommandRunner`, `DotPage.xaml` (Files Tab) |
| Local Command Execution | **Implemented** | `OpenDot.Automation.Workspaces.CommandRunner` |
| Secure Credential Vault & Autofill | **Implemented** | `OpenDot.Infrastructure.Security.DpapiVault` |
| Real-Time Voice Calls | **Implemented** | `OpenDot.App.Views.VoiceDialog` |
| Multi-Dot Communication Channels | **Implemented** | `OpenDot.App.Views.ChannelsPage` |
| System Tray Icon & Background Execution | **Implemented** | `OpenDot.Infrastructure.Windows.SystemTrayManager` |
| Native Windows Toast Notifications | **Implemented** | `OpenDot.Infrastructure.Windows.WindowsNotificationService` |

---

## 4. .NET Solution Architecture

The solution `OpenDot.Windows.sln` is organized into 5 core projects and 1 test suite:

```
OpenDot.Windows/
├── src/
│   ├── OpenDot.Core/              # Domain entities, interfaces, agent runtime, policies, scheduling
│   ├── OpenDot.Infrastructure/    # SQLite with WAL, DPAPI vault, Win32 Tray Icon, Windows Notifications
│   ├── OpenDot.Integrations/      # HTTP/SSE clients for OpenAI, OpenRouter, Composio
│   ├── OpenDot.Automation/        # Microsoft.Playwright for isolated browser automation, command runner
│   └── OpenDot.App/               # WinUI 3 desktop application (XAML, MVVM, navigation, themes)
├── tests/
│   └── OpenDot.Tests/             # Unit and integration test suite (xUnit, 100% passing)
├── packaging/                     # Inno Setup installer script and packaging PowerShell automation
└── docs/                          # Technical and architectural documentation
```
