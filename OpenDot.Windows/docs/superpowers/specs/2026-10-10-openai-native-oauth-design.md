# Login ChatGPT/Codex nativo no OpenDot Windows

Status: desenho em conversa aprovado; especificação escrita aguardando revisão. Nenhum código de produto foi alterado para esta funcionalidade.

## Objetivo e restrições aprovadas

Adicionar login com conta ChatGPT para executar os agentes do OpenDot usando o acesso ao Codex disponível nessa conta, sem colar uma chave de API. Manter OpenAI API e OpenRouter como alternativas explicitamente selecionadas.

- Implementação em C#/.NET 8 e interface WinUI 3, compatível com Windows 10 x64 22H2 e Windows 11.
- Nenhuma dependência nova de Codex CLI, Node.js, Electron, WebView ou executável auxiliar.
- O navegador padrão é usado somente para a autenticação externa; a interface e a execução dos agentes continuam nativas.
- Reutilizar o cofre Windows existente; não importar credenciais de outros aplicativos nem ler `~/.codex/auth.json`.
- Não executar a suíte de testes, conforme a orientação anterior do usuário. A verificação planejada é compilação/publicação e observação do fluxo real, sem pedir senhas ou tokens no chat.
- Não alterar os provedores existentes, a automação ou as políticas de aprovação além das adaptações necessárias ao novo provedor.

## Evidência no repositório

- `OpenDot.App/Services/AppServices.cs` registra `OpenAiClient` e `OpenRouterClient`; o resolvedor escolhe OpenRouter pelo prefixo `openrouter:` e OpenAI nos demais casos.
- `OpenDot.App/ViewModels/SettingsViewModel.cs` guarda chaves usando `IDotRepository.GetSetting/SetSetting` e `ISecureVault.Seal/Unseal`.
- `OpenDot.App/Views/SettingsPage.xaml` oferece campos de chave e de modelo padrão; não há login de conta.
- `OpenDot.Core/Agents/AiModels.cs` define `IAiClient`, mensagens, chamadas de ferramentas e as requisições/respostas do agente.
- `OpenDot.Core/Agents/AgentRuntime.cs` reconstrói o histórico a cada etapa, guarda `PreviousResponseId` e mantém o estado de chamadas pendentes entre aprovações.
- `default_model` é salvo nas configurações, mas os dois caminhos de requisição do runtime usam `dot.Model ?? "gpt-4o"`. O novo seletor precisa alcançar esses caminhos; adicionar apenas um botão de login seria insuficiente.
- `OpenDot.App/Views/NewDotDialog.xaml.cs` cria Dots sem definir modelo. A escolha padrão precisa funcionar também para esses Dots.

## Limite externo: compatibilidade não significa autorização

O código público do Codex fornece um protocolo de referência, não uma garantia de acesso ao backend por qualquer aplicativo de terceiros. A documentação oficial apresenta o App Server como interface de integração, mas o requisito aprovado exclui esse componente.

O desenho direto utiliza o protocolo OAuth do Codex e seu cliente público `app_EMoamEEZ73f0CkXaXp7hrann`, sujeito à aceitação e às políticas da OpenAI. Esse identificador não é um segredo nem um registro próprio do OpenDot. O OpenDot deve se identificar como OpenDot, sem declarar ser um cliente oficial. Se o serviço exigir um registro próprio ou rejeitar o cliente, informar a limitação; não contornar a rejeição, extrair cookies ou apresentar uma sessão fictícia.

A aceitação de login e inferência em uma conta real permanece não verificada. Ela exige participação do usuário no navegador e acesso elegível ao Codex. Implementação e compilação sozinhas não comprovam esse acesso.

## Arquitetura

### 1. Autenticação e sessão

Adicionar `ChatGptOAuthService` em `OpenDot.Integrations`, com uma interface de autenticação em `OpenDot.Core/Interfaces` consumida pela interface nativa. O serviço é singleton para evitar renovações concorrentes do mesmo refresh token.

Responsabilidades:

- Criar autorização, receber callback, trocar o código, armazenar a sessão, renovar tokens e desconectar.
- Expor à UI somente estado, identificação da conta e erros seguros; nunca access token, refresh token ou ID token.
- Manter uma única operação de login ativa, cancelável pelo usuário.
- Serializar renovação e alteração de conta. Uma conclusão de login/renovação iniciada antes de desconectar não pode restaurar a sessão removida.
- Guardar a sessão inteira como um único valor selado `chatgpt_oauth_session` no armazenamento de configurações. Falha ao salvar significa que o login não foi concluído.
- Não modificar `openai_key` ao conectar, renovar ou desconectar.

### 2. OAuth no navegador padrão

1. Gerar `state`, `nonce` e PKCE S256 usando gerador criptográfico do .NET.
2. Abrir um listener TCP exclusivamente em `127.0.0.1`, antes de abrir o navegador. Usar o callback `/auth/callback` na porta 1455; tentar 1457 quando a primeira estiver ocupada, seguindo as portas encontradas no upstream. Não encerrar processos que já ocupam essas portas.
3. Abrir `https://auth.openai.com/oauth/authorize` no navegador padrão com `response_type=code`, identificador público do cliente, redirect URI exata, PKCE e `state`. Solicitar somente os escopos de identidade e sessão necessários: `openid profile email offline_access`.
4. Aceitar somente o callback esperado, com tamanho limitado, sem parâmetros duplicados, com `state` correspondente. Não tratar requisições estranhas ao callback como sucesso ou como autorização para trocar um código.
5. Trocar o código em `https://auth.openai.com/oauth/token`, com `grant_type=authorization_code`, `code_verifier` e a mesma redirect URI. Não incluir client secret em aplicativo desktop.
6. Validar a identidade retornada por OIDC antes de apresentá-la como verificada: assinatura via JWKS do emissor, emissor, audiência, validade e nonce. Usar apenas metadados do emissor OpenAI por HTTPS.
7. Persistir a sessão selada e encerrar o listener. Cancelamento, falha, saída da página ou prazo de três minutos também encerram o listener e descartam o verificador.

O listener será implementado com APIs .NET, sem reserva HTTP.sys, alteração de firewall ou solicitação de administrador. A página de retorno conterá apenas confirmação/erro estático, sem tokens ou URLs sensíveis.

### 3. Renovação e desconexão

- Renovar pelo mesmo endpoint de token antes da expiração, com margem de cinco minutos e `grant_type=refresh_token`.
- Persistir os tokens rotacionados juntos; preservar o refresh token anterior somente quando a resposta válida não retornar um novo.
- Erros transitórios de rede não removem a sessão; apresentar a falha sem trocar de provedor.
- Rejeição definitiva do refresh token exige novo login e não entra em loop de renovação.
- Desconectar remove a sessão local, invalida operações concorrentes e cancela as requisições ativas desse provedor. A UI não promete revogação global da conta ChatGPT nem desconecta outros aplicativos.
- Requisições com credenciais não seguem redirecionamentos para outros hosts.

### 4. Cliente ChatGPT/Codex

Adicionar `ChatGptCodexClient` em `OpenDot.Integrations`, adaptando o protocolo Responses para `IAiClient`.

- Base do backend: `https://chatgpt.com/backend-api/codex`.
- Modelos: catálogo autenticado em `/models`, com versão de compatibilidade do protocolo explicitamente definida e identificação do OpenDot separada. Não inventar uma lista de modelos quando o catálogo falhar.
- Inferência: `/responses`, HTTP streaming/SSE, com access token e identificação da conta conforme o protocolo upstream, `store=false` e `stream=true`.
- Converter instruções, mensagens, ferramentas e resultados de ferramentas para o formato Responses; não enviar o payload Chat Completions existente para esse endpoint.
- Processar texto incremental, chamadas de função, conclusão, recusa, resposta incompleta e erro de stream. Fim de conexão sem evento terminal é falha, não resposta concluída.
- Agregar argumentos de ferramentas por item/chamada e preservar a ordem; não executar uma ferramenta antes de receber os argumentos completos.
- Propagar cancelamento até a conexão HTTP e liberar stream e resposta.
- Não converter tokens em chave de API e não usar a troca opcional upstream por `openai-api-key`.
- Não reenviar automaticamente uma geração que já produziu texto ou ferramentas. Erros de cota, acesso e modelo devem aparecer claramente ao usuário.

### 5. Continuação do agente e ferramentas

Como as respostas usam `store=false`, um ID de resposta sozinho não é contrato suficiente para continuar uma chamada de ferramenta.

Estender as requisições/respostas do agente com um estado opcional de continuação pertencente ao provedor. Para ChatGPT/Codex, esse estado conserva os itens de saída necessários, inclusive chamadas de função e itens opacos de raciocínio retornados pelo serviço, junto com os resultados correspondentes. O runtime guarda esse estado na execução pendente, inclusive durante pausas de aprovação, e o repassa na etapa seguinte.

- Nenhum estado global de conversa no cliente singleton: execuções simultâneas de Dots não podem compartilhar continuação.
- Uma nova execução ou mudança de conta não reaproveita a continuação de outra execução/conta.
- Manter os fluxos atuais de aprovação, cancelamento e execução de ferramentas do OpenDot. OAuth não dá permissão adicional para executar ações.
- OpenAI API e OpenRouter continuam com seu comportamento atual quando o novo campo opcional está ausente.
- O trabalho não inclui redesenhar a persistência de retomada após encerrar o aplicativo.

### 6. Escolha explícita do provedor

Usar `chatgpt:<model-id>` no campo de modelo persistido, seguindo a convenção existente de `openrouter:<model-id>`.

- Resolver o modelo efetivo como modelo do Dot, depois `default_model`, depois o padrão legado `gpt-4o`.
- Aplicar a mesma resolução ao provedor e ao payload, inclusive na comunicação entre Dots.
- Dots com modelo explicitamente configurado mantêm esse modelo.
- Login não muda automaticamente os modelos existentes nem o padrão. O usuário seleciona ChatGPT/Codex e um modelo disponível para começar a usá-lo.
- Adicionar seleção nativa nas configurações e na configuração de cada Dot; preservar identificadores existentes e mostrar o estado desconectado quando apropriado.
- Dots sem modelo explícito seguem a preferência padrão. Rotinas que usam esses Dots seguem a mesma resolução.
- Um Dot configurado para ChatGPT/Codex permanece nesse provedor mesmo quando existe `OPENAI_API_KEY`; falha de login nunca implica cobrança pela API.

### 7. Interface WinUI

Adicionar uma seção de conta ChatGPT/Codex separada dos campos de chaves:

- Estados: desconectado, aguardando login, conectado e novo login necessário.
- Ações: entrar, cancelar login e desconectar.
- Mostrar identidade da conta após validação e um seletor de modelos carregado do serviço.
- Indicar que o uso segue o acesso e os limites da conta ChatGPT, sem prometer acesso a todos os modelos ou uso ilimitado.
- Desabilitar operações conflitantes e exibir erros sem incluir dados de credenciais.
- Atualizar os controles via dispatcher da UI e cancelar/desvincular operações da página ao sair dela.
- Reutilizar os componentes WinUI e o estilo existentes; não redesenhar o restante das configurações.

## Arquivos e limites de responsabilidade

| Área | Arquivos previstos |
| --- | --- |
| Contrato de autenticação e estado público | Novos arquivos em `src/OpenDot.Core/Interfaces` e `src/OpenDot.Core/Models` |
| OAuth, listener e armazenamento selado | Novos arquivos em `src/OpenDot.Integrations/OpenAI` |
| Adaptador Responses/SSE | Novo `src/OpenDot.Integrations/OpenAI/ChatGptCodexClient.cs` e auxiliares focados no protocolo |
| Continuação e modelo efetivo | `src/OpenDot.Core/Agents/AiModels.cs`, `AgentRuntime.cs` e o estado pendente existente |
| Registro e seleção de provedor | `src/OpenDot.App/Services/AppServices.cs` |
| Conta e escolha de modelo | `src/OpenDot.App/ViewModels/SettingsViewModel.cs`, `Views/SettingsPage.xaml`, `Views/SettingsPage.xaml.cs` |
| Escolha por Dot | `src/OpenDot.App/Views/DotPage.xaml`, `DotPage.xaml.cs` e `ViewModels/DotViewModel.cs`, conforme os bindings existentes |
| Orientação de uso | `README.md` e documentação existente do aplicativo Windows |

Os caminhos são relativos a `OpenDot.Windows`. Não mover o `OpenAiClient` existente de `OpenDot.Core/Agents` apenas para corrigir a localização do arquivo; isso não é requisito da funcionalidade.

## Critérios de aceitação e verificação

1. Publicação self-contained win-x64 sem runtime auxiliar novo e sem exigir administrador no login.
2. Botão nativo abre o navegador e permite cancelar; porta ocupada e recusa no navegador geram mensagens úteis.
3. Callback estranho, `state` inválido, identidade inválida e sessão não persistida nunca geram estado conectado.
4. Reiniciar o aplicativo recupera a sessão pelo cofre Windows sem expor tokens em logs ou UI.
5. Refresh e desconexão concorrentes não restauram uma sessão removida nem sobrescrevem uma conta nova.
6. Selecionar ChatGPT/Codex alcança a inferência real pelo provedor correspondente, sem chave de API.
7. Resposta em streaming, ferramenta seguida de resultado e pausa/retomada de aprovação preservam a conversa e o isolamento entre Dots.
8. API por chave e OpenRouter continuam selecionáveis; erro OAuth nunca muda de provedor silenciosamente.
9. Não executar a suíte de testes. Compilar e observar a superfície WinUI e o callback real. Autenticação e geração ao vivo exigem que o usuário complete o consentimento no navegador; reportar separadamente tudo que não puder ser observado.
10. Não declarar suporte de conta ou inferência como comprovado apenas com compilação, simulações ou leitura do código upstream.

## Fontes de protocolo

Consultadas em 2026-10-10; os links `main` são mutáveis e a implementação deverá registrar a revisão de compatibilidade utilizada.

- [Autenticação do Codex](https://developers.openai.com/codex/auth).
- [App Server: alternativa oficial não selecionada](https://developers.openai.com/codex/app-server).
- [Servidor OAuth e callback do Codex](https://github.com/openai/codex/blob/main/codex-rs/login/src/server.rs).
- [Cliente público e renovação de tokens](https://github.com/openai/codex/blob/main/codex-rs/login/src/auth/manager.rs).
- [Base do backend ChatGPT/Codex](https://github.com/openai/codex/blob/main/codex-rs/model-provider-info/src/lib.rs).
- [Cliente de modelos](https://github.com/openai/codex/blob/main/codex-rs/codex-api/src/endpoint/models.rs).
- [Cliente de inferência e requisições sem armazenamento](https://github.com/openai/codex/blob/main/codex-rs/core/src/client.rs).
