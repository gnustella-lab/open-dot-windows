# Getting Started & Development Guide: Open Dot for Windows

This guide explains how to compile, test, package, and run **Open Dot for Windows** on **Windows 10 x64** or **Windows 11**.

---

## 1. Development Prerequisites

To build the project from source:

- **Operating System:** Windows 10 Version 22H2 (Build 19045 or later) or Windows 11.
- **.NET 8 LTS SDK:** (.NET 8.0.400 or later).
- *(Optional)* **Visual Studio 2022** (with the *.NET Desktop Development* workload) or **Visual Studio Code** with the C# Dev Kit extension.
- *(Optional for building the installer .exe)* **Inno Setup 6**.

> [!NOTE]
> Node.js, pnpm, Python, and Electron runtimes are NOT required to build or run Open Dot for Windows.

---

## 2. Building the Solution

Open PowerShell and navigate to the `OpenDot.Windows` directory:

```powershell
cd OpenDot.Windows

# 1. Restore NuGet packages
dotnet restore OpenDot.Windows.sln

# 2. Build the complete solution in Debug mode
dotnet build OpenDot.Windows.sln -p:WindowsPackageType=None
```

The `-p:WindowsPackageType=None` flag configures the Windows App SDK for unpackaged desktop execution, enabling the application to run directly as a standard Win32 executable without MSIX package registration or developer mode certificates.

---

## 3. Running Automated Tests

The solution includes an automated unit test suite covering the SQLite database, DPAPI vault with AES-256-GCM, the ReviewEngine security policies, and AI API clients:

```powershell
dotnet test tests/OpenDot.Tests/OpenDot.Tests.csproj
```

**Expected Output:**
```text
Passed!  – Failed: 0, Passed: 15, Skipped: 0, Total: 15, Duration: ~370 ms
```

---

## 4. Running the Application

To launch the app directly in debug mode:

```powershell
dotnet run --project src/OpenDot.App/OpenDot.App.csproj -p:WindowsPackageType=None
```

Or execute the compiled binary directly:
```powershell
.\src\OpenDot.App\bin\Debug\net8.0-windows10.0.19041.0\win-x64\OpenDot.App.exe
```

---

## 5. Generating Release Packages & Installer

To generate an optimized, self-contained release package:

### Automated Packaging (Recommended):
```powershell
powershell -ExecutionPolicy Bypass -File packaging/build-package.ps1
```

This script automatically executes:
1. Optimized Release build targeting `win-x64`.
2. Standalone packaging under `OpenDot.Windows/dist/OpenDot-win-x64/`.
3. Portable archive generation: `OpenDot.Windows/dist/OpenDot-Windows-v1.0.0-x64.zip`.
4. Inno Setup compilation (if installed): `OpenDot.Windows/dist/installer/OpenDot-Windows-Setup-1.0.0.exe`.

### Manual CLI Publish:
```powershell
dotnet publish src/OpenDot.App/OpenDot.App.csproj `
  -c Release `
  -r win-x64 `
  --self-contained true `
  -p:WindowsPackageType=None `
  -p:Platform=x64 `
  -o dist/OpenDot-win-x64
```

---

## 6. Initial AI Authentication

Open **Settings** in the sidebar and choose one OpenAI sign-in method:

- **API key:** Enter an OpenAI Platform key for API billing.
- **ChatGPT / Codex:** Select **Sign in with ChatGPT / Codex**, finish the browser login, then use your eligible ChatGPT workspace subscription. OpenDot uses the Codex Responses backend and the default model `codex:gpt-5.5`; account/model access can vary.

These methods remain separate. OpenDot never silently switches from your Codex subscription to API billing or the reverse. Change the selected method explicitly in Settings. A Dot's optional custom model overrides the default; leave it blank for the current default. Use the `codex:` prefix for Codex models, no prefix for OpenAI API models, and `openrouter:` for OpenRouter.

Codex sign-in credentials and API keys are sealed with authenticated AES-256-GCM and Windows DPAPI. The callback runs locally on `127.0.0.1:1455`; the browser sign-in uses OAuth PKCE and state validation. Disconnect clears this app's locally saved session without logging out of Codex CLI or other OpenAI apps. Closing Settings cancels an active sign-in. OpenRouter and Composio keys remain independently configurable.

---

## 7. User Data Directory

Runtime data is stored under the user's local application directory:

- **SQLite Database:** `%LOCALAPPDATA%\OpenDot\dots.db`
- **Encrypted DPAPI Vault:** `%LOCALAPPDATA%\OpenDot\vault.json`
- **Agent Workspaces:** `%LOCALAPPDATA%\OpenDot\dots\<dotId>\workspace\`
- **Isolated Browser Profiles:** `%LOCALAPPDATA%\OpenDot\dots\<dotId>\browser\`
- **Application Logs:** `%LOCALAPPDATA%\OpenDot\logs\`
