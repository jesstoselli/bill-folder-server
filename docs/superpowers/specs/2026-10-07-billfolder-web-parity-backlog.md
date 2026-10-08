# BillFolder Web — Backlog delimitado de paridade

**Data:** 2026-10-08  
**Status:** semente de design aprovada para priorização futura; não é plano de implementação  
**Base:** BillFolder Web MVP verificado localmente

## 1. Objetivo e limite

Completar a autonomia funcional do cliente web sobre a fundação do MVP, sem reabrir os fluxos já
entregues de autenticação, Home, despesas, gastos diários, receitas, cartões/faturas, poupança e
ajustes. Cada item abaixo é um módulo de paridade delimitado por resultado observável, dependências e
critérios de aceitação; decomposição técnica, estimativas e commits pertencem a planos futuros
separados.

Operação offline com sincronização/conflitos e notificações push **não são paridade implícita**. São
decisões de produto independentes, cada uma exigindo design, riscos e autorização próprios.

## 2. Sequência por dependência

1. **Fundação administrativa:** ciclos e contas-correntes.
2. **Cadastros financeiros:** administração de contas de cartão, administração de contas de
   poupança e fontes de renda.
3. **Conta autossuficiente:** onboarding, apoiado por todos os cadastros anteriores.
4. **Personalização:** preferências e tema claro.
5. **Empacotamento opcional:** shell Tauri, somente após decisão explícita de produto e estabilização
   da paridade web.

Essa ordem expressa dependências de produto. Não obriga que módulos do mesmo grupo sejam executados
no mesmo lote ou em paralelo.

## 3. Módulos de paridade

### 3.1 Ciclos

**Resultado:** a pessoa pode administrar os períodos financeiros que hoje precisam existir antes do
uso cotidiano do web.

**Critérios de aceitação:**

- listar, criar, editar e excluir ciclos usando os contratos existentes;
- validar datas civis e impedir intervalos inválidos ou sobrepostos conforme as regras da API;
- identificar o ciclo atual e refletir mudanças na Home e nas telas dependentes sem reload manual;
- proteger exclusões com confirmação e explicar conflitos causados por dados vinculados;
- tratar o backfill de janela como operação administrativa explícita e idempotente, caso seja exposto.

**Dependências:** contrato atual de ciclos e política de exclusão/backfill confirmada com o backend.

### 3.2 Contas-correntes

**Resultado:** contas usadas para saldo, recebimentos e pagamentos podem ser administradas no web.

**Critérios de aceitação:**

- listar, criar, editar e excluir conta com banco, agência, número, saldo inicial e indicador de
  conta principal;
- manter no máximo uma conta principal segundo a regra efetiva do backend;
- atualizar seletores de pagamento e saldos consumidores após qualquer mudança;
- exibir conflitos de exclusão quando a conta tiver lançamentos vinculados, sem perda silenciosa.

**Dependências:** regras de conta principal e de exclusão do backend; precede poupança e onboarding.

### 3.3 Administração de contas de cartão

**Resultado:** cartões deixam de depender do Android para cadastro e manutenção.

**Critérios de aceitação:**

- listar, criar, editar e excluir cartão com nome, banco emissor, bandeira, dia de fechamento e dia de
  vencimento;
- validar dias e preservar a regra de enquadramento das compras em faturas do backend;
- atualizar imediatamente o seletor da tela de Cartões após mudanças;
- avisar e bloquear destruição quando faturas/compras vinculadas impedirem a operação.

**Dependências:** contrato de contas de cartão; contas-correntes disponíveis para os fluxos de
pagamento usados na validação integrada.

### 3.4 Administração de contas de poupança

**Resultado:** contas-poupança podem ser criadas e mantidas antes do registro de movimentos.

**Critérios de aceitação:**

- listar, criar, editar e excluir conta-poupança vinculada a uma conta-corrente;
- editar banco, agência, número e saldo inicial sem confundir saldo inicial com saldo corrente
  calculado;
- atualizar seletor, saldo corrente e movimentos da tela de Poupança após alterações;
- preservar e explicar restrições de exclusão quando houver transações vinculadas.

**Dependências:** contas-correntes; contrato de saldo corrente do backend.

### 3.5 Fontes de renda

**Resultado:** origens recorrentes de receita podem ser administradas no web, não apenas consultadas.

**Critérios de aceitação:**

- listar, criar, editar, ativar/desativar e excluir fontes com origem, tipo, valor padrão, dia esperado
  e vigência;
- mostrar claramente o efeito da fonte sobre entradas materializadas, sem reescrever histórico
  fechado;
- refletir mudanças nas telas de Receitas e Home por meio do barramento de mudanças;
- exigir confirmação de escopo quando a regra de recorrência afetar ocorrências futuras.

**Dependências:** ciclos; confirmação das regras de materialização e encerramento do backend.

### 3.6 Onboarding

**Resultado:** uma conta nova consegue chegar ao primeiro ciclo utilizável apenas pelo navegador.

**Critérios de aceitação:**

- detectar configuração incompleta e conduzir uma sequência retomável;
- coletar o mínimo necessário: conta-corrente principal, ciclo inicial e, opcionalmente, cartões,
  poupança e fontes de renda;
- permitir pular módulos opcionais sem bloquear o uso do MVP;
- concluir em uma Home válida e oferecer retomada segura após interrupção ou erro;
- não duplicar cadastros quando uma etapa for repetida.

**Dependências:** ciclos, contas-correntes e os cadastros financeiros; decisão sobre o estado de
progresso do onboarding (derivado ou persistido).

### 3.7 Preferências

**Resultado:** configurações pessoais suportadas pelo produto podem ser consultadas e alteradas no
web com persistência definida.

**Critérios de aceitação:**

- definir um contrato explícito para cada preferência, sua origem e onde é persistida;
- carregar preferências na restauração da sessão sem atrasar indevidamente a primeira tela;
- aplicar alterações de forma previsível e sincronizá-las entre clientes quando a preferência for
  de conta, não apenas local;
- manter defaults compatíveis para contas existentes.

**Dependências:** decisão de produto e possível contrato novo de backend; não presumir que storage
local seja suficiente.

### 3.8 Tema claro

**Resultado:** o web oferece tema claro sem degradar a identidade dark-first nem a acessibilidade.

**Critérios de aceitação:**

- tokens completos para fundo, superfícies, texto, estados, gráficos, foco e overlays;
- contraste e significado de status válidos nos dois temas, sem depender apenas de cor;
- preferência persistida conforme a decisão do módulo de Preferências, com fallback coerente ao
  sistema quando aplicável;
- verificação responsiva e visual das telas principais, dialogs e estados loading/empty/error.

**Dependências:** preferências; auditoria dos tokens e dos componentes Material tematizados.

### 3.9 Shell Tauri opcional

**Resultado:** se aprovado, o build web é reutilizado como aplicativo desktop leve, sem bifurcar a
regra de negócio.

**Critérios de aceitação:**

- decisão explícita de produto sobre macOS/plataformas, distribuição, atualização e assinatura;
- autenticação por cookie, abertura de links e origem da API validadas no WebView escolhido;
- dados financeiros e tokens não expostos a bridges desnecessárias;
- build web continua funcional isoladamente e o shell contém apenas adaptações de plataforma;
- instalação, atualização e desinstalação têm procedimento verificável antes de qualquer publicação.

**Dependências:** paridade web estável; auditoria de capacidades navegador/WebView; decisões de
distribuição e segurança. Não faz parte do MVP nem autoriza publicação.

## 4. Critério comum de encerramento de um módulo

Um módulo só sai deste backlog quando seu design específico confirmar contrato e regras destrutivas,
os fluxos de teclado/responsividade estiverem cobertos, writes invalidarem consumidores corretos,
erros preservarem contexto/dados do usuário e a verificação automatizada relevante estiver verde.
Isso define qualidade de aceitação, não uma decomposição de implementação.

## 5. Decisões fora deste backlog

- **Offline sync:** exige política própria de cache, fila, conflito, reconciliação e proteção de dados.
- **Push:** exige casos de uso, consentimento, entrega, privacidade e infraestrutura próprios.
- **Deploy/produção:** continua sendo uma etapa separada sujeita a autorização explícita.
