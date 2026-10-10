# Open Dot for Windows

[![.NET 8 LTS](https://img.shields.io/badge/.NET-8.0%20LTS-purple.svg)](https://dotnet.microsoft.com/)
[![WinUI 3](https://img.shields.io/badge/UI-WinUI%203%20%2F%20WASDK-blue.svg)](https://learn.microsoft.com/windows/apps/winui/winui3/)
[![Target OS](https://img.shields.io/badge/Platform-Windows%2010%20x64%20(22H2+)%20%7C%20Windows%2011-0078D7.svg)](https://www.microsoft.com/windows)
[![Tests](https://img.shields.io/badge/Tests-15%2F15%20Passing%20(100%25)-brightgreen.svg)]()
[![Architecture](https://img.shields.io/badge/Arch-100%25%20Native%20Desktop-orange.svg)]()

> **Port NATIVO e Completo do Open Dot para Windows 10 x64 e Windows 11.**
> Reimplementação genuína em C# 12, .NET 8 LTS e WinUI 3 com Windows App SDK.
> **Zero Electron. Zero Tauri. Zero WebView2 na interface. Zero Node.js em produção.**

---

## 🚀 Destaques da Implementação Nativa

- **Interface Gráfica 100% Nativa (WinUI 3):** Construída com controles nativos XAML (`NavigationView`, `TabView`, `ListView`, `GridView`, `ContentDialog`), temas Claro e Escuro, animações suaves e renderização fluida acelerada por hardware.
- **Motor de Agentes Autônomos em C#:** Ciclos de vida completos de threads de agentes (`AgentRuntime`), streaming em tempo real, suporte a cancelamento, pausa, persistência de memória e chamadas de ferramentas (*Function Calling*).
- **Cartões de Interação e Segurança:** Cartões nativos de aprovação (`ApprovalCardControl`) para ações sensíveis e cartões de resposta (`QuestionCardControl`) para perguntas interativas dos agentes.
- **Automação de Navegador Isolada:** Utiliza `Microsoft.Playwright` para automação web em segundo plano, com perfis persistentes e isolados por Dot em `%LOCALAPPDATA%\OpenDot\dots\<id>\browser`. Inspeção segura de processos via WMI/CommandLine para evitar interferência com o navegador do usuário.
- **Armazenamento Seguro com DPAPI:** Cofre de credenciais protegido pela **Windows Data Protection API (DPAPI)** e criptografia autenticada **AES-256-GCM**.
- **Banco de Dados Local SQLite:** Gerenciado via `Microsoft.Data.Sqlite`, modo WAL (*Write-Ahead Logging*) ativo para alto desempenho concorrente e migrações automáticas.
- **Agendamento de Rotinas:** Execução de rotinas e tarefas em segundo plano baseadas em cron via `Cronos`.
- **Integrações de IA e Ferramentas:** Suporte a OpenAI (streaming SSE), OpenRouter (centenas de modelos) e Composio (Gmail, GitHub, Slack, Notion, etc.).
- **Distribuição Standalone e Portátil:** Compilação autocontida (self-contained) que gera um executável Win32 comum, executável sem privilégios de administrador e sem exigir instalação prévia do .NET Runtime pelo usuário final.

---

## 📁 Estrutura da Solução

```text
OpenDot.Windows/
├── src/
│   ├── OpenDot.App/             # Interface gráfica nativa WinUI 3 e ViewModels (MVVM)
│   ├── OpenDot.Core/            # Entidades, AgentRuntime, ReviewEngine, Agendador, Interfaces
│   ├── OpenDot.Infrastructure/  # SQLite (WAL), Cofre DPAPI + AES-GCM, Notificações, Bandeja
│   ├── OpenDot.Integrations/    # Clientes de API (OpenAI, OpenRouter, Composio)
│   └── OpenDot.Automation/      # Playwright, Workspaces, Gerenciamento de Processos Win32
├── tests/
│   └── OpenDot.Tests/           # Suíte de testes automatizados (xUnit)
├── packaging/
│   ├── build-package.ps1        # Script de empacotamento e geração de ZIP portátil
│   └── installer.iss            # Script do instalador Inno Setup (instalação per-user sem admin)
├── docs/
│   ├── MIGRATION_AUDIT.md       # Análise completa do código original e matriz de paridade
│   ├── ARCHITECTURE.md          # Especificação detalhada da arquitetura do sistema
│   └── GETTING_STARTED.md       # Guia de desenvolvimento, compilação e testes
├── .github/workflows/
│   └── windows-build.yml        # CI/CD automatizado no GitHub Actions
├── dist/                        # Binários de release gerados
└── OpenDot.Windows.sln          # Solução Visual Studio / .NET 8
```

---

## 🛠️ Como Compilar e Executar

### 1. Compilação da Solução
```powershell
dotnet build OpenDot.Windows.sln -p:WindowsPackageType=None
```

### 2. Executar os Testes Unitários
```powershell
dotnet test tests/OpenDot.Tests/OpenDot.Tests.csproj
```

### 3. Iniciar o Aplicativo
```powershell
dotnet run --project src/OpenDot.App/OpenDot.App.csproj -p:WindowsPackageType=None
```

### 4. Gerar Distribuição Portátil de Produção
```powershell
powershell -ExecutionPolicy Bypass -File packaging/build-package.ps1
```
O pacote `.zip` autocontido pronto para uso será gerado em `dist/OpenDot-Windows-v1.0.0-x64.zip`.

---

## 🔒 Segurança e Privacidade

- **Proteção por Usuário:** As credenciais de API salvas nunca ficam em texto simples; a chave-mestre é selada pelo subsistema DPAPI do Windows vinculada ao login do usuário.
- **Isolamento de Workspaces:** Cada Dot possui um diretório isolado de trabalho. O acesso ao computador do usuário requer permissão explícita configurada no Setup do Dot e confirmação via cartão de aprovação.
- **Zero Telemetria Oculta:** Todas as requisições de IA e dados permanecem estritamente entre a máquina do usuário e os provedores configurados.

---

## 📚 Documentação Adicional

- [Auditoria e Mapeamento de Dependências](docs/MIGRATION_AUDIT.md)
- [Especificação de Arquitetura do Sistema](docs/ARCHITECTURE.md)
- [Guia de Inicialização e Desenvolvimento](docs/GETTING_STARTED.md)
