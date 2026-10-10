## Open Dot for Windows v1.0.0 — Native Desktop Release

Official first release of **Open Dot for Windows**, a 100% native desktop port built with **C# 12**, **.NET 8 LTS**, and **WinUI 3 with the Windows App SDK**.

### 🌟 Release Highlights
- **100% Native WinUI 3 UI:** Built using native XAML controls with hardware acceleration, Dark/Light theme support, and Windows 10 & 11 title bar integration.
- **Zero Web Wrappers:** Zero Electron, zero Tauri, zero WebView2 in the main interface, and zero Node.js / pnpm runtime dependencies in production.
- **Official Windows Installer:** Easy per-user installation without requiring administrator privileges (`%LOCALAPPDATA%\Programs\OpenDot`). Automatically configures Start Menu shortcuts and standard Windows uninstaller.
- **Hardware-Backed DPAPI Vault:** API keys (OpenAI, OpenRouter, Composio) and saved website credentials protected with authenticated AES-256-GCM encryption sealed by the Windows Data Protection API (DPAPI).
- **Local SQLite Storage:** Integrated relational database at `%LOCALAPPDATA%\OpenDot\dots.db` configured with WAL (Write-Ahead Logging) mode and automatic migrations.
- **Isolated Browser Automation:** Independent Chromium profiles per Dot via Microsoft.Playwright with safe WMI process inspection to prevent interference with personal browsers.
- **Scheduled Background Routines:** Autonomous background job execution via cron expressions powered by Cronos, complemented by native Windows Toast notifications.

---

### 📦 Downloads & Installation
- **`OpenDot-Windows-Setup-1.0.0.exe`**: Official Windows 10/11 x64 Installer (Recommended). Installs for the current user without requiring administrator permissions.
- **`OpenDot-Windows-v1.0.0-x64.zip`**: Portable standalone release. Unzip and run `OpenDot.App.exe` directly.

---

### 🔐 Checksums (SHA-256)
- **`OpenDot-Windows-Setup-1.0.0.exe`**:
  `C5C7023B14E70EDF702E074F560AA1B950FD864F6DD1D97FC49DBA70219C4C0F`
- **`OpenDot-Windows-v1.0.0-x64.zip`**:
  `3AB4B7B1C98E55C3C14E50BB87A7B5C2BB24160DED933F5A7892E131F5A4DF19`
