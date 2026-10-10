# Relatório de Auditoria e Matriz de Migração: Open Dot para Windows 10 Nativo

## 1. Visão Geral e Missão

O projeto **Open Dot** original é um aplicativo de agentes autônomos ("Dots") desenhado primordialmente para macOS, empacotado através do Electron executando um servidor standalone Next.js em segundo plano (`electron/main.mjs`). A interface visual original é construída em React 19, Next.js 16, Tailwind CSS e Three.js (React Three Fiber), renderizada em uma janela web do Electron.

O objetivo deste projeto é realizar um **port nativo integral** para o **Windows 10 x64 (versão 22H2 / build 19045+)** utilizando:
- **C# 12 e .NET 8 LTS**
- **WinUI 3 com Windows App SDK (WASDK)**
- **XAML Nativo** para 100% da interface do usuário (sem Electron, sem Tauri, sem WebView2 como interface principal, sem runtime Node.js em produção)
- **Microsoft.Data.Sqlite** para persistência relacional local
- **Windows DPAPI (Data Protection API)** para cofre seguro de senhas e chaves
- **Microsoft.Playwright para .NET** exclusivamente para automação de navegador do agente
- **APIs Win32 / Windows Runtime** para integração nativa com o Windows (bandeja do sistema, notificações toast, ciclo de vida e background tasks)

---

## 2. Inventário de Dependências Originais e Riscos Identificados

| Módulo / Arquivo Original | Tecnologia Original | Dependências / Chamadas Específicas de Unix/macOS | Risco / Incompatibilidade | Solução Nativa no Windows (.NET / WinUI 3) |
|---|---|---|---|---|
| `electron/main.mjs` | Electron, Node.js `child_process`, `http` | Usa `process.platform === 'darwin'`, `/bin/zsh`, `loginPath` com `printf %s "$PATH"`, atalhos `⌘Q` | Não é nativo; depende de Node.js embutido e webview Chromium. | Substituído por aplicativo WinUI 3 nativo em C# (`OpenDot.App`), ciclo de vida gerenciado por `App.xaml.cs` e Win32 Tray Icon. |
| `src/server/vault.ts` | Node.js `crypto`, CLI `security` (macOS Keychain) | Invoca binário `security find-generic-password` e `add-generic-password`. Fallback para arquivo `0600`. | A ferramenta `security` inexiste no Windows. Arquivos 0600 em NTFS não oferecem proteção do Keychain. | Substituído por `DpapiVault` utilizando `System.Security.Cryptography.ProtectedData` (`DataProtectionScope.CurrentUser`) e AES-256-GCM. |
| `src/server/computer/browser.ts` | Node.js Playwright, link simbólico Unix `SingletonLock` | Executa `fs.readlinkSync(path.join(dir, "SingletonLock"))` para matar processos do Chrome com `SIGTERM`/`SIGKILL`. | `readlinkSync` falha em sistemas Windows. Códigos de sinal Unix (`SIGTERM`, `SIGKILL`) não existem no Windows. | Inspeção de processos Windows via `System.Diagnostics.Process` filtrando rigorosamente pelo diretório de dados do Dot (`--user-data-dir`) sem afetar processos alheios. |
| `src/server/computer/shell.ts` | Bash (`/bin/bash`, `sh -c`) | Executa comandos com `/bin/bash -c`. Presume comandos Unix (`ls`, `cat`, `grep`, `curl`). | No Windows, bash inexiste nativamente sem WSL/Git Bash. Comandos falhariam. | Executor de comandos Windows baseado em PowerShell / CMD (`ProcessStartInfo`), com controle de diretório de trabalho, variáveis de ambiente seguras e timeouts. |
| `src/server/db.ts` | Node.js `node:sqlite` (DatabaseSync) | Caminho de dados em `process.env.DOTS_DATA_DIR` ou `.data`. Cria tabelas com SQL puro. | `DatabaseSync` é módulo experimental do Node 22+. Incompatível com C#. | `Microsoft.Data.Sqlite` no .NET 8 com armazenamento oficial em `%LOCALAPPDATA%\OpenDot\dots.db`, WAL mode e migrações idempotentes. |
| `src/server/agent/runtime.ts` | TypeScript, OpenAI SDK JS | Stream SSE via `openai/resources/responses/responses`, loop assíncrono de tool calling, cards e aprovações. | Dependência do runtime JS. | Reimplementação completa em C# no `OpenDot.Core.Agents` com suporte a SSE streaming, chamada assíncrona de ferramentas e cancelamento por `CancellationToken`. |
| `src/server/agent/review.ts` | TypeScript, OpenAI SDK JS | Avaliação de regras de segurança via JSON schema estrito. | Dependência do runtime JS. | Motor em C# (`ReviewEngine`) comunicando-se com endpoints de modelos OpenAI/OpenRouter para avaliação semântica de regras. |
| `src/server/scheduler.ts` | Pacote NPM `croner` | Avaliação de expressões cron e agendamento em memória. | Dependência de biblioteca JS. | Agendador nativo .NET (`Cronos` ou avaliador de cron puro em C#) gerenciando rotinas em segundo plano. |
| `src/server/triggers.ts` | `@composio/core` (JS SDK) | Cliente SDK Composio, webhook/stream de eventos. | SDK oficial é focado em Node.js/Python. | Adaptador HTTP REST/SSE em C# (`ComposioClient`) implementando contratos de API do Composio (Toolkits, Triggers, OAuth). |
| `src/server/voice.ts` | OpenAI Realtime WebRTC / WebSocket | Usa WebRTC pelo navegador do Electron com permissões do browser. | Dependência de WebRTC em navegador. | Módulo de voz .NET (`VoiceService`) integrando com WebSocket do OpenAI Realtime API e captura de áudio via `Windows.Media.Capture` / CoreAudio / WASAPI. |
| `src/components/*` | React 19, Next.js, Three.js, Lucide icons | Interface web, componentes HTML, renderização 3D do personagem "Dot" com canvas WebGL. | Não é nativo de desktop. Viola o requisito eliminatório do projeto. | Interface 100% XAML nativa WinUI 3 com `NavigationView`, `Page`, `ItemsRepeater`, ícones nativos `FontIcon` / Segoe Fluent Icons, e controle de renderização vetorial do Dot. |

---

## 3. Matriz de Paridade Funcional

| Funcionalidade Original | Status no Port Windows | Componente Windows Responsável |
|---|---|---|
| Múltiplos agentes ("Dots") independentes | **Implementado** | `OpenDot.Core.Agents.AgentRuntime`, `SqliteDotRepository` |
| Criação, Edição, Personalização Visual do Dot | **Implementado** | `OpenDot.App.Views.SetupPaneControl`, `LookEditorControl` |
| Conversação com Streaming em Tempo Real | **Implementado** | `OpenDot.App.Views.ChatControl`, `AgentRuntime.TurnAsync` |
| Cartões de Aprovação ("Ask First", "Allow", "Never") | **Implementado** | `OpenDot.Core.Policies.ReviewEngine`, `ApprovalCardControl` |
| Cartões de Pergunta e Opções Interativas | **Implementado** | `QuestionCardControl`, `AgentRuntime.ResolveCardAsync` |
| Conexão de Aplicativos (Composio OAuth Connect) | **Implementado** | `OpenDot.Integrations.Composio.ComposioClient`, `ConnectCardControl` |
| Memória Duradoura do Dot (`remember` / `forget`) | **Implementado** | `MemoryManager`, `SqliteDotRepository` |
| Habilidades Reutilizáveis (`save_skill` / `use_skill`) | **Implementado** | `SkillManager`, `SqliteDotRepository` |
| Rotinas Agendadas (Cron) | **Implementado** | `OpenDot.Core.Scheduling.RoutineScheduler` |
| Gatilhos Externos (Triggers de Apps Composio) | **Implementado** | `OpenDot.Integrations.Composio.TriggerService` |
| Automação de Navegador Isolada por Dot | **Implementado** | `OpenDot.Automation.Browser.PlaywrightBrowserManager` |
| Captura de Tela e Transmissão de Ações | **Implementado** | `ComputerPaneControl`, `PlaywrightBrowserManager.ScreenshotAsync` |
| Assumir e Devolver Controle do Navegador | **Implementado** | `ComputerPaneControl`, `PlaywrightBrowserManager.TakeOverAsync` |
| Workspace de Arquivos do Agente | **Implementado** | `WorkspaceManager`, `FilesPaneControl` |
| Execução de Comandos do Dot | **Implementado** | `OpenDot.Automation.Workspaces.CommandRunner` |
| Cofre Seguro de Credenciais e Auto-preenchimento | **Implementado** | `OpenDot.Infrastructure.Security.DpapiVault` |
| Chamadas de Voz em Tempo Real | **Implementado** | `OpenDot.App.Views.VoiceDialog`, `VoiceService` |
| Canais em Grupo (Multi-Dot) | **Implementado** | `OpenDot.App.Views.ChannelsPage`, `ChannelCoordinator` |
| Ícone na Bandeja e Execução em Segundo Plano | **Implementado** | `OpenDot.Infrastructure.Windows.TrayIconManager` |
| Notificações Nativas do Windows 10 | **Implementado** | `WindowsToastNotificationService` |

---

## 4. Arquitetura da Solução .NET

A solução `OpenDot.Windows.sln` é estruturada em 5 projetos principais e 1 projeto de testes:

```
OpenDot.Windows/
├── src/
│   ├── OpenDot.Core/              # Entidades de domínio, interfaces, runtime dos agentes, regras e agendamento
│   ├── OpenDot.Infrastructure/    # SQLite com WAL, DPAPI vault, Win32 Tray Icon, Toasts do Windows
│   ├── OpenDot.Integrations/      # Clientes HTTP/SSE para OpenAI (Responses API), OpenRouter, Composio, E2B
│   ├── OpenDot.Automation/        # Playwright .NET para Chrome isolado, automação de telas e executor de comandos
│   └── OpenDot.App/               # Aplicação WinUI 3 desktop (XAML, MVVM, navegação, temas claro/escuro)
├── tests/
│   └── OpenDot.Tests/             # Testes de unidade e integração (xUnit)
├── packaging/                     # Configurações de MSIX e instalador standalone
└── docs/                          # Documentação técnica e arquitetural
```
