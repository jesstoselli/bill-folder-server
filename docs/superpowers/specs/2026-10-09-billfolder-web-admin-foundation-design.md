# BillFolder Web — Fundação administrativa: Ciclos e Contas-correntes

**Data:** 2026-10-09  
**Status:** implementado e verificado localmente; aguardando autorização de commit/push
**Clientes afetados:** Web e API  
**Fora do escopo:** CRUD de cartões, poupanças e fontes de renda; onboarding; preferências; tema claro; Tauri

## 1. Objetivo

Dar autonomia ao cliente web para administrar os dois cadastros que sustentam os demais fluxos
financeiros:

- ciclos, que delimitam os períodos exibidos na Home e nas telas financeiras;
- contas-correntes, usadas como referência em pagamentos, despesas avulsas, poupança e saldos.

A entrega também atualiza a documentação de rollout, elimina o baseline pendente de formatação e
registra o estado real da paridade. Ela deve preservar a direção visual “Painel focado”, reutilizar
os componentes compartilhados existentes e preparar a navegação administrativa para os próximos
cadastros sem criar um framework genérico prematuro.

## 2. Estado atual confirmado

### 2.1 Ciclos

A API já oferece listagem, consulta, criação, atualização, exclusão e backfill. O web atualmente
consome somente listagem e ciclo atual por meio de `CyclesApi` e `CycleStore`.

Regras efetivas da API:

- `startDate` e `endDate` são datas civis e `startDate` deve ser anterior a `endDate`;
- `label` é obrigatório e limitado a 100 caracteres;
- a unicidade garantida é `(userId, startDate)`;
- a API não rejeita intervalos sobrepostos;
- criar manualmente um ciclo também materializa recorrências e gera a janela futura automática;
- excluir um ciclo remove somente o registro do período. Lançamentos são associados por data, não
  por chave estrangeira ao ciclo;
- excluir o ciclo atual pode deixar o app temporariamente sem período que cubra hoje;
- o endpoint de backfill é técnico, idempotente e já é complementado pelo safety-net de
  `GetCurrentAsync`.

### 2.2 Contas-correntes

A API já oferece CRUD completo e garante no máximo uma conta principal por usuário. O web usa a
listagem como dado de referência em formulários, com cache ordenado de cinco minutos.

Regras efetivas da API:

- banco, agência e número são obrigatórios na criação;
- saldo inicial deve ser maior ou igual a zero;
- marcar uma conta como principal desmarca as demais;
- não há obrigação de existir ao menos uma conta principal;
- a exclusão atual pode apagar em cascata a poupança e as despesas avulsas vinculadas;
- recorrências de despesas avulsas usam `Restrict`, enquanto referências de pagamento de despesas
  e faturas usam `SetNull`.

A exclusão atual não atende ao requisito de ausência de perda silenciosa. A UI administrativa só
será exposta junto com uma proteção de domínio no backend.

## 3. Arquitetura escolhida

Serão criadas duas páginas independentes e lazy-loaded:

- `/gerenciar/ciclos`;
- `/gerenciar/contas`.

A sidebar ganhará a seção “Gerenciar”, separada da navegação operacional. Essa estrutura permite
adicionar cartões, poupanças e fontes de renda depois sem transformar cada tela financeira em uma
mistura de operação diária e cadastro.

Alternativas rejeitadas:

- uma única página de configurações com abas: economiza espaço na sidebar, mas acopla domínios com
  regras e ciclos de vida diferentes;
- CRUD embutido nas telas financeiras: reduz a descoberta, espalha regras administrativas e
  dificulta a expansão da paridade.

Não será criado um `CrudStore<T>` ou formulário genérico. APIs, stores e formulários continuam
específicos por domínio. O compartilhamento ocorrerá apenas onde a semântica é realmente comum:
componentes visuais existentes, diálogo de confirmação destrutiva e estilos de listagem.

## 4. Experiência e componentes

### 4.1 Navegação

A sidebar passa a renderizar grupos de destinos. O grupo principal mantém as telas atuais e o grupo
“Gerenciar” contém Ciclos e Contas-correntes. Em modo rail, os títulos dos grupos ficam
visualmente ocultos, e os tooltips continuam identificando cada destino.

Os novos destinos usam o `IconComponent`, com ícones coerentes com o Android e com os demais itens
da sidebar.

### 4.2 Estrutura das páginas

As páginas reutilizam:

- `PageHeaderComponent` para título e ações;
- `ButtonComponent` para Atualizar e Novo;
- `PageStateComponent` para loading, erro e vazio;
- `RefreshStatusComponent` para refresh não destrutivo;
- `InlineAlertComponent` para falhas de ações;
- `bf-ledger` para tabela no desktop e cartões no mobile;
- `DialogFrameComponent` e `WriteDialogLock` nos formulários;
- `MoneyComponent`, formatadores civis e `IconComponent` quando aplicável.

Será criado um diálogo compartilhado de confirmação destrutiva. Ele recebe título, mensagem e
rótulos, devolve a escolha confirmada/cancelada e não conhece ciclos nem contas. Regras de negócio,
estado do write e textos específicos continuam nas páginas de domínio.

### 4.3 Ciclos

A lista mostra nome, intervalo, origem e ações. Os estados “Atual” e “Gerado automaticamente” são
indicados por texto/badge, sem depender apenas de cor.

O formulário de criação/edição contém nome, data inicial e data final. A criação usa como default o
mês civil atual, igual ao Android; duplicidade de início continua sendo tratada pela API. A edição
envia o contrato parcial existente.

Todos os ciclos podem ser editados, inclusive os gerados automaticamente, porque esse é o contrato
vigente. A exclusão exige confirmação. Para o ciclo atual, o texto destaca que as telas podem ficar
sem período ativo; para os demais, explica que o período sai da navegação, mas lançamentos datados
são preservados.

O backfill não será exposto nesta entrega. Ele é uma operação de manutenção e não agrega valor ao
fluxo cotidiano enquanto a geração automática e o safety-net estiverem ativos.

### 4.4 Contas-correntes

A lista mostra banco, agência/conta, saldo inicial, indicador de conta principal e ações. “Saldo
inicial” permanece explícito para não sugerir um saldo corrente que o contrato não calcula.

O formulário de criação/edição contém banco, agência, número, saldo inicial e controle de conta
principal. A UI informa que marcar uma conta como principal substitui a principal anterior.

A exclusão exige confirmação. O backend bloqueará a operação quando a conta estiver ligada a:

- uma conta-poupança;
- despesas avulsas;
- uma recorrência de despesas avulsas.

Nesses casos a API retorna `409 account_in_use` com mensagem acionável, e nenhum dado é removido.
Referências históricas configuradas como `SetNull` continuam preservando os lançamentos quando uma
conta sem dependências destrutivas é excluída.

## 5. Dados, invalidação e consistência

Cada módulo terá API e store próprios. Writes usam `notifyingWrite`, preservando o barramento global
de mudanças.

Após writes de ciclos:

1. a lista administrativa é atualizada;
2. o `CycleStore` é recarregado;
3. a seleção atual é preservada quando o ciclo ainda existe;
4. se o ciclo selecionado foi removido, o store volta ao ciclo atual retornado pelo backend;
5. consumidores ativos recebem o bump global e atualizam seus dados.

Após writes de contas-correntes:

1. a lista administrativa é atualizada;
2. o cache de contas em `ReferenceDataStore` é invalidado explicitamente;
3. diálogos abertos depois da alteração recebem a nova lista;
4. o bump global atualiza Home e demais telas interessadas.

O refresh mantém conteúdo já carregado visível. Falhas de atualização aparecem como estado de
refresh, enquanto falhas iniciais usam o estado de página. Falhas de create/update/delete permanecem
no contexto da ação, sem substituir a lista inteira por uma tela de erro.

## 6. Proteção de exclusão no backend

`CheckingAccountsService.DeleteAsync` fará uma verificação transacional antes da remoção. A conta é
considerada em uso quando qualquer dependência destrutiva ou restritiva existir. O endpoint mapeia
`account_in_use` para HTTP 409.

Essa mudança também protege o Android: uma exclusão perigosa deixa de causar cascade ou erro de
constraint e passa a receber um conflito previsível. Não há migração de banco.

O backend de ciclos não muda nesta entrega. A exclusão é reversível apenas recriando o período, mas
não apaga os lançamentos financeiros associados por data.

## 7. Acessibilidade e responsividade

- Todos os fluxos funcionam por teclado e mantêm foco visível.
- Dialogs devolvem foco à ação que os abriu.
- Durante writes, Fechar, Cancelar, Escape e submissão repetida ficam bloqueados.
- A tabela se transforma no padrão de cartões do `bf-ledger` em telas pequenas.
- Badges têm rótulos textuais; cor é apenas reforço visual.
- Mensagens de erro descrevem o próximo passo possível.
- A direção visual permanece dark-first, tipográfica e contida, sem introduzir um segundo sistema
  de cards ou novos tokens desnecessários.

## 8. Documentação e qualidade de base

Antes da entrega funcional:

- os cinco arquivos apontados pelo Prettier serão formatados;
- serão adicionados scripts `format` e `format:check`, e o check fará parte do gate de CI do web;
- o README registrará o Worker, o domínio `preview.billfolder.app`, o build automático, o CORS e o
  estado real do rollout;
- o handoff registrará os heads, verificações e próximos módulos reais;
- o backlog marcará Ciclos e Contas-correntes como entregues ao final; Cartões, Poupança e Fontes de
  renda ficarão como “infraestrutura pronta, UI administrativa pendente”.

Números de testes e bundles serão atualizados somente após a verificação final, nunca copiados de
uma execução anterior.

## 9. Estratégia de testes

### Backend

- RED/GREEN para a conta sem vínculos ser excluída;
- RED/GREEN para cada dependência destrutiva/restritiva produzir `account_in_use` sem remoção;
- teste do mapeamento HTTP 409;
- suíte completa e build sem warnings.

### Web

- APIs: métodos, URLs, bodies, notificação somente em sucesso e mapeamento de conflitos;
- stores: load, refresh preservando conteúdo, create/update/delete, rollback e invalidação;
- formulários: defaults, prefill, validação, payload e lock durante write;
- páginas: estados loading/error/empty/content, confirmação, mensagens específicas e retorno de
  foco;
- sidebar e rotas: grupos, active route, rail e lazy loading;
- cache de referências: invalidação após writes de conta;
- regressão E2E dos fluxos críticos de criação/edição/exclusão segura e consumo da conta atualizada
  em um seletor de pagamento;
- `format:check`, testes unitários/estáticos, builds production/preview e checks de artefatos.

## 10. Critérios de conclusão

A entrega está concluída quando:

- Ciclos e Contas-correntes são administráveis no web em desktop e mobile;
- alterações de ciclo refletem nas telas dependentes sem reload manual;
- alterações de conta refletem nos seletores e saldos consumidores;
- nenhuma exclusão de conta apaga poupança ou despesas silenciosamente;
- os fluxos destrutivos exigem confirmação e preservam contexto em erro;
- documentação e backlog refletem o estado publicado real;
- todos os gates automatizados relevantes estão verdes;
- nenhum push, deploy ou publicação adicional é feito sem autorização explícita.
