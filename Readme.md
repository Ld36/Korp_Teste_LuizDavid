# Sistema de Emissao de Notas Fiscais - Korp ERP

Projeto desenvolvido como solução para o desafio tecnico. A aplicacao consiste em um sistema de gestao de produtos, controle de estoque e emissao/impressao de notas fiscais estruturado em arquitetura de microsservicos desacoplados, persistencia real em PostgreSQL e interface SPA desenvolvida em Angular.

---

## 1. Arquitetura da Solucao

A solucao e composta por tres aplicacoes independentes e dois bancos de dados fisicamente isolados:

1. **Stock.API (Microsservico de Estoque)**
   - Porta padrao: `http://localhost:5284`
   - Responsabilidade: Cadastro de produtos, consulta de estoque, controle de concorrencia e baixa de saldo idempotente.
   - Banco de Dados: PostgreSQL (`korp_stock_db`).

2. **Invoicing.API (Microsservico de Faturamento)**
   - Porta padrao: `http://localhost:5106`
   - Responsabilidade: Criacao de notas fiscais com numeracao sequencial automatica, pre-validacao de saldos, integracao resiliente com a Stock.API e fechamento de status.
   - Banco de Dados: PostgreSQL (`korp_invoicing_db`).

3. **Korp.Web (Frontend SPA)**
   - Porta padrao: `http://localhost:4200`
   - Responsabilidade: Interface web reativa construida em Angular com tema de recibos/tickets fiscais, responsividade multiplataforma (Desktop/Mobile), controle assincrono via RxJS e simulador de indisponibilidade de servicos.

---

## 2. Tecnologias Utilizadas

- **Backend:** .NET 10 (C#), ASP.NET Core Minimal APIs, Entity Framework Core 10, Npgsql (PostgreSQL Provider), Swashbuckle (Swagger).
- **Frontend:** Angular 21 (Standalone Components, Signals, HttpClient, RxJS), SCSS Modular.
- **Banco de Dados:** PostgreSQL 16+.
- **Seguranca:** .NET User Secrets para protecao de credenciais em desenvolvimento.

---

## 3. Requisitos Atendidos

### Requisitos Obrigatorios
- **Arquitetura de Microsservicos:** Servico de Estoque e Servico de Faturamento separados.
- **Cadastro de Produtos:** Codigo, Descricao e Saldo disponivel.
- **Cadastro de Notas Fiscais:** Numeracao sequencial automatica, status inicial "Aberta" e inclusao de multiplos produtos.
- **Impressao de Notas Fiscais:** Validacao de saldo, fechamento para status "Fechada", bloqueio de reimpressao em notas fechadas e baixa automatica do saldo em estoque.
- **Tratamento de Falhas e Resiliencia:** Feedback visual imediato e retencao do status "Aberta" caso o servico de estoque esteja inacessivel ou offline (HTTP 503).
- **Persistencia Real:** Banco de dados relacional PostgreSQL com migrations aplicadas via Entity Framework Core.

### Requisitos Opcionais Implementados
- **Tratamento de Concorrencia Otimista:** Implementado via token nativo `xmin`/RowVersion no PostgreSQL e captura de `DbUpdateConcurrencyException`.
- **Idempotencia:** Controle de transacoes processadas via chave unica por item (`INV_{Id}_ITEM_{ItemId}`) na Stock.API, impedindo debitos duplicados por repeticao de chamadas de rede.

---

## 4. Pre-requisitos para Execucao Local

Antes de iniciar, certifique-se de ter instalado em sua maquina:

- .NET SDK (versao 8.0, 9.0 ou 10.0+)
- Node.js (versao 20.x ou 22.x LTS)
- Angular CLI (`npm install -g @angular/cli`)
- Servidor PostgreSQL ativo localmente (porta padrao 5432)

---

## 5. Passo a Passo para Execucao

### Passo 1: Clonar o Repositorio

```bash
git clone https://github.com/Ld36/Korp_Teste_LuizDavid.git
cd Korp_Teste_LuizDavid
```

### Passo 2: Configurar as Credenciais do PostgreSQL (User Secrets)

Para nao expor senhas em ambiente de versionamento publico, configure as connection strings locais utilizando o .NET Secret Manager:

```bash
# Configuracao do Banco de Estoque
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=localhost;Database=korp_stock_db;Username=postgres;Password=SUA_SENHA_AQUI" --project Stock.API

# Configuracao do Banco de Faturamento
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=localhost;Database=korp_invoicing_db;Username=postgres;Password=SUA_SENHA_AQUI" --project Invoicing.API
```

### Passo 3: Executar as Migrations do Banco de Dados

Execute os comandos abaixo na raiz do projeto para que o Entity Framework crie as duas bases de dados e as tabelas necessarias no PostgreSQL:

```bash
# Cria o banco 'korp_stock_db' e tabelas do Estoque
dotnet ef database update --project Stock.API

# Cria o banco 'korp_invoicing_db' e tabelas do Faturamento
dotnet ef database update --project Invoicing.API
```

### Passo 4: Executar as APIs do Backend

Abra dois terminais separados para rodar os microsservicos:

**Terminal 1 - Servico de Estoque:**

```bash
dotnet run --project Stock.API
```

- Endereco: `http://localhost:5284`
- Documentacao Swagger: `http://localhost:5284/swagger`

**Terminal 2 - Servico de Faturamento:**

```bash
dotnet run --project Invoicing.API
```

- Endereco: `http://localhost:5106`
- Documentacao Swagger: `http://localhost:5106/swagger`

### Passo 5: Executar o Frontend Angular

Em um terceiro terminal, acesse a pasta da aplicacao web, instale os pacotes e inicialize o servidor de desenvolvimento:

```bash
cd Korp.Web
npm install
npm start
```

Acesse a interface no navegador:
`http://localhost:4200`

## 6. Demonstracao dos Cenarios de Teste

### Cenario 1: Cadastro de Produtos e Emissao de Nota Fiscal

1. Acesse o menu "Produtos" e cadastre um produto (ex: Codigo `PRD-01`, Descricao `Teclado`, Saldo `10`).
2. Acesse o menu "Notas Fiscais", clique em "Nova nota fiscal", adicione `2` unidades do produto e salve a nota (status inicial `Aberta`).
3. Na listagem de notas, clique sobre a nota criada e pressione "Imprimir nota fiscal".
4. O sistema exibira o indicador de processamento, fechara a nota fiscal (status `Fechada`) e abatera o estoque do produto para `8` unidades.

### Cenario 2: Validacao de Saldo Insuficiente

1. Crie uma nota fiscal solicitando `15` unidades do produto `PRD-01` (saldo atual: `8`).
2. Ao tentar imprimir, a API validara o saldo e retornara erro de saldo insuficiente.
3. A nota fiscal permanecera com status `Aberta` e nenhum saldo sera alterado no banco.

### Cenario 3: Tratamento de Falha de Microsservico (Resiliencia)

1. Na barra lateral da interface, desative o switch "Servico Estoque" (simulacao de indisponibilidade) ou pare a execucao do terminal `Stock.API`.
2. Tente imprimir uma nota com status `Aberta`.
3. O sistema capturara a falha de comunicacao (HTTP 503), exibira o banner explicativo de servico indisponivel, mantera a nota com status `Aberta` e disponibilizara o botao "Tentar novamente" para recuperacao.

## 7. Endpoints da Solucao

### Stock.API (`http://localhost:5284`)

- `GET /api/products` - Listagem completa de produtos cadastrados.
- `GET /api/products/{code}` - Busca de produto por codigo.
- `POST /api/products` - Cadastro de novo produto.
- `POST /api/products/deduct-stock` - Baixa de estoque idempotente com tratamento de concorrencia.

### Invoicing.API (`http://localhost:5106`)

- `GET /api/invoices` - Listagem de notas fiscais ordenadas sequencialmente.
- `GET /api/invoices/{id}` - Detalhes de uma nota fiscal especifica.
- `POST /api/invoices` - Criacao de nota fiscal com numeracao sequencial e status "Aberta".
- `POST /api/invoices/{id}/print` - Processamento, validacao de saldo, baixa no estoque e fechamento da nota.

---

## 8. Observacoes Finais

- O projeto foi estruturado para demonstrar uma integracao real entre microsservicos e banco de dados isolados.
- A interface Angular foi desenvolvida para simular o comportamento de erro de dependencia e permitir teste de recuperacao.
- Todas as credenciais locais ficam protegidas via `.NET User Secrets`, evitando vazamento em repositórios públicos.