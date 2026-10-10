# Guia de Inicialização e Desenvolvimento: Open Dot for Windows

Este guia orienta como compilar, testar, empacotar e executar o **Open Dot for Windows** em máquinas **Windows 10 x64** ou **Windows 11**.

---

## 1. Pré-requisitos de Desenvolvimento

Para compilar o projeto a partir do código-fonte:

- **Sistema Operacional:** Windows 10 Versão 22H2 (Build 19045 ou superior) ou Windows 11.
- **SDK .NET 8 LTS:** (.NET 8.0.400 ou superior).
- *(Opcional)* **Visual Studio 2022** (com as cargas de trabalho *.NET Desktop Development* e *Windows App SDK*) ou **Visual Studio Code** com a extensão C# Dev Kit.
- *(Opcional para instalador setup.exe)* **Inno Setup 6**.

> [!NOTE]
> Nenhum runtime de Node.js, pnpm, Python ou Electron é necessário para compilar ou executar o Open Dot for Windows.

---

## 2. Compilação da Solução

Abra um terminal PowerShell na pasta `OpenDot.Windows`:

```powershell
cd OpenDot.Windows

# 1. Restaure os pacotes NuGet
dotnet restore OpenDot.Windows.sln

# 2. Compile a solução inteira em modo Debug
dotnet build OpenDot.Windows.sln -p:WindowsPackageType=None
```

A flag `-p:WindowsPackageType=None` configura o Windows App SDK para compilação unpackaged, permitindo que o aplicativo seja executado diretamente como um executável Win32 comum, sem necessidade de registro de pacote MSIX ou modo de desenvolvedor.

---

## 3. Execução dos Testes Automatizados

O projeto inclui uma suíte abrangente de testes unitários cobrindo o banco de dados SQLite, o cofre de segurança DPAPI com AES-256-GCM, o motor de regras de segurança (ReviewEngine) e os clientes de API:

```powershell
dotnet test tests/OpenDot.Tests/OpenDot.Tests.csproj
```

**Resultado esperado:**
```text
Aprovado! – Com falha: 0, Aprovado: 15, Ignorado: 0, Total: 15, Duração: ~400 ms
```

---

## 4. Executando o Aplicativo

Para iniciar o aplicativo diretamente em modo de depuração:

```powershell
dotnet run --project src/OpenDot.App/OpenDot.App.csproj -p:WindowsPackageType=None
```

Ou execute o binário compilado:
```powershell
.\src\OpenDot.App\bin\Debug\net8.0-windows10.0.19041.0\win-x64\OpenDot.App.exe
```

---

## 5. Geração do Pacote de Distribuição (Release)

Para gerar uma distribuição de produção portátil e autocontida (self-contained), sem dependências externas:

### Método Automático (via script PowerShell):
```powershell
powershell -ExecutionPolicy Bypass -File packaging/build-package.ps1
```

O script realizará:
1. Compilação otimizada em `Release` para a arquitetura `win-x64`.
2. Geração da pasta com executável e bibliotecas em `OpenDot.Windows/dist/OpenDot-win-x64/`.
3. Compactação automática em `OpenDot.Windows/dist/OpenDot-Windows-v1.0.0-x64.zip`.
4. Compilação do instalador `OpenDot-Windows-Setup-1.0.0.exe` (caso o Inno Setup esteja instalado).

### Método Manual (via dotnet CLI):
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

## 6. Configuração Inicial e Chaves de API

Ao iniciar o Open Dot for Windows pela primeira vez:

1. Acesse o menu lateral **Settings**.
2. Insira suas credenciais:
   - **OpenAI API Key:** (opcional, para modelos GPT-4o, etc.).
   - **OpenRouter API Key:** (opcional, para modelos Claude, DeepSeek, Llama, etc.).
   - **Composio API Key:** (opcional, para integrações com Gmail, GitHub, Notion).
3. Selecione o provedor e modelo padrão.
4. Clique em **Save API Keys**.

> [!IMPORTANT]
> Todas as chaves são criptografadas com **AES-256-GCM** e seladas pela **DPAPI do Windows** antes de serem salvas em disco. Nenhum token é armazenado em texto claro.

---

## 7. Estrutura de Arquivos em Disco

Os dados da aplicação em tempo de execução são armazenados na pasta de dados locais do usuário:

- **Banco de Dados SQLite:** `%LOCALAPPDATA%\OpenDot\dots.db`
- **Cofre Criptográfico DPAPI:** `%LOCALAPPDATA%\OpenDot\vault.json`
- **Workspaces dos Agentes:** `%LOCALAPPDATA%\OpenDot\dots\<dotId>\workspace\`
- **Perfis de Navegador Isolados:** `%LOCALAPPDATA%\OpenDot\dots\<dotId>\browser\`
- **Logs da Aplicação:** `%LOCALAPPDATA%\OpenDot\logs\`
