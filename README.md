# ClyvoCare API (.NET)

API REST para cadastrar clínicas veterinárias e consultar sua localização. Construída em ASP.NET Core com Oracle, faz parte do Challenge FIAP 2026, turma 2TDSPG.

Com ela, você pode cadastrar uma clínica, atualizar seus dados e encontrar clínicas por cidade, nome ou CNPJ. Esta API complementa a [API Java](https://github.com/ClyvoPet-Challenge-2026/Java-Sprint-1), responsável pelos tutores, pets, planos e contratações.

A proposta é manter o cadastro da rede veterinária ligado às cidades já usadas pelo restante do sistema. Para quem consulta, isso permite localizar as clínicas cadastradas. Para a administração, permite manter os dados de contato e localização sem duplicar o cadastro de estados e cidades.

## Como os projetos se complementam

O ClyvoCare está dividido em repositórios que atendem a partes diferentes da solução e das entregas do Challenge:

| Projeto | Papel |
|---|---|
| [API .NET](https://github.com/ClyvoPet-Challenge-2026/.Net-Sprint-1) | Cadastro e consulta de clínicas veterinárias, com leitura de estados e cidades compartilhados |
| [API Java](https://github.com/ClyvoPet-Challenge-2026/Java-Sprint-1) | Cadastro de tutores, pets e planos; autenticação; simulação e gestão de contratações |
| [Clyvo-Care — mobile](https://github.com/ClyvoPet-Challenge-2026/Clyvo-Care) | Aplicativo e apresentação visual da entrega de Mobile Application Development |
| [ClyvoCare Web](https://github.com/ClyvoPet-Challenge-2026/Java-Sprint-1-Web) | Interface React/Vite que consome a API Java para demonstrar os fluxos de Java Advanced |

A divisão de responsabilidades no banco é:

| Dados | API Java | API .NET |
|---|---|---|
| Estados e cidades — `TB_CAD_STATE`, `TB_CAD_CITY` | Cadastro e manutenção | Consulta |
| Tutores, pets, espécies, raças, planos e contratações | Cadastro e manutenção | Sem endpoints implementados |
| Clínicas — `TB_CAD_CLINIC` | A estrutura faz parte do schema compartilhado | CRUD e buscas |
| Eventos clínicos e lembretes | A estrutura faz parte do schema compartilhado | Escopo previsto, ainda sem entidades e endpoints implementados |

Para compartilhar os dados, configure as duas APIs para usar **o mesmo banco e o mesmo schema Oracle**. A .NET consulta as tabelas pelo Entity Framework Core; não faz chamadas HTTP à API Java para buscar cidades. Depois que o schema estiver preparado, Java não precisa estar em execução para a .NET consultar esses registros.

Por exemplo: uma cidade cadastrada pela API Java fica disponível em `GET /api/cidades` na .NET. O ID retornado pode ser usado para cadastrar uma clínica. Os contratos HTTP são diferentes: Java expõe `/cidades`, enquanto a .NET expõe `/api/cidades`.

Status e formas de pagamento das contratações são valores de texto em `TB_CAD_SUBSCRIPTION`, conforme os enums de Java. O SQL atual não cria as antigas tabelas `TB_CAD_SUB_STATUS` e `TB_CAD_PAYMENT_METHOD`.

As instruções do frontend web ficam no repositório **ClyvoCare Web**. Essa interface usa os fluxos de Java; para experimentar os endpoints desta API, use o Swagger ou um cliente HTTP.

## Como executar

Para rodar a API na sua máquina, você precisa de:

- **.NET SDK 9**, conforme [global.json](ClyvoCare/global.json).
- **Acesso a um banco Oracle**, com usuário e senha. Você pode usar o Oracle da FIAP com as credenciais da sua conta.
- Um schema com as tabelas da aplicação, conforme [Banco de dados compartilhado](#banco-de-dados-compartilhado).
- **Git**, para clonar o repositório.

### Configurando a conexão

Clone o projeto e entre na pasta da solução:

```bash
git clone https://github.com/ClyvoPet-Challenge-2026/.Net-Sprint-1.git
cd .Net-Sprint-1/ClyvoCare
```

Defina a conexão no mesmo terminal em que você vai iniciar a aplicação. Substitua os valores de usuário e senha pelos da sua conta Oracle.

No **Bash ou Git Bash**:

```bash
export ConnectionStrings__ClyvoCareOracle='Data Source=oracle.fiap.com.br:1521/ORCL;User Id=SEU_RM;Password=SUA_SENHA;'
dotnet restore
dotnet run --project ClyvoCare.API --launch-profile http
```

No **PowerShell**:

```powershell
$env:ConnectionStrings__ClyvoCareOracle = 'Data Source=oracle.fiap.com.br:1521/ORCL;User Id=SEU_RM;Password=SUA_SENHA;'
dotnet restore
dotnet run --project ClyvoCare.API --launch-profile http
```

A variável de ambiente substitui `ConnectionStrings:ClyvoCareOracle` do [appsettings.json](ClyvoCare/ClyvoCare.API/appsettings.json). Se você inicia pela IDE, configure essa mesma variável na execução. Para usar outro Oracle, ajuste o host, a porta e o serviço em `Data Source`.

As credenciais conectam a aplicação ao banco. **A API .NET atual não exige login ou token nos endpoints.** A autenticação JWT e os perfis `ADMIN` e `OWNER` documentados em Java pertencem à API Java.

### O que acontece ao iniciar

A aplicação registra o contexto Oracle, os repositórios, os serviços, o tratamento de erros e os recursos de observabilidade. Ela não cria o schema, não aplica migrations automaticamente e não carrega dados de demonstração ao iniciar.

Com o perfil `http`, os endereços são:

| Endereço | Uso |
|---|---|
| `http://localhost:5067/` | Swagger UI |
| `http://localhost:5067/swagger/v1/swagger.json` | Contrato OpenAPI em JSON |
| `http://localhost:5067/api/clinicas` | Cadastro e consulta de clínicas |
| `http://localhost:5067/health` | Verificação do Oracle e dos serviços externos |
| `http://localhost:5067/metrics` | Métricas no formato Prometheus |

O Swagger fica na raiz e é habilitado somente em **Development**, ambiente definido pelos perfis de [launchSettings.json](ClyvoCare/ClyvoCare.API/Properties/launchSettings.json).

Para usar HTTPS, execute `dotnet dev-certs https --trust` e depois `dotnet run --project ClyvoCare.API --launch-profile https`. Esse perfil usa `https://localhost:7197` e também configura a porta HTTP 5067; o middleware redireciona as chamadas HTTP para HTTPS.

O CORS permite `http://localhost:5173` e `http://localhost:3000` por padrão. Para mudar as origens, ajuste `Cors:AllowedOrigins` ou defina variáveis como `Cors__AllowedOrigins__0` com o endereço do frontend.

## Banco de dados compartilhado

O arquivo [docs/script.sql](docs/script.sql) é uma cópia do [SQL da API Java](https://github.com/ClyvoPet-Challenge-2026/Java-Sprint-1/blob/10e529f347c39b3852a6f904ff2c025b8ad0c0ad/docs/script.sql), na revisão `10e529f`. Ele reúne o modelo das duas APIs, com 13 tabelas, dados de demonstração, funções, procedimentos, trigger de auditoria e consultas de verificação.

Essa cópia também corresponde ao `script_bd.sql` da raiz do projeto Java. Ao atualizar o modelo compartilhado, mantenha as cópias sincronizadas. Use `docs/script.sql` como referência para preparar e consultar o modelo atual.

### Usando um schema já preparado

Se a API Java já preparou o banco com Flyway, configure a .NET para usar esse mesmo schema. Você não precisa executar o SQL novamente para iniciar a .NET.

Confira as tabelas usadas por esta API em um cliente Oracle, como o SQL Developer:

```sql
SELECT ID, UF, NAME FROM TB_CAD_STATE ORDER BY ID;
SELECT ID, STATE_ID, NAME FROM TB_CAD_CITY ORDER BY ID;
SELECT ID, NAME, CNPJ, CITY_ID, PHONE FROM TB_CAD_CLINIC ORDER BY ID;
```

A .NET mapeia essas três tabelas em [ClyvoCareContext](ClyvoCare/ClyvoCare.Infrastructure/Persistence/ClyvoCareContext.cs). As chaves estrangeiras ligam clínica → cidade → estado. O SQL compartilhado usa os mesmos nomes de colunas e tamanhos definidos nas configurações do Entity Framework.

### Preparando um banco de demonstração com SQL

**O `docs/script.sql` exclui e recria as tabelas da aplicação e os objetos PL/SQL listados no arquivo. Isso apaga os dados existentes nessas tabelas, inclusive os compartilhados com Java.** Use esse fluxo em um schema de demonstração que possa ser recriado.

No SQL Developer:

1. Conecte com o usuário Oracle que será usado pela aplicação.
2. Abra [docs/script.sql](docs/script.sql).
3. Execute como script, com **F5**, para processar os blocos PL/SQL e os comandos de configuração.
4. Confira a saída, as contagens de registros e as consultas de `USER_OBJECTS` e `USER_ERRORS` incluídas no final.
5. Configure a conexão da API para esse schema e inicie a aplicação.

O arquivo inclui operações de demonstração e `COMMIT`, além do DDL e da carga inicial. Ele não substitui o versionamento de um banco em uso e não reinicia os históricos do Flyway ou do Entity Framework.

Os objetos de contratação e auditoria ficam disponíveis no banco, mas a API .NET atual não chama essas funções e procedures. Sua persistência usa os repositórios do Entity Framework para estados, cidades e clínicas.

### Migrations do Entity Framework e do Flyway

A migration [InitialCreate](ClyvoCare/ClyvoCare.Infrastructure/Migrations/20260523133532_InitialCreate.cs) da .NET tem `Up()` e `Down()` vazios. Ela é um ponto de partida para o histórico do Entity Framework; executar `dotnet ef database update` não cria as tabelas de negócio.

A evolução do schema na execução normal de Java está nas [migrations Flyway](https://github.com/ClyvoPet-Challenge-2026/Java-Sprint-1/tree/develop/src/main/resources/db/migration). Elas criam a estrutura, convertem status e pagamentos para texto e carregam os dados de demonstração. As orientações para schemas existentes estão no [README de Java](https://github.com/ClyvoPet-Challenge-2026/Java-Sprint-1#banco-de-dados-e-migrations).

### Oracle na FIAP e na Azure

O [script ACR + ACI de Java](https://github.com/ClyvoPet-Challenge-2026/Java-Sprint-1/blob/develop/azure-acr-aci-setup.sh) cria um Oracle próprio e publica a API Java. O banco desse ambiente é separado do Oracle da FIAP, e esse script não publica a API .NET.

Para a .NET acessar os registros desse ambiente, configure `Data Source=HOST_DO_ACI:1521/XEPDB1` e o mesmo schema usado por Java. No manifesto atual do script, a API Java se conecta como `system`, com a senha definida por `ORACLE_PWD`; o usuário `APP_DB_USER` criado pelo script é outro schema.

O deploy ACI atual desabilita Flyway e usa Hibernate para criar ou atualizar tabelas. Consulte as diferenças e os comandos de provisionamento no [README de Java](https://github.com/ClyvoPet-Challenge-2026/Java-Sprint-1#deploy-no-azure-acr--aci).

## Endpoints

As rotas de negócio começam com `/api`. As listagens retornam arrays, sem paginação.

| Método | Rota | Uso | Respostas principais |
|---|---|---|---|
| `GET` | `/api/estados` | Listar estados | `200` |
| `GET` | `/api/estados/{id}` | Consultar um estado | `200`, `404` |
| `GET` | `/api/cidades` | Listar cidades com o estado aninhado | `200` |
| `GET` | `/api/cidades/{id}` | Consultar uma cidade | `200`, `404` |
| `GET` | `/api/clinicas` | Listar clínicas com cidade e estado | `200` |
| `GET` | `/api/clinicas/{id}` | Consultar uma clínica | `200`, `404` |
| `GET` | `/api/clinicas/por-cidade/{cityId}` | Listar clínicas de uma cidade existente | `200`, `404` |
| `GET` | `/api/clinicas/buscar?nome=&cnpj=` | Buscar por nome parcial e/ou CNPJ exato | `200` |
| `POST` | `/api/clinicas` | Cadastrar uma clínica | `201`, `400`, `404` |
| `PUT` | `/api/clinicas/{id}` | Atualizar os dados de uma clínica | `204`, `400`, `404` |
| `DELETE` | `/api/clinicas/{id}` | Excluir uma clínica | `204`, `404` |

### Cadastrar e atualizar uma clínica

Primeiro, consulte `GET /api/cidades` e escolha uma cidade existente. Use o ID retornado no campo `cityId`; o valor `1` abaixo é apenas um exemplo.

```http
POST /api/clinicas
Content-Type: application/json

{
  "name": "Vet Care Center",
  "cnpj": "99.999.999/0001-99",
  "cityId": 1,
  "phone": "(11) 99999-9999"
}
```

O cadastro retorna `201 Created`, com o endereço do recurso no cabeçalho `Location` e a clínica no corpo. A resposta inclui cidade e estado aninhados:

```json
{
  "id": 23,
  "name": "Vet Care Center",
  "cnpj": "99.999.999/0001-99",
  "phone": "(11) 99999-9999",
  "city": {
    "id": 1,
    "name": "Sao Paulo",
    "state": {
      "id": 1,
      "name": "Sao Paulo",
      "uf": "SP"
    }
  }
}
```

Para atualizar, envie o mesmo formato de corpo para `PUT /api/clinicas/{id}`. A resposta de sucesso é `204 No Content`. A exclusão também retorna `204`; se a clínica não existir, retorna `404`.

| Campo | Validação |
|---|---|
| `name` | Obrigatório, entre 2 e 150 caracteres |
| `cnpj` | Obrigatório, no formato `XX.XXX.XXX/XXXX-XX`, sem repetição entre clínicas |
| `cityId` | Positivo e correspondente a uma cidade existente |
| `phone` | Opcional, com até 20 caracteres |

A validação de CNPJ verifica o formato e a duplicidade; não calcula os dígitos verificadores. Nome, CNPJ e telefone são ajustados com `Trim()`, e telefone em branco é armazenado como nulo.

O serviço verifica a existência da cidade e a disponibilidade do CNPJ antes de gravar. Cidade inexistente retorna `404`; CNPJ já usado retorna `400`. As constraints do Oracle também protegem os relacionamentos e a unicidade. Uma clínica com eventos clínicos vinculados pode ter a exclusão impedida pelo banco.

### Consultar e buscar

No Bash ou Git Bash:

```bash
curl -i http://localhost:5067/api/cidades
curl -i http://localhost:5067/api/clinicas
curl -i "http://localhost:5067/api/clinicas/buscar?nome=Vet"
curl -i "http://localhost:5067/api/clinicas/buscar?cnpj=99.999.999/0001-99"
```

No PowerShell, use `curl.exe` no lugar de `curl`. Você também pode executar essas operações pelo Swagger na raiz da API.

A busca por nome não diferencia maiúsculas de minúsculas. Quando nome e CNPJ são informados juntos, os dois filtros são aplicados. Uma cidade existente sem clínicas retorna uma lista vazia; uma cidade inexistente na rota `por-cidade` retorna `404`.

## Monitoramento e observabilidade

A configuração está em [Program.cs](ClyvoCare/ClyvoCare.API/Program.cs).

### Health Checks

`GET /health` executa três verificações:

| Verificação | Dependência |
|---|---|
| `Oracle FIAP` | Oracle configurado em `ConnectionStrings:ClyvoCareOracle` |
| `FIAP` | `https://fiap.com.br` |
| `Google` | `https://google.com.br` |

O nome `Oracle FIAP` é fixo no código, mesmo quando a conexão aponta para outro Oracle. A resposta reúne o estado geral em `status`, a duração em `duration` e uma lista `checks`. Cada item traz `name`, `status`, `description`, `duration` e `error`.

O endpoint retorna `200` quando as verificações estão saudáveis e `503` em caso de falha crítica. Uma falha de credenciais Oracle ou indisponibilidade dos sites pode deixar o resultado geral como `Unhealthy`; confira `checks[].error` para identificar a dependência.

O teste cobre conectividade. Ele não confirma se todas as tabelas, dados ou operações de negócio estão corretas. Na versão atual, há somente `/health`, sem rotas separadas de readiness e liveness.

### Logs com Serilog

O Serilog registra mensagens no console e em arquivos diários `logs/clyvocare-.log`, relativos à pasta de execução. O nível padrão é `Information`, com `Microsoft.AspNetCore` em `Warning`, conforme o `appsettings.json`.

O middleware `UseSerilogRequestLogging()` registra requisições HTTP. O tratamento global de exceções registra a mensagem e um identificador obtido de `Activity.Current?.Id`, usando `HttpContext.TraceIdentifier` como alternativa.

Em Development, respostas de erro tratadas pelo handler incluem `traceId` com o `HttpContext.TraceIdentifier`. A API não tem middleware próprio para receber e devolver `X-Correlation-ID`; o identificador de atividade usado no log pode ser diferente desse `traceId` da resposta.

### Métricas com OpenTelemetry e Prometheus

`GET /metrics` expõe métricas no formato Prometheus. A configuração usa o nome de serviço `ClyvoCare.API` e inclui instrumentação de ASP.NET Core, HttpClient e runtime do .NET.

Esses dados permitem acompanhar requisições, duração e recursos do processo. Para consultar:

```bash
curl -i http://localhost:5067/health
curl -i http://localhost:5067/metrics
```

O OpenTelemetry está configurado com `WithMetrics`. Ainda não há `WithTracing` nem um exportador de traces distribuídos configurado nesta API.

## Tratamento de erros

O [GlobalExceptionHandler](ClyvoCare/ClyvoCare.API/Exceptions/GlobalExceptionHandler.cs) transforma exceções em respostas `ProblemDetails`, com campos como `title`, `status`, `detail` e `instance`.

| Situação | Resposta |
|---|---|
| Argumento inválido, regra de domínio ou CNPJ duplicado | `400` |
| Recurso não encontrado pelos serviços | `404` |
| `UnauthorizedAccessException` | `401` |
| Exceção não prevista | `500`, com mensagem genérica |

A validação automática do corpo pode retornar `ValidationProblemDetails`, com erros por campo em `errors`. Alguns controllers retornam `NotFound()` diretamente quando a consulta não encontra um registro.

## Testes

A solução tem três projetos de teste com xUnit, organizados por camada:

| Projeto | O que verifica |
|---|---|
| [ClyvoCare.Domain.Tests](ClyvoCare/tests/ClyvoCare.Domain.Tests) | Criação e atualização de clínicas, invariantes e normalização dos campos |
| [ClyvoCare.Application.Tests](ClyvoCare/tests/ClyvoCare.Application.Tests) | Serviços de clínicas, cidades e estados, com repositórios simulados por Moq |
| [ClyvoCare.IntegrationTests](ClyvoCare/tests/ClyvoCare.IntegrationTests) | Endpoints de clínicas, health checks e métricas usando `WebApplicationFactory<Program>` |

Os testes seguem o padrão Arrange, Act, Assert e nomes que descrevem método, cenário e resultado. Os testes HTTP usam uma aplicação compartilhada por `CollectionFixture`.

Na pasta `ClyvoCare`, execute:

```bash
dotnet test ClyvoCare.sln
```

Para gerar os arquivos de cobertura com `coverlet.collector`:

```bash
dotnet test ClyvoCare.sln --collect:"XPlat Code Coverage"
```

Para executar somente os testes de domínio e aplicação:

```bash
dotnet test tests/ClyvoCare.Domain.Tests/ClyvoCare.Domain.Tests.csproj
dotnet test tests/ClyvoCare.Application.Tests/ClyvoCare.Application.Tests.csproj
```

O restore precisa de acesso ao NuGet. Os testes unitários não usam banco real. Nos testes de integração, o contexto de persistência é substituído por SQLite em memória, com estados e cidades de teste.

**As verificações de `/health` continuam usando o Oracle configurado e os sites externos.** A factory substitui o DbContext, mas não troca os health checks. Esse teste confere o formato do JSON e a presença das verificações; passar nele não comprova que o Oracle está saudável. Os testes com SQLite também não validam o DDL e as particularidades do provider Oracle.

## Tecnologias utilizadas

| Ferramenta | Papel |
|---|---|
| .NET 9 e ASP.NET Core | Execução da API e endpoints HTTP |
| Entity Framework Core 9 e Oracle.EntityFrameworkCore | Persistência e mapeamento do Oracle |
| Swashbuckle | Swagger UI e contrato OpenAPI |
| Serilog | Logs no console, arquivo diário e requisições HTTP |
| AspNetCore.HealthChecks.Oracle e Uris | Verificação de banco e URLs externas |
| OpenTelemetry e exportador Prometheus | Instrumentação e exposição de métricas |
| xUnit, Moq e WebApplicationFactory | Testes unitários e de integração |
| SQLite em memória | Persistência isolada dos testes de integração |
| Coverlet | Coleta de cobertura dos testes |

As versões estão nos arquivos `.csproj` de cada projeto. O [global.json](ClyvoCare/global.json) seleciona a linha de SDK 9 com `rollForward=latestMinor`.

## Estrutura e decisões de implementação

A solução separa domínio, aplicação, persistência e HTTP em quatro projetos:

```text
ClyvoCare/
├── ClyvoCare.Domain/          entidades e exceções de domínio
├── ClyvoCare.Application/     DTOs, contratos de repositório e serviços
├── ClyvoCare.Infrastructure/  DbContext, mapeamentos Oracle e repositórios
├── ClyvoCare.API/             controllers, configuração, erros e observabilidade
└── tests/
    ├── ClyvoCare.Domain.Tests/
    ├── ClyvoCare.Application.Tests/
    └── ClyvoCare.IntegrationTests/
```

Para acompanhar uma requisição, comece no controller, siga para o serviço e depois para o repositório. Os DTOs separam o JSON das entidades, e os serviços verificam a existência da cidade e a unicidade do CNPJ antes de persistir.

A entidade `Clinic` concentra suas invariantes em `Create` e `Update`. `State` e `City` têm construtores privados e são usados para leitura nesta API.

O repositório genérico cobre operações básicas. Os repositórios de cidade e clínica carregam os relacionamentos necessários com `Include` e `ThenInclude`, permitindo retornar cidade e estado aninhados. As consultas de existência usam `Count() > 0`, conforme a implementação atual.

Outros arquivos úteis:

| Arquivo | Conteúdo |
|---|---|
| [docs/script.sql](docs/script.sql) | SQL atualizado do modelo compartilhado com Java |
| [docs/MER.png](docs/MER.png) | Diagrama de referência; confira a estrutura atual no SQL |
| [Program.cs](ClyvoCare/ClyvoCare.API/Program.cs) | Configuração HTTP, observabilidade e inicialização |
| [ClinicRequest.cs](ClyvoCare/ClyvoCare.Application/DTOs/ClinicRequest.cs) | Campos e validações de cadastro e atualização |

## Relação com a Sprint 3

| Requisito | Onde encontrar |
|---|---|
| Health Checks | `/health`, com Oracle e duas URLs externas |
| Logging estruturado | Serilog, console, arquivo diário e logs de requisição |
| Métricas | OpenTelemetry e `/metrics` para Prometheus |
| Testes unitários em AAA | Projetos de domínio e aplicação, com xUnit e Moq |
| Testes de integração | `WebApplicationFactory`, SQLite e cenários HTTP de sucesso e erro |
| Organização e documentação | Projetos por camada e instruções de execução neste README |
| Integração com Java | Schema Oracle compartilhado e SQL em `docs/script.sql` |

Tracing distribuído e um middleware próprio de Correlation ID ainda precisam ser implementados para completar esses pontos de observabilidade. Os testes de autenticação também dependem de adicionar autenticação à API .NET.

Eventos clínicos, lembretes, cache e autenticação continuam como evolução prevista. O CRUD implementado nesta versão é o de clínicas; as demais tabelas presentes no SQL não representam endpoints já disponíveis.
