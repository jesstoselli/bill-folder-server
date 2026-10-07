# BillFolder Web MVP — Design

**Data:** 2026-10-07  
**Status:** aprovado em conversa; aguardando revisão do documento  
**Repositórios relacionados:** `BillFolder` (API), `BillFolderApp` (Android) e futuro `BillFolderWeb` (Angular)

## 1. Objetivo

Criar uma aplicação web responsiva do BillFolder para uso cotidiano no laptop, preservando o backend e as regras financeiras existentes. A primeira entrega deve ser um MVP completo para a rotina da usuária atual, mas sua arquitetura deve aceitar a paridade funcional com o Android por expansão, sem reescrita estrutural.

O web será instalável como PWA, porém funcionará online. Suporte a lançamentos offline e sincronização posterior não fazem parte do produto nesta etapa.

## 2. Contexto e critérios de sucesso

O BillFolder já possui:

- API .NET 10 com autenticação JWT, refresh tokens e contratos para todas as áreas financeiras;
- PostgreSQL como fonte de verdade;
- app Android com os fluxos validados em uso real;
- domínio público da API em `api.billfolder.app`;
- identidade visual dark-first baseada no verde `#86BC65` e na fonte Barlow Semi Condensed.

O MVP estará pronto quando a usuária conseguir, apenas pelo navegador do laptop:

1. autenticar-se e recuperar sua conta;
2. consultar a situação financeira do ciclo;
3. registrar e alterar os lançamentos usados no dia a dia;
4. dar baixa em despesas, ocorrências recorrentes, receitas e faturas;
5. consultar cartões, poupança e ajustes;
6. instalar o web como PWA;
7. ver as telas relacionadas se atualizarem após uma mutação, sem logout ou recarga manual.

O MVP pode assumir que ciclos, contas, cartões e poupanças já foram configurados pelo Android. Tornar uma conta nova completamente autossuficiente no web pertence à etapa de paridade.

## 3. Abordagem escolhida

### 3.1 Aplicação Angular client-side

Será criado um repositório independente chamado `BillFolderWeb`. Ele usará Angular 22 com componentes standalone, rotas lazy-loaded e Signals para estado. A aplicação será uma SPA/PWA client-side e consumirá diretamente a API existente.

SSR não será usado: as telas são autenticadas, não dependem de SEO e não justificam outro runtime de renderização. Também não haverá BFF intermediário no MVP; a API existente continua sendo o único backend do produto.

### 3.2 Hospedagem

O frontend será publicado no Cloudflare com domínio `app.billfolder.app`. A configuração deve permitir:

- deploy automático da branch `main`;
- URL de preview para branches ou pull requests;
- HTTPS e domínio customizado;
- configuração distinta da URL da API para local, preview e produção;
- fallback de todas as rotas da SPA para `index.html`.

Cloudflare Pages é o alvo inicial por ser suficiente para assets estáticos. A aplicação não dependerá de APIs proprietárias do Pages, permitindo migrar os mesmos assets para Cloudflare Workers Static Assets caso a plataforma passe a recomendar essa modalidade durante a implementação.

## 4. Arquitetura do frontend

```text
src/app
├── core
│   ├── auth
│   ├── http
│   ├── layout
│   ├── navigation
│   ├── config
│   └── data-change
├── shared
│   ├── components
│   ├── dialogs
│   ├── forms
│   ├── formatters
│   ├── models
│   ├── states
│   └── styles
└── features
    ├── home
    ├── expenses
    ├── daily-expenses
    ├── income
    ├── cards
    ├── savings
    └── adjustments
```

### 4.1 Responsabilidades

- `core`: serviços singleton e infraestrutura usada pela aplicação inteira. Não contém regra visual de uma feature.
- `shared`: componentes e utilitários sem conhecimento de uma rota específica.
- `features`: cada domínio contém seus modelos de transporte, API client, store, componentes de página e formulários.

Cada feature seguirá o fluxo:

```text
API client → store/service com Signals → componentes
```

Os stores expõem estados discriminados de `loading`, `content` e `error`, além de ações nomeadas. Não haverá um store global contendo todos os dados, e NgRx não será adicionado ao MVP.

### 4.2 Contratos e tipos

Os DTOs TypeScript representarão explicitamente os contratos JSON da API. Dinheiro continuará sendo recebido como número no MVP para manter o contrato atual, mas cálculos financeiros relevantes permanecerão no backend; o frontend apenas formata valores e executa operações de apresentação claramente definidas.

Datas de domínio recebidas como `YYYY-MM-DD` serão mantidas como datas civis, sem conversão implícita para UTC. Timestamps de auditoria serão tratados separadamente como instantes.

## 5. Autenticação web segura

Os endpoints atuais permanecem inalterados para o Android. A API ganhará endpoints específicos do navegador:

- `POST /v1/auth/web/signup`
- `POST /v1/auth/web/login`
- `POST /v1/auth/web/refresh`
- `POST /v1/auth/web/logout`

Cadastro e login retornam o access token e os dados do usuário. O refresh token não será exposto ao JavaScript: ele será enviado como cookie host-only da API, `HttpOnly`, `Secure` e com política `SameSite` compatível com os subdomínios do BillFolder.

O access token ficará somente em memória. Na inicialização da aplicação, o Angular chama `web/refresh` com credenciais para restaurar uma sessão válida. Um interceptor adiciona o access token às chamadas autenticadas.

Ao receber `401`, o cliente pode executar uma única tentativa coordenada de refresh. Requisições concorrentes devem aguardar o mesmo refresh, sem disparar rotações paralelas do token. Se o refresh falhar, a sessão é limpa e o usuário volta ao login.

O backend deve:

- permitir credenciais apenas de origens configuradas;
- restringir CORS a `app.billfolder.app` em produção e às origens locais explicitamente cadastradas em desenvolvimento;
- validar a origem dos endpoints de cookie;
- manter rotação e revogação de refresh tokens;
- preservar o comportamento dos endpoints móveis existentes.

Forgot password e reset password podem reutilizar os endpoints atuais porque não dependem de cookie de sessão.

## 6. Escopo funcional do MVP

### 6.1 Autenticação

- cadastro público, com o mesmo comportamento atual do Android;
- login;
- sessão restaurada após reload;
- logout;
- solicitação de recuperação de senha;
- confirmação do código e definição da nova senha;
- guards para rotas públicas e autenticadas.

### 6.2 Home

- ciclo selecionado e navegação entre ciclos;
- saldo disponível e valor realizado;
- breakdown por categoria;
- abas `próximos`, `últimas` e `atrasadas`, incluindo badge numérico;
- valores provisionados remanescentes e valor mensal original quando aplicável;
- ações de baixa já suportadas pela Home no Android;
- atalhos para os principais lançamentos.

### 6.3 Despesas

- listar por ciclo e status;
- criar e editar despesa avulsa;
- criar recorrência semanal ou mensal;
- pagar despesa comum;
- dar baixa em uma ocorrência da despesa provisionada;
- reajustar valor por sessão com escopo;
- encerrar/excluir com escopo;
- preservar as regras do backend para valores pagos, reservados e recorrências.

### 6.4 Gastos diários

- listar por ciclo;
- criar, editar e excluir;
- usar as contas e categorias já cadastradas.

### 6.5 Receitas

- listar por ciclo;
- criar entrada avulsa;
- confirmar recebimento;
- editar e excluir entradas;
- exibir as fontes existentes como referência, sem administrar fontes no MVP.

### 6.6 Cartões

- selecionar cartão;
- navegar somente entre faturas válidas;
- distinguir fatura aberta, fechada e paga pela regra do backend;
- criar compra avulsa e assinatura mensal;
- pagar fatura fechada, com data, valor e conta;
- reajustar assinatura com escopo;
- encerrar/excluir compra ou assinatura conforme o contrato existente.

### 6.7 Poupança

- selecionar conta;
- exibir saldo corrente total;
- exibir movimento líquido do ciclo;
- criar aporte ou saque;
- editar e excluir transações.

### 6.8 Ajustes

- listar ajustes do ciclo;
- criar, editar e excluir;
- manter a categoria sintética `Ajustes` no breakdown retornado pela Home.

## 7. Fora do MVP e continuidade de paridade

Entram na etapa seguinte, usando a mesma arquitetura:

- criação e administração de ciclos;
- administração de contas bancárias;
- administração de cartões;
- administração de contas de poupança;
- administração de fontes recorrentes de renda;
- onboarding autossuficiente para contas novas;
- preferências do usuário;
- modo claro;
- operação offline e resolução de conflitos;
- notificações push;
- empacotamento nativo para macOS.

O futuro app para Mac deve reutilizar o build web em um shell leve, preferencialmente Tauri. O frontend não deve depender de APIs exclusivas do navegador sem uma abstração pequena quando isso comprometer esse empacotamento.

## 8. Experiência visual e responsividade

A direção aprovada é **Painel focado**.

### 8.1 Identidade

- dark-first;
- Barlow Semi Condensed;
- verde `#86BC65` como assinatura;
- números monetários tabulares;
- pílulas para controles e tabs;
- cards comuns com raio moderado;
- raio maior e hachura de cédula apenas no saldo principal;
- cores de status sempre acompanhadas por texto ou ícone.

Angular Material/CDK fornecerá acessibilidade, overlays, formulários e primitivas de interação. A camada visual será tematizada com tokens próprios do BillFolder e não usará a aparência padrão do Material.

### 8.2 Shell

- `>= 1200px`: sidebar completa e conteúdo capaz de usar duas colunas;
- `768–1199px`: barra lateral compacta e conteúdo adaptado;
- `< 768px`: drawer e layout de uma coluna.

A Home usa saldo e lançamentos como área dominante; breakdown e atalhos são secundários. Telas internas usam listas ou tabelas mais densas. A experiência não depende de swipe: ações ficam em botões, menus contextuais e clique de linha com indicação visível.

### 8.3 Formulários

- formulário curto: dialog central;
- formulário longo: painel lateral à direita;
- largura pequena: experiência em tela cheia;
- pagamento: dialog com resumo da obrigação antes da confirmação;
- ações destrutivas e escopo de recorrência: dialog compacto.

Todo fluxo deve funcionar por teclado, ter foco visível, labels programáticos, mensagens anunciáveis e alvos de interação adequados.

## 9. Consistência de dados

O web terá um `DataChangeService` equivalente ao bus do Android. Após qualquer write bem-sucedido:

1. o repositório/store publica uma nova versão;
2. stores consumidores observam a mudança;
3. dados afetados são recarregados em segundo plano;
4. o conteúdo atual permanece visível durante a atualização.

O bus é para mutações feitas naquela instância web. Alterações realizadas no Android ou externamente são recuperadas por refresh manual, troca de ciclo, retomada da aba após período significativo ou novo carregamento da rota. Não haverá polling contínuo no MVP.

## 10. Erros e estados

- carregamentos iniciais exibem skeletons compatíveis com a composição final;
- refresh mantém conteúdo visível e mostra progresso discreto;
- lista vazia explica o estado e aponta uma ação possível;
- erro de leitura oferece `tentar novamente`;
- erro de write preserva formulário e dados digitados;
- erros de validação da API são associados ao campo quando houver correspondência;
- erros inesperados usam mensagem geral e registram contexto técnico sem dados financeiros sensíveis;
- uma falha de refresh de sessão encerra a sessão de forma previsível.

## 11. Estratégia de testes

### 11.1 Frontend

- testes unitários para formatadores de moeda/data, projeções de tela e regras de habilitação;
- testes de stores com API clients falsos para `loading/content/error`, mutações e invalidação;
- testes de componentes para formulários, acessibilidade básica e estados vazios/erro;
- testes de contrato para desserialização dos DTOs mais críticos;
- Playwright para login, recuperação de sessão, lançamento de despesa, baixa semanal, pagamento de fatura e atualização da Home.

### 11.2 Backend

- testes dos endpoints web de autenticação e atributos do cookie;
- CORS permitido para origem configurada e negado para origem desconhecida;
- refresh concorrente/rotacionado sem reutilização indevida;
- regressão dos endpoints Android existentes.

## 12. Segurança e operação

- nenhuma credencial ou segredo será compilado no Angular;
- somente URLs e flags públicas entram na configuração de build;
- headers de segurança, incluindo CSP compatível com os assets reais, serão configurados na hospedagem;
- logs não registram access token, refresh token, senha nem payload financeiro completo;
- dependências serão mantidas mínimas e auditadas no CI;
- produção usa `app.billfolder.app` e `api.billfolder.app` exclusivamente por HTTPS.

## 13. Sequência de entrega

1. **Fundação web:** repositório, Angular, CI, tema, shell, ambientes e PWA.
2. **Sessão segura:** backend web auth, CORS, frontend auth e recuperação de senha.
3. **Home:** contratos, stores, layout Painel focado e sincronização global.
4. **Lançamentos centrais:** despesas, gastos diários e receitas.
5. **Cartões:** faturas, compras, assinaturas e pagamento.
6. **Poupança e ajustes:** saldo, transações e ajustes de ciclo.
7. **E2E, acessibilidade e deploy:** fluxos críticos, headers, preview e produção.
8. **Paridade:** módulos administrativos e onboarding, em plano separado imediatamente após o MVP.

Cada etapa deve terminar com uma aplicação compilável, testes verdes e um commit revisável. O deploy de produção não será feito automaticamente pela implementação sem autorização explícita da usuária.

## 14. Referências técnicas

- [Angular — version compatibility](https://angular.dev/reference/versions)
- [Angular — releases](https://angular.dev/reference/releases)
- [Cloudflare — deploy an Angular site](https://developers.cloudflare.com/pages/framework-guides/deploy-an-angular-site/)
- [Cloudflare Pages — Git integration](https://developers.cloudflare.com/pages/configuration/git-integration/)

