# Arquitetura do Open Dot for Windows

## 1. Visão Geral do Sistema

O **Open Dot for Windows** é uma implementação 100% nativa do assistente autônomo Open Dot para **Windows 10 x64 (22H2 / build 19045+)** e **Windows 11**. 

Ao contrário de empacotamentos web (Electron, Tauri ou wrappers com WebView2), o projeto foi reconstruído do zero em **C# 12**, **.NET 8 LTS** e **WinUI 3 com Windows App SDK**. Ele elimina completamente a dependência de Node.js, pnpm e servidores web em produção, proporcionando uma experiência de desktop com consumo mínimo de recursos, inicialização imediata e segurança integrada ao sistema operacional Windows.

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
│  - AgentRuntime (Streaming, Thread Loop, Pausabilidade, Tool Engine)   │
│  - ReviewEngine (Políticas de segurança, aprovações, Always-Allow)      │
│  - RoutineScheduler (Cron agendado via Cronos)                         │
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

## 2. Camadas da Aplicação

### 2.1. OpenDot.App (Interface WinUI 3)
- **Tecnologia:** WinUI 3 (Windows App SDK 1.5+), XAML nativo compilado.
- **Padrão:** MVVM (Model-View-ViewModel) estrito.
- **Injeção de Dependências:** Configurada em `AppServices.cs` com `ServiceCollection`.
- **Janela e Telas:**
  - `MainWindow`: `NavigationView` com navegação fluida entre telas, pesquisa de agentes e botão rápido de criação.
  - `HomePage`: Visão geral dos Dots, estatísticas e atalhos rápidos.
  - `DotPage`: Workspace completo com abas (*Chat*, *Computer*, *Files*, *Setup*).
  - `ChannelsPage`: Gerenciador de canais para comunicação entre múltiplos Dots.
  - `AppsPage`: Catálogo de integrações do Composio (Gmail, GitHub, Slack, Notion, etc.).
  - `SettingsPage`: Configuração de chaves de API, provedor padrão e preferências visuais.
  - `VoiceDialog`: Diálogo nativo para chamadas de voz com animação de onda sonora em tempo real.
  - `ApprovalCardControl` & `QuestionCardControl`: Cartões interativos incorporados nas conversas para confirmação de ações perigosas e perguntas interativas do agente.

### 2.2. OpenDot.Core (Motor Cognitivo e Domínio)
- **Totalmente desacoplado de UI e APIs específicas de sistema operacional.**
- **`AgentRuntime`:**
  - Ciclo de vida dos agentes: `Idle` -> `Thinking` -> `ToolCalling` -> `AwaitingApproval` -> `Paused` -> `Error`.
  - Suporte a streaming de respostas via `IAsyncEnumerable<string>`.
  - Tratamento de pausa e retomada idempotente.
  - Cancelamento limpo via `CancellationTokenSource`.
- **`ReviewEngine`:**
  - Avaliação de políticas de segurança antes da execução de ferramentas (ex: execução de comandos no computador do usuário, exclusão de arquivos, chamadas a APIs externas).
  - Geração de cartões de aprovação (`CardData`) e persistência de regras de auto-aprovação (*Always Allow*).
- **`RoutineScheduler`:**
  - Agendamento de tarefas autônomas em segundo plano com expressões cron padrão (ex: `0 9 * * *`).
  - Execução desacoplada usando a biblioteca `Cronos`.

### 2.3. OpenDot.Infrastructure (Persistência, Segurança e Windows APIs)
- **`SqliteDotRepository`:**
  - Armazenamento local em `%LOCALAPPDATA%\OpenDot\dots.db`.
  - Modo WAL (*Write-Ahead Logging*) ativo para alto desempenho concorrente.
  - Auto-migrações automáticas de schema na inicialização.
- **`DpapiVault`:**
  - Chave-mestre derivada protegida via **Windows DPAPI** (`ProtectedData.Protect`).
  - Criptografia autenticada **AES-256-GCM** para todas as credenciais sensíveis (chaves OpenAI, OpenRouter, Composio, senhas de perfis).
  - Isolamento seguro por usuário do Windows, sem chaves em texto claro em disco.
- **`SystemTrayManager` & `NotificationService`:**
  - Notificações nativas do Windows e ícone na bandeja do sistema para execução ininterrupta em segundo plano.

### 2.4. OpenDot.Automation (Navegação Web e Computador)
- **`PlaywrightBrowserManager`:**
  - Instâncias isoladas do Chromium por Dot, armazenadas em `%LOCALAPPDATA%\OpenDot\dots\<dotId>\browser`.
  - Persistência total de cookies, sessões e logins.
  - Detecção segura de processos via WMI (`Win32_Process` com `CommandLine`), garantindo que apenas processos Chromium do Dot específico sejam gerenciados, sem afetar o navegador do usuário.
- **`CommandRunner` & `ComputerManager`:**
  - Execução de comandos no workspace isolado de cada Dot (`%LOCALAPPDATA%\OpenDot\dots\<dotId>\workspace`) ou no computador do usuário quando explicitamente autorizado.
  - Captura periódica de tela para exibição no painel nativo *Computer*.

### 2.5. OpenDot.Integrations (Provedores de Inteligência e Ecossistema)
- **`OpenAiClient`:** Chamadas nativas via `System.Net.Http`, streaming Server-Sent Events (SSE), function calling estruturado.
- **`OpenRouterClient`:** Acesso a centenas de modelos de IA (Claude, Llama, DeepSeek, Mistral) mantendo compatibilidade de API.
- **`ComposioClient`:** Conexão nativa com centenas de ferramentas externas, autenticação OAuth e execução remota de ações.

---

## 3. Fluxo de Execução de Mensagens e Ferramentas

```
[ Usuário digita na UI ]
         │
         ▼
[ DotViewModel.SendMessageAsync ]
         │
         ▼
[ AgentRuntime.RunThreadAsync ]
         │
         ├───▶ Monta prompt dinâmico (PromptBuilder + Memória + Regras)
         │
         ├───▶ Chama LLM (OpenAI / OpenRouter) com streaming SSE
         │      │
         │      └──▶ Atualiza UI em tempo real caractere por caractere
         │
         ├───▶ Detecta intenção de Tool Calling
         │      │
         │      ▼
         │   [ ReviewEngine.EvaluateActionAsync ]
         │      ├── Ação Permitida? ───▶ Executa Ferramenta
         │      └── Ação Sensível?  ───▶ Emite ApprovalCardControl na UI
         │                                (Aguarda resposta do usuário)
         │
         ├───▶ Executa ferramenta (PlaywrightBrowser / CommandRunner / Composio)
         │
         └───▶ Persiste mensagem e estado no SQLite (SqliteDotRepository)
```

---

## 4. Requisitos de Compatibilidade e Distribuição

| Aspecto | Especificação |
|---|---|
| **Sistema Operacional** | Windows 10 versão 22H2 (build 19045+) e Windows 11 (build 22000+) |
| **Arquitetura** | x64 (AMD64 / Intel 64-bit) |
| **Privilégios** | Usuário padrão (não requer privilégios de administrador) |
| **Tipo de Pacote** | Standalone unpackaged (`WindowsPackageType=None`) ou instalador Inno Setup |
| **Dependências em Produção** | Nenhuma (Self-Contained .NET 8 Runtime + Windows App SDK embutidos) |
| **Armazenamento de Dados** | `%LOCALAPPDATA%\OpenDot\` |
