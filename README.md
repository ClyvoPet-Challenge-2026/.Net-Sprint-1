# ClyvoCare API (.NET)

API REST para cadastrar clínicas veterinárias, consultar sua localização e agendar atendimentos dos pets. Construída em ASP.NET Core com Oracle, faz parte do Challenge FIAP 2026, turma 2TDSPG.

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
| Eventos clínicos — `TB_HEA_CLINICAL_EVENT` | Criação da tabela no baseline compartilhado | Criação e consulta de agendamentos, com isolamento por tutor |
| Lembretes | A estrutura faz parte do schema compartilhado | Sem entidades e endpoints implementados |

Para compartilhar os dados, configure as duas APIs para usar **o mesmo banco e o mesmo schema Oracle**. A .NET consulta as tabelas pelo Entity Framework Core; não faz chamadas HTTP à API Java para buscar cidades. Depois que o schema estiver preparado, Java não precisa estar em execução para a .NET consultar esses registros.

Por exemplo: uma cidade cadastrada pela API Java fica disponível em `GET /api/cidades` na .NET. O ID retornado pode ser usado para cadastrar uma clínica. Os contratos HTTP são diferentes: Java expõe `/cidades`, enquanto a .NET expõe `/api/cidades`.

Status e formas de pagamento das contratações são valores de texto em `TB_CAD_SUBSCRIPTION`, conforme os enums de Java. O SQL atual não cria as antigas tabelas `TB_CAD_SUB_STATUS` e `TB_CAD_PAYMENT_METHOD`.

As instruções do frontend web ficam no repositório **ClyvoCare Web**. Essa interface usa os fluxos de Java; para experimentar os endpoints desta API, use o Swagger ou um cliente HTTP.

## Como executar

Para rodar a API na sua máquina, você precisa de:

- **.NET SDK 9**, conforme [global.json](ClyvoCare/global.json).
- **Acesso a um banco Oracle**, com usuário e senha. Você pode usar o Oracle da FIAP com as credenciais da sua conta.
- **MongoDB**, local ou remoto, para a auditoria. Para preparar as coleções e importar o dataset, use também o [MongoDB Shell (`mongosh`)](https://www.mongodb.com/docs/mongodb-shell/install/).
- Um schema com as tabelas da aplicação, conforme [Banco de dados compartilhado](#banco-de-dados-compartilhado).
- **Git**, para clonar o repositório.

### Configurando a conexão

Clone o projeto e entre na pasta da solução:

```bash
git clone https://github.com/ClyvoPet-Challenge-2026/.Net-Sprint-1.git
cd .Net-Sprint-1/ClyvoCare
```

Defina a conexão e o caminho da chave pública no mesmo terminal em que você vai iniciar a aplicação. Substitua os valores de usuário e senha pelos da sua conta Oracle e o caminho pelo arquivo público do par usado pela API Java.

No **Bash ou Git Bash**:

```bash
export ConnectionStrings__ClyvoCareOracle='Data Source=oracle.fiap.com.br:1521/ORCL;User Id=SEU_RM;Password=SUA_SENHA;'
export Jwt__PublicKeyPath='/CAMINHO/ABSOLUTO/public_key.pem'
export Jwt__Issuer='clyvocare-api'
export Mongo__ConnectionString='mongodb://localhost:27017'
export Mongo__DatabaseName='clyvocare'
dotnet restore
dotnet run --project ClyvoCare.API --launch-profile http
```

No **PowerShell**:

```powershell
$env:ConnectionStrings__ClyvoCareOracle = 'Data Source=oracle.fiap.com.br:1521/ORCL;User Id=SEU_RM;Password=SUA_SENHA;'
$env:Jwt__PublicKeyPath = 'C:/CAMINHO/ABSOLUTO/public_key.pem'
$env:Jwt__Issuer = 'clyvocare-api'
$env:Mongo__ConnectionString = 'mongodb://localhost:27017'
$env:Mongo__DatabaseName = 'clyvocare'
dotnet restore
dotnet run --project ClyvoCare.API --launch-profile http
```

A variável de ambiente substitui `ConnectionStrings:ClyvoCareOracle` do [appsettings.json](ClyvoCare/ClyvoCare.API/appsettings.json). Se você inicia pela IDE, configure essa mesma variável na execução. Para usar outro Oracle, ajuste o host, a porta e o serviço em `Data Source`.

As credenciais conectam a aplicação ao banco. A .NET valida os JWTs emitidos pelo login Java e consulta a conta no mesmo schema Oracle. Não há login separado na .NET. Leituras de clínicas, cidades e estados são públicas; criar, atualizar e excluir clínicas exige `ADMIN`.

`Jwt__PublicKeyPath` recebe um caminho de arquivo, sem o prefixo `file:` usado na configuração Java. No Git Bash do Windows, use um caminho como `C:/chaves/public_key.pem`. Sem configuração explícita, o padrão é `.keys/public_key.pem`, relativo à pasta de execução. A chave é carregada e validada na inicialização: arquivo ausente, chave privada, PEM inválido ou RSA com menos de 2048 bits impedem a API de iniciar. A .NET recebe somente a pública; a privada permanece exclusiva da Java. Arquivos PEM locais ficam fora do Git.

O token deve ter assinatura RS256, emissor `clyvocare-api`, expiração, `sub` com e-mail, `ownerId` positivo e um único perfil `OWNER` ou `ADMIN`. A API confere ID, e-mail, perfil e `ENABLED='Y'` no banco a cada autenticação. Conta desativada ou identidade divergente retorna `401`, mesmo com assinatura válida. Tokens antigos sem `ownerId` exigem novo login. O contrato Java não emite audience, portanto essa verificação está desabilitada; assinatura, emissor, prazo e conta continuam obrigatórios. A tolerância de relógio é zero: mantenha os servidores sincronizados.

No Swagger, use **Authorize** e informe o token Java sem o prefixo `Bearer`. O contrato marca autenticação nas operações protegidas, preservando as consultas públicas. Em clientes HTTP, envie `Authorization: Bearer SEU_TOKEN`. Falta de token ou token inválido retorna `401`; conta `OWNER` tentando alterar clínicas recebe `403`. Ao trocar o par de chaves, distribua a nova pública, reinicie os serviços e faça novo login.

Para gerar o par RSA após clonar, execute `bash scripts/generate-keys.sh` na raiz do repositório Java. Aponte a .NET para o `public_key.pem` gerado ali; não gere um par diferente para cada API. O script preserva chaves existentes e aceita uma pasta de destino como primeiro argumento.

Antes de iniciar a .NET, prepare o MongoDB conforme a seção de auditoria abaixo. A URI local acima pressupõe um servidor MongoDB já iniciado; ela não instala nem cria um container. Em deploy, use o endereço interno ou remoto do serviço e suas credenciais. Dentro de containers separados, `localhost` aponta para o próprio container. As duas variáveis `Mongo__ConnectionString` e `Mongo__DatabaseName` são obrigatórias e não têm credenciais padrão no repositório.

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

O Swagger fica na raiz e é habilitado por padrão em **Development**, ambiente definido pelos perfis de [launchSettings.json](ClyvoCare/ClyvoCare.API/Properties/launchSettings.json). Para disponibilizá-lo em outro ambiente, configure `Swagger__Enabled=true`. Isso mantém o ambiente e a autenticação da API; as operações protegidas continuam exigindo JWT.

Para usar HTTPS, execute `dotnet dev-certs https --trust` e depois `dotnet run --project ClyvoCare.API --launch-profile https`. Esse perfil usa `https://localhost:7197` e também configura a porta HTTP 5067; o middleware redireciona as chamadas HTTP para HTTPS.

O CORS permite `http://localhost:5173` e `http://localhost:3000` por padrão. Para mudar as origens, ajuste `Cors:AllowedOrigins` ou defina variáveis como `Cors__AllowedOrigins__0` com o endereço do frontend.

## Banco de dados compartilhado

O arquivo [docs/script.sql](docs/script.sql) é uma cópia do [SQL da API Java](https://github.com/ClyvoPet-Challenge-2026/Java-Sprint-1/blob/fad4ab8/docs/script.sql), na revisão `fad4ab8`. Ele reúne o modelo das duas APIs, com 13 tabelas, dados de demonstração, funções e procedimentos organizados em packages, trigger de auditoria e consultas de verificação. O escape de caracteres de controle do JSON acompanha a correção da migration V5 de Java.

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

A migration [AddAppointmentDetails](ClyvoCare/ClyvoCare.Infrastructure/Migrations/20261026190000_AddAppointmentDetails.cs) mantém o ID `20261026190000_AddAppointmentDetails`, já registrado no schema compartilhado inspecionado. Ela acrescenta `REASON` NVARCHAR2(150), `NOTES` NVARCHAR2(1000), o índice `IX_EVENT_CLINIC` e o índice único `UX_EVENT_PET_DATE` em `(PET_ID, EVENT_DATE)`. As colunas permitem nulo para preservar eventos antigos. A entidade exige motivo em novas criações, aceita os cinco tipos definidos no banco e fixa o status inicial em `SCHEDULED`.

Os novos horários são normalizados para UTC e truncados à precisão de microssegundos antes da validação de futuro. `EVENT_DATE` não armazena fuso; a leitura assume UTC sem deslocar os valores gravados. Horários legados que tenham sido armazenados como hora local exigem uma revisão separada dos dados.

As referências de tutor e pet são mapeadas para consulta e ficam excluídas das migrations EF. Não há endpoints .NET para gravar essas identidades. A migration clínica não recria a tabela de eventos nem as tabelas Java; as chaves estrangeiras existentes devem ser preservadas.

Antes de atualizar outro ambiente, confira o schema, os históricos Flyway/EF, as colunas, as definições dos índices e possíveis duplicidades de pet/data. Prepare o baseline com Flyway primeiro. Se o histórico EF já contém `20261026190000_AddAppointmentDetails` e os objetos correspondem à migration, ela não deve ser reaplicada. Se há objetos sem histórico correspondente ou uma atualização parcial, investigue antes de executar novamente. A aplicação não aplica migrations automaticamente.

**O rollback dessa migration remove as duas colunas e os índices, perdendo os motivos e observações armazenados.** Ele não faz parte da validação normal. Não use o script destrutivo de demonstração para corrigir divergências em um banco existente.

A evolução do schema na execução normal de Java está nas [migrations Flyway](https://github.com/ClyvoPet-Challenge-2026/Java-Sprint-1/tree/develop/src/main/resources/db/migration). Elas criam a estrutura, convertem status e pagamentos para texto e carregam os dados de demonstração. As orientações para schemas existentes estão no [README de Java](https://github.com/ClyvoPet-Challenge-2026/Java-Sprint-1#banco-de-dados-e-migrations).

### Oracle na FIAP e na Azure

Para o novo ambiente **App Service**, as duas APIs serão publicadas na Azure e continuarão acessando o mesmo Oracle da FIAP pela rede. O banco não será copiado nem recriado. A conexão de cada Web App deve apontar para o mesmo schema; confira o histórico Flyway e EF antes de qualquer atualização. A conexão Azure → FIAP ainda precisa ser validada no ambiente publicado.

O provisionamento compartilhado está no [README Java](https://github.com/ClyvoPet-Challenge-2026/Java-Sprint-1#provisionamento-compartilhado). O grupo dedicado desta entrega é `rm561584`, com as duas APIs no mesmo plano App Service. Os scripts antigos de ACR + ACI foram removidos; a publicação atual usa os artefatos das pipelines.

## MongoDB, auditoria e exportação

Oracle continua sendo a fonte dos dados das duas APIs. MongoDB tem duas coleções distintas: `ClinicAuditLogs`, gravada pela .NET após alterações nas clínicas, e `Clinics`, alimentada pela exportação/importação abaixo. A API Java não depende do MongoDB.

### Provisionamento gratuito no Atlas

O script [scripts/provision-atlas.cjs](scripts/provision-atlas.cjs) cria ou reutiliza um projeto dedicado com tag `project=clyvocare-sprint4` e um cluster **M0 gratuito no provedor Azure**. Ele consulta as regiões M0 antes de criar o cluster, usa `US_EAST_2` por padrão e não faz fallback para um plano pago. O Atlas usa sua própria conta e não exige que ela seja a mesma do login Azure; o cluster fica na infraestrutura do provedor escolhido, administrado pelo Atlas.

Instale Node.js e a [Atlas CLI](https://www.mongodb.com/docs/atlas/cli/current/install-atlas-cli/) no PATH. A validação desta entrega usou Atlas CLI 1.59.4. Se usar uma instalação portátil, configure `ATLAS_EXECUTABLE` com o caminho do executável. O script não instala ferramentas, não compra serviços e não configura o faturamento do Marketplace Azure.

Na raiz do repositório, autentique um perfil próprio e consulte o ID da sua organização:

```bash
atlas auth login --authType UserAccount --profile clyvocare-rm561584 --output json
atlas organizations list --profile clyvocare-rm561584 --output json
export ATLAS_PROFILE='clyvocare-rm561584'
export ATLAS_ORG_ID='ID_DA_SUA_ORGANIZACAO'
export ATLAS_PROJECT_NAME='clyvocare-rm561584'
export ATLAS_CLUSTER_NAME='clyvocare-rm561584'
export ATLAS_REGION='US_EAST_2'
export Mongo__DatabaseName='clyvocare'
export ATLAS_ALLOWED_IPS='SEU_IP_PUBLICO'
```

`ATLAS_ALLOWED_IPS` aceita endereços IPv4 individuais separados por vírgula, sem faixas CIDR ou `0.0.0.0/0`. Inicialmente, informe o IP do computador que fará a preparação. Depois de criar o App Service, acrescente os IPs possíveis de saída da .NET retornados pelo provisionamento Azure e execute novamente. As regras existentes não são apagadas; regras amplas ou um projeto/cluster fora do escopo esperado fazem o script parar para revisão.

Gere duas senhas diferentes e fortes, com pelo menos 20 caracteres, e preserve-as em um cofre ou variáveis secretas. Para informá-las no Git Bash sem gravar valores no histórico:

```bash
read -r -s -p 'Senha do usuario da aplicacao: ' ATLAS_APP_PASSWORD; printf '\n'
export ATLAS_APP_PASSWORD
read -r -s -p 'Senha do usuario de preparacao: ' ATLAS_SETUP_PASSWORD; printf '\n'
export ATLAS_SETUP_PASSWORD
node scripts/provision-atlas.cjs
```

O script cria `clyvocare_app` com `readWrite` no banco informado e `clyvocare_setup` com `readWrite` e `dbAdmin` nesse banco. Os dois ficam restritos ao cluster. As senhas são enviadas à API por stdin e não aparecem nos argumentos dos processos nem na saída. Usuários existentes são conferidos e reutilizados, sem troca de senha ou alteração de permissões; mantenha as credenciais originais. A saída contém apenas IDs, estado, SRV sem credenciais e nomes dos usuários. Uma falha interrompe o fluxo, sem excluir recursos criados ou afetar outro projeto; corrija a causa e reexecute com os mesmos nomes e segredos.

Antes de conectar ao banco, espere o cluster ficar `IDLE`. Se precisar acompanhar pela CLI, use o `projectId` retornado pelo script:

```bash
atlas clusters watch clyvocare-rm561584 --projectId ID_DO_PROJETO --profile clyvocare-rm561584
```

Configure a URI `mongodb+srv` com usuário e senha escapados para URL e `authSource=admin`. A API e o importador usam **`clyvocare_app`**; somente a preparação de validadores e índices usa **`clyvocare_setup`**. Guarde a URI como segredo do ambiente, sem colocá-la no Git, nos artefatos ou no vídeo. Nenhuma chave RSA é enviada ao Atlas.

### Preparar as coleções

Na raiz deste repositório, defina `Mongo__ConnectionString` com a URI do usuário de preparação e `Mongo__DatabaseName` com o mesmo banco da API. Execute:

```bash
mongosh --nodb --file docs/mongodb/create-collections.js
```

O [script de criação](docs/mongodb/create-collections.js) configura validadores e índices para as duas coleções. Pode ser executado novamente: atualiza os validadores sem excluir os documentos. Os IDs relacionais são BSON `long`; a data da auditoria é BSON `date`. Na coleção `Clinics`, cidade e estado ficam embutidos no documento, evitando joins para consultas por localidade.

O usuário que executa a preparação precisa de permissão para criar coleções, modificar validadores (`collMod`) e criar índices. Depois dessa etapa, use a URI de `clyvocare_app` na aplicação e na importação. O usuário da aplicação não recebe permissão para alterar validadores. Use os endereços e usuários do ambiente de deploy, sem versionar credenciais.

### Gerar e importar o dataset

Com a .NET iniciada e um token `ADMIN` obtido pelo login Java, execute na raiz do repositório, em Bash ou Git Bash:

```bash
export DOTNET_API_URL='http://localhost:5067'
export ADMIN_TOKEN='TOKEN_OBTIDO_NO_LOGIN_JAVA'
mkdir -p artifacts
curl --fail --silent --show-error \
  -H "Authorization: Bearer $ADMIN_TOKEN" \
  "$DOTNET_API_URL/api/clinicas/exportar" \
  --output artifacts/clinics.json
export CLINICS_JSON_PATH="$PWD/artifacts/clinics.json"
mongosh --nodb --file docs/mongodb/import-clinics.js
```

`GET /api/clinicas/exportar` baixa um array JSON com todas as clínicas do Oracle, sem paginação ou links HTTP. Exige `ADMIN`: sem autenticação retorna `401`, e `OWNER` recebe `403`. Cada documento usa o ID relacional em `_id` e contém `name`, `cnpj`, `phone` e `city`, com `state` aninhado. O exportador não consulta MongoDB e funciona durante sua indisponibilidade.

O [script de importação](docs/mongodb/import-clinics.js) usa `replaceOne` com `upsert` pelo `_id`: repetir a importação atualiza os mesmos documentos sem duplicá-los. Não apaga clínicas ausentes do novo arquivo nem sincroniza alterações posteriores automaticamente. Um erro interrompe a execução; documentos já importados permanecem e podem ser atualizados ao repetir o comando. Os IDs precisam ser inteiros positivos representáveis com segurança em JavaScript; valores acima desse limite são recusados antes das gravações.

No Git Bash do Windows, se o `mongosh` for o executável nativo do Windows, use `export CLINICS_JSON_PATH="$(pwd -W)/artifacts/clinics.json"`. O caminho é interpretado pelo processo que executa o script. No PowerShell, defina `$env:CLINICS_JSON_PATH = (Resolve-Path artifacts/clinics.json).Path`.

Os JSONs locais em `artifacts/` ficam fora do Git. Guarde o arquivo gerado do ambiente usado na demonstração para a entrega de Database; os scripts não substituem esse entregável nem o vídeo narrado.

### Comportamento da auditoria

Criação, atualização e exclusão bem-sucedidas de clínicas geram metadados com `clinicId`, `operation` (`CREATE`, `UPDATE` ou `DELETE`), `actorId`, `occurredAtUtc` e `traceId`. Não são copiados nomes, telefones, tokens ou chaves. Falhas de validação, registros ausentes e gravações relacionais que falharam não geram auditoria de sucesso.

A gravação no MongoDB é uma tentativa após o commit no Oracle. Falhas do driver são registradas sem os detalhes da conexão e não desfazem a alteração relacional nem mudam sua resposta de sucesso. Não há fila ou reenvio automático: a auditoria daquela operação pode se perder. Os timeouts de seleção de servidor, conexão e socket são de três segundos cada; uma falha pode acrescentar espera à requisição.

O health check faz `ping` no MongoDB e informa `Degraded` quando ele está indisponível, sem expor a mensagem interna da conexão. Esse ping não comprova permissão de escrita ou execução dos scripts. O dataset `Clinics` só muda quando você repete a exportação e a importação.

### Ordem de preparação do ambiente

1. Disponibilize um Oracle persistente e um usuário/schema da aplicação, compartilhado entre Java e .NET.
2. Gere uma vez o par RSA. Monte a privada e a pública na Java; monte somente a mesma pública na .NET.
3. Execute Java com Flyway habilitado e Hibernate em `validate`, após conferir o histórico do schema. Prepare e aplique as migrations clínicas EF somente quando pendentes e revisadas.
4. Disponibilize um MongoDB persistente e execute `create-collections.js` com a configuração do ambiente.
5. Inicie a .NET com Oracle, MongoDB e chave pública configurados. Confira `/health`, faça login na Java e teste uma operação autenticada.
6. Exporte o JSON e importe em MongoDB para a demonstração de Database.

O provisionamento App Service mantém a conexão com o Oracle da FIAP e recebe a URI do MongoDB Atlas preparado para a entrega. Não use H2 na execução final: ele fica restrito aos testes isolados de Java.

## Inicialização no Azure App Service

O script [scripts/start-appservice.sh](scripts/start-appservice.sh) prepara a execução nativa da API em **App Service Linux com .NET 9**, no ambiente **Production**. O provisionamento compartilhado está no [repositório Java](https://github.com/ClyvoPet-Challenge-2026/Java-Sprint-1#provisionamento-compartilhado); execute uma única vez para criar o plano e os dois Web Apps. A pipeline está descrita abaixo. A execução completa ainda precisa ser validada na Azure e no Azure DevOps.

Na raiz deste repositório, prepare o diretório de publicação:

```bash
dotnet publish ClyvoCare/ClyvoCare.API/ClyvoCare.API.csproj -c Release -o publish
mkdir -p publish/scripts
cp scripts/start-appservice.sh publish/scripts/
```

Publique o conteúdo de `publish`, incluindo `scripts/start-appservice.sh`, e configure o comando de inicialização do Web App:

```bash
bash /home/site/wwwroot/scripts/start-appservice.sh
```

Configure as variáveis nas **configurações de aplicativo** do Web App:

| Variável | Valor |
|---|---|
| `ConnectionStrings__ClyvoCareOracle` | Conexão com o schema compartilhado, por exemplo `Data Source=oracle.fiap.com.br:1521/ORCL;User Id=SEU_RM;Password=SUA_SENHA;` |
| `Mongo__ConnectionString` | URI do MongoDB remoto, incluindo autenticação; para Atlas, use a URI fornecida pelo serviço |
| `Mongo__DatabaseName` | Nome do banco MongoDB preparado com os scripts da entrega |
| `JWT_PUBLIC_KEY_BASE64` | Conteúdo completo do PEM público do mesmo par usado pela Java, codificado em Base64 em uma linha |
| `Jwt__Issuer` | `clyvocare-api`, correspondente ao emissor Java |
| `Cors__AllowedOrigins__0` | Origem HTTPS do frontend web; use índices seguintes para outras origens |
| `AllowedHosts` | Hostname público do Web App, sem protocolo ou caminho |

As quatro primeiras configurações são obrigatórias para o script. Use variáveis secretas para as credenciais no Azure DevOps e configurações protegidas no App Service. Não envie a chave privada para esta API e não inclua PEMs, credenciais ou configurações com segredos no Git ou nos artefatos. Base64 não é criptografia.

O script grava somente a chave pública em um diretório temporário privado e define `Jwt__PublicKeyPath` com o caminho de arquivo esperado pela .NET. O valor Base64 é removido do ambiente do processo .NET. Uma falha na preparação remove o arquivo criado; após iniciar a aplicação, ele permanece no diretório temporário da instância. A validação existente da aplicação continua recusando PEM privado, inválido ou RSA com menos de 2048 bits. Mantenha a mesma pública nas reinicializações; trocas precisam acompanhar o par Java.

A aplicação escuta HTTP na porta interna fornecida pela plataforma (`SERVER_PORT`, ou `PORT`, com padrão `8080`). O HTTPS público termina no proxy do App Service. O script habilita `AppService__Enabled`, e a aplicação processa `X-Forwarded-For` e `X-Forwarded-Proto` apenas de proxies em loopback ou nas redes privadas indicadas pela Microsoft, incluindo endereços IPv4 mapeados em IPv6. O processamento ocorre antes do redirecionamento HTTPS. O script desabilita a opção genérica `ASPNETCORE_FORWARDEDHEADERS_ENABLED`, que aceitaria qualquer proxy. Esse modo é específico para o App Service; outras topologias precisam da revisão das redes confiáveis.

Os logs continuam no console e passam a usar `/home/LogFiles/clyvocare-.log`; `Logging__FilePath` permite configurar outro caminho gravável. A aplicação permanece em Production. O provisionamento compartilhado configura `Swagger__Enabled=true` para disponibilizar a interface na raiz HTTPS e o contrato em `/swagger/v1/swagger.json`. Sem essa configuração, o Swagger permanece desabilitado em Production. Confira também `/health` e `/metrics` no ambiente publicado. O health check também depende do Oracle, MongoDB e das URLs externas já configuradas; a inicialização do processo não comprova acesso aos bancos, permissão de escrita ou importação do dataset. Este script não cria coleções nem aplica migrations.

Referências: [configuração ASP.NET Core no App Service](https://learn.microsoft.com/en-us/azure/app-service/configure-language-dotnetcore#detect-https-session) e [restrições dos cabeçalhos de proxies](https://learn.microsoft.com/en-us/aspnet/core/breaking-changes/8/forwarded-headers-unknown-proxies).

### CI/CD no Azure DevOps

A pipeline [azure-pipelines.yml](azure-pipelines.yml) executa a cada alteração na **master**, usando agente Linux `ubuntu-24.04` e SDK .NET 9. O CI verifica a inicialização e os scripts MongoDB, restaura os pacotes, executa os testes em Release, publica resultados TRX e gera o pacote de `dotnet publish`. O artefato `dotnet/dotnet.zip` fica no próprio Azure DevOps e contém a aplicação publicada e `scripts/start-appservice.sh`, sem pasta externa envolvendo o conteúdo.

O CD executa automaticamente após sucesso do CI na master: baixa o artefato **da mesma execução**, publica por Azure CLI e verifica `GET /api/estados` por HTTPS. Ele não recompila a aplicação nem reaplica migrations. Pull requests para master executam o CI, sem CD. Erros de testes, publicação ou verificação HTTP fazem a pipeline falhar.

Configure o projeto privado **Sprint 4 – Azure DevOps**, a conexão de serviço ARM `clyvocare-azure` e a concessão do agente conforme o [README Java](https://github.com/ClyvoPet-Challenge-2026/Java-Sprint-1#cicd-no-azure-devops). A conexão deve usar a assinatura do ambiente e escopo do grupo dedicado, preferencialmente com identidade federada. Autorize a pipeline .NET explicitamente. A existência de um pool não comprova minutos gratuitos; não compre paralelismo automaticamente.

Conecte esta pipeline ao repositório GitHub e selecione `/azure-pipelines.yml`. O YAML já define `azureResourceGroup: rm561584`; configure a variável não secreta `dotnetWebAppName`. Para outro ambiente, ajuste o grupo no YAML. Se a conexão tiver outro nome, ajuste `azureServiceConnection` no YAML. As credenciais de Oracle, Atlas e RSA permanecem nas configurações protegidas do Web App; os testes do CI usam SQLite, RSA temporária e objetos simulados, sem credenciais reais. O provisionamento não prepara o Atlas nem suas coleções: confira os scripts MongoDB e os IPs de saída permitidos antes de publicar.

Prepare a branch `master` preservando o histórico da versão validada. Somente adicionar o YAML em `develop` ou `main` não ativa o gatilho obrigatório. Para o vídeo, demonstre uma alteração real no código, os testes e artefatos no Azure DevOps, o CD automático, a aplicação pública e o CRUD com `SELECT` no Oracle. A configuração do projeto, o acesso Basic do professor, o diagrama e o PDF continuam sendo necessários. A execução real da pipeline e do deploy ainda está pendente.

Referências: [artefatos do Azure Pipelines](https://learn.microsoft.com/en-us/azure/devops/pipelines/artifacts/pipeline-artifacts) e [publicação de ZIP no App Service](https://learn.microsoft.com/en-us/azure/app-service/deploy-zip).

## Endpoints

As rotas de negócio começam com `/api`. As localidades e as rotas legadas de clínicas retornam arrays. `GET /api/clinicas` sem parâmetros preenchidos preserva esse formato; com filtros ou paginação, retorna uma página. A listagem de agendamentos também é paginada.

| Método | Rota | Uso | Respostas principais |
|---|---|---|---|
| `GET` | `/api/estados` | Listar estados | `200` |
| `GET` | `/api/estados/{id}` | Consultar um estado | `200`, `404` |
| `GET` | `/api/cidades` | Listar cidades com o estado aninhado | `200` |
| `GET` | `/api/cidades/{id}` | Consultar uma cidade | `200`, `404` |
| `GET` | `/api/clinicas` | Listar clínicas ou consultar página filtrada, com cidade, estado e links | `200`, `400` |
| `GET` | `/api/clinicas/{id}` | Consultar uma clínica | `200`, `404` |
| `GET` | `/api/clinicas/por-cidade/{cityId}` | Listar clínicas de uma cidade existente | `200`, `404` |
| `GET` | `/api/clinicas/buscar?nome=&cnpj=` | Buscar por nome parcial e/ou CNPJ exato | `200` |
| `GET` | `/api/clinicas/exportar` | Baixar o dataset relacional para MongoDB (`ADMIN`) | `200`, `401`, `403` |
| `POST` | `/api/clinicas` | Cadastrar uma clínica (`ADMIN`) | `201`, `400`, `401`, `403`, `404` |
| `PUT` | `/api/clinicas/{id}` | Atualizar os dados de uma clínica (`ADMIN`) | `204`, `400`, `401`, `403`, `404` |
| `DELETE` | `/api/clinicas/{id}` | Excluir uma clínica (`ADMIN`) | `204`, `401`, `403`, `404` |
| `POST` | `/api/agendamentos` | Agendar atendimento para pet próprio (`OWNER`) ou qualquer pet (`ADMIN`) | `201`, `400`, `401`, `403`, `404`, `409` |
| `GET` | `/api/agendamentos` | Listar os agendamentos visíveis para a conta | `200`, `400`, `401`, `403` |
| `GET` | `/api/agendamentos/{id}` | Consultar um agendamento visível | `200`, `400`, `401`, `403`, `404` |

### Criar e consultar agendamentos

Use o mesmo JWT do login Java e IDs retornados pelas APIs. O exemplo abaixo exige um pet da conta, uma clínica existente e uma data ainda futura no momento do envio:

```http
POST /api/agendamentos
Authorization: Bearer TOKEN_JAVA
Content-Type: application/json

{
  "petId": 1,
  "clinicId": 1,
  "eventType": "CONSULTATION",
  "scheduledAt": "2035-01-15T12:15:00Z",
  "reason": "Check-up Geral",
  "notes": "Observações opcionais do tutor"
}
```

`eventType` aceita `CONSULTATION`, `VACCINATION`, `SURGERY`, `FOLLOW_UP` ou `EXAM`. O horário deve ser uma string ISO 8601 com `Z` ou offset explícito, como `-03:00`. Horários sem fuso, datas passadas, motivo vazio ou maior que 150 caracteres e observações maiores que 1000 retornam `400`. Os detalhes são aparados e observações vazias viram nulo. O servidor define o status inicial como `SCHEDULED`.

A resposta `201` contém `id`, `petId`, `clinicId`, `eventType`, `scheduledAt`, `status`, `reason` e `notes`, além do cabeçalho `Location` para consultar o registro. A confirmação do mobile deve ocorrer somente depois desse retorno. A API salva no Oracle, normalizando o horário para UTC e precisão de microssegundos. Dois eventos do mesmo pet no mesmo instante retornam `409`, mesmo que o cliente use offsets diferentes; o índice único também protege a inserção após uma verificação concorrente. Falhas de persistência sem duplicidade não são convertidas em sucesso ou conflito fictício.

O tutor não pode criar eventos para outro tutor (`403`). Na leitura por ID, registros de outro tutor ficam ocultos com `404`. As listas também filtram pela identidade autenticada; `ADMIN` pode consultar todos. Referências inexistentes na criação retornam `404`, sem inserção.

```http
GET /api/agendamentos?petId=1&pageNumber=1&pageSize=20
Authorization: Bearer TOKEN_JAVA
```

A listagem retorna `items`, `pageNumber`, `pageSize`, `totalCount` e `totalPages`, ordenados por horário e ID. `petId` é opcional, as páginas começam em 1 e `pageSize` aceita de 1 a 100. Paginação inválida ou deslocamento acima do limite de inteiro retorna `400`. Sem resultados ou fora da última página, a resposta é `200` com lista vazia e as contagens correspondentes. Eventos legados podem ter `reason` e `notes` nulos.

Não há endpoints de edição/cancelamento de agendamento, lembretes futuros ou reserva de profissional/disponibilidade neste fluxo. A notificação do mobile permanece local e posterior à confirmação da criação.

### Cadastrar e atualizar uma clínica

Primeiro, consulte `GET /api/cidades` e escolha uma cidade existente. Use o ID retornado no campo `cityId`; o valor `1` abaixo é apenas um exemplo.

```http
POST /api/clinicas
Authorization: Bearer TOKEN_ADMIN_JAVA
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

### Filtros, ordenação e paginação das clínicas

O mobile continua usando `GET /api/clinicas` sem parâmetros, que retorna um array. Ao informar um parâmetro preenchido de busca ou paginação, a mesma rota retorna um objeto com `items`, `pageNumber`, `pageSize`, `totalCount`, `totalPages` e `_links`:

```bash
curl -i 'http://localhost:5067/api/clinicas?nome=Vet&cityId=1&pageNumber=1&pageSize=20&sortBy=name&sortDirection=asc'
```

| Parâmetro | Regra |
|---|---|
| `nome` | Trecho do nome, até 150 caracteres; ignora diferença entre maiúsculas e minúsculas |
| `cnpj` | Correspondência exata no formato `XX.XXX.XXX/XXXX-XX` |
| `cityId` | ID positivo da cidade |
| `pageNumber` | Padrão 1; positivo |
| `pageSize` | Padrão 20; de 1 a 100 |
| `sortBy` | `id` (padrão), `name`, `cnpj` ou `cityId` |
| `sortDirection` | `asc` (padrão) ou `desc` |

Os filtros são combinados com AND. Espaços externos são removidos; `%` e `_` no nome são caracteres literais, sem função de curinga. Não há normalização especial de acentos. A ordenação aceita os nomes indicados sem distinção de caixa e usa ID crescente para desempatar os demais campos. Filtros, ordenação, contagem e recorte são executados no banco.

Parâmetros inválidos ou deslocamento acima do limite de inteiro retornam `400`. Uma busca paginada sem resultados, inclusive por cidade inexistente, retorna `200` com lista vazia. Uma página fora do limite mantém o total encontrado e retorna `items` vazio. A contagem e a página usam consultas separadas; alterações concorrentes podem modificar os dados entre elas. As rotas `/buscar` e `/por-cidade/{cityId}` preservam o formato de array e suas respostas anteriores.

### Links de navegação (HATEOAS)

Cada clínica inclui `_links.self` e `_links.by-city`. Para `ADMIN`, também aparecem `_links.update` e `_links.delete`; as operações de alteração continuam protegidas por autorização no servidor. Os links têm `href` e `method`, por exemplo:

```json
{
  "self": { "href": "/api/clinicas/1", "method": "GET" },
  "by-city": { "href": "/api/clinicas/por-cidade/1", "method": "GET" }
}
```

As páginas incluem links `self`, `first` e `last`, além de `prev` e `next` quando aplicáveis. Eles preservam filtros, ordenação e tamanho de página. Os endereços são relativos à origem da API .NET e são gerados pelo roteamento. No Swagger, a resposta `200` dessa listagem usa `oneOf` para documentar tanto o array legado quanto a página.

Os testes verificam as oito combinações de ordenação, filtros combinados, desempate, limites, links navegáveis com caracteres codificados, permissões dos links e os dois formatos de resposta. As consultas de listagem, filtros, paginação e ordenação também foram conferidas por GET contra o Oracle compartilhado, sem gravações ou migrations; isso não substitui a validação do deploy e do fluxo completo no dispositivo.

## Monitoramento e observabilidade

A configuração está em [Program.cs](ClyvoCare/ClyvoCare.API/Program.cs).

### Health Checks

`GET /health` executa quatro verificações:

| Verificação | Dependência |
|---|---|
| `Oracle FIAP` | Oracle configurado em `ConnectionStrings:ClyvoCareOracle` |
| `MongoDB` | Ping no banco configurado em `Mongo:ConnectionString` e `Mongo:DatabaseName` |
| `FIAP` | `https://fiap.com.br` |
| `Google` | `https://google.com.br` |

O nome `Oracle FIAP` é fixo no código, mesmo quando a conexão aponta para outro Oracle. A resposta reúne o estado geral em `status`, a duração em `duration` e uma lista `checks`. Cada item traz `name`, `status`, `description`, `duration` e `error`.

O endpoint retorna `200` quando as verificações estão saudáveis e `503` em caso de falha crítica. Uma falha de credenciais Oracle ou indisponibilidade dos sites pode deixar o resultado geral como `Unhealthy`; confira `checks[].error` para identificar a dependência.

O teste cobre conectividade. Ele não confirma se todas as tabelas, dados ou operações de negócio estão corretas. Na versão atual, há somente `/health`, sem rotas separadas de readiness e liveness.

Se apenas o MongoDB estiver indisponível, seu resultado é `Degraded`; o padrão do ASP.NET Core mantém HTTP `200` nesse estado. Monitore também `status` e os itens de `checks`, além do código HTTP.

### Logs com Serilog

O Serilog registra mensagens no console e em arquivos diários `logs/clyvocare-.log`, relativos à pasta de execução. Para alterar o destino, configure `Logging__FilePath`; o script App Service usa `/home/LogFiles/clyvocare-.log`. O nível padrão é `Information`, com `Microsoft.AspNetCore` em `Warning`, conforme o `appsettings.json`.

O middleware `UseSerilogRequestLogging()` registra requisições HTTP. O middleware de contexto usa o ID W3C da atividade atual em `HttpContext.TraceIdentifier` e no contexto do Serilog. Console e arquivo exibem esse valor em `TraceId`, permitindo localizar os logs da mesma requisição.

A API devolve `X-Trace-Id` nas respostas. O mesmo valor aparece em `traceId` nos `ProblemDetails` do tratamento global e do MVC, incluindo validação automática, tanto em Development quanto em Production. Exceções inesperadas continuam com mensagem pública genérica. O CORS permite `traceparent`/`tracestate` e expõe `X-Trace-Id` às origens autorizadas.

### Métricas com OpenTelemetry e Prometheus

`GET /metrics` expõe métricas no formato Prometheus. A configuração usa o nome de serviço `ClyvoCare.API` e inclui instrumentação de ASP.NET Core, HttpClient e runtime do .NET.

Esses dados permitem acompanhar requisições, duração e recursos do processo. Para consultar:

```bash
curl -i http://localhost:5067/health
curl -i http://localhost:5067/metrics
```

### Tracing distribuído

O OpenTelemetry também usa `WithTracing`, com instrumentação de ASP.NET Core e HttpClient e fontes próprias `ClyvoCare.Application` e `ClyvoCare.Infrastructure`. Uma requisição pode mostrar, por exemplo, `GET api/clinicas` → `ClinicService.SearchPage` → `ClinicRepository.SearchPage`. Serviços de estados, cidades e agendamentos, repositórios compartilhados, consulta de identidade, auditoria e ping MongoDB também têm spans.

O contexto W3C recebido em `traceparent` é propagado pelas camadas e por chamadas HTTP de saída. `tracestate` é preservado. Sem um parent válido, o servidor gera um novo trace; o header `X-Trace-Id` não é usado como entrada. O sampling padrão respeita o parent: um trace recebido como não amostrado mantém a correlação, sem forçar sua exportação. Os scopes são encerrados ao retornar ou lançar uma exceção, restaurando a atividade anterior.

Os spans manuais registram nomes de operações, sem parâmetros SQL, IDs de contas, dados das clínicas, corpos, tokens, chaves ou connection strings. Valores da query HTTP de entrada e a URL completa de saída são removidos dos tags. Falhas tratadas do MongoDB marcam o span como erro, com o tipo da exceção; não são copiados sua mensagem ou stack trace. A auditoria mantém o mesmo trace ID, sem desfazer a gravação Oracle em caso de falha.

O tracing deste repositório cobre a .NET e a propagação de seu contexto HTTP. As APIs compartilham Oracle, mas esse compartilhamento não cria uma chamada HTTP nem um trace contínuo entre login Java e uma requisição .NET posterior. Outros serviços precisam participar do mesmo contexto para aparecer na mesma árvore.

### Enviar traces para um coletor

Não é necessário configurar um coletor para iniciar a API, usar os endpoints ou correlacionar logs e erros. Sem `OTEL_EXPORTER_OTLP_ENDPOINT`, não há exportação externa nem armazenamento dos traces para consulta posterior; `/metrics` continua disponível para Prometheus.

Para enviar traces a um coletor OTLP já disponível, defina as variáveis no terminal ou no ambiente do container antes de iniciar a API. Exemplo em Bash ou Git Bash, com OTLP por HTTP/protobuf:

```bash
export OTEL_EXPORTER_OTLP_ENDPOINT='http://localhost:4318'
export OTEL_EXPORTER_OTLP_PROTOCOL='http/protobuf'
dotnet run --project ClyvoCare.API --launch-profile http
```

Execute o comando acima na pasta `ClyvoCare`. No PowerShell, use `$env:OTEL_EXPORTER_OTLP_ENDPOINT = 'http://localhost:4318'` e `$env:OTEL_EXPORTER_OTLP_PROTOCOL = 'http/protobuf'`. O endpoint é a URL base: o exportador acrescenta `/v1/traces`. Se o coletor recebe gRPC, use protocolo `grpc` e a porta correspondente, normalmente 4317. Dentro de containers separados, substitua `localhost` pelo endereço acessível do coletor. O serviço aparece como `ClyvoCare.API`.

O exportador usa envio em lotes e as opções padrão do [exportador OTLP .NET](https://github.com/open-telemetry/opentelemetry-dotnet/blob/core-1.18.0/src/OpenTelemetry.Exporter.OpenTelemetryProtocol/README.md), incluindo `OTEL_EXPORTER_OTLP_HEADERS` e `OTEL_EXPORTER_OTLP_TIMEOUT` quando necessárias. Configure autenticação por segredo do ambiente. Falhas de exportação não alteram a resposta de negócio; o envio não garante retenção durante indisponibilidade. Instalação, armazenamento e interface de consulta do coletor fazem parte do ambiente de observabilidade.

Exemplo para conferir a propagação, com a API iniciada:

```bash
curl -i 'http://localhost:5067/api/clinicas?pageSize=1' \
  -H 'traceparent: 00-0123456789abcdef0123456789abcdef-0123456789abcdef-01'
```

O header `X-Trace-Id` da resposta deve conter `0123456789abcdef0123456789abcdef`. Com exportação habilitada, procure esse ID no coletor e confira os spans de aplicação e persistência. Nos testes automatizados, a árvore é capturada em memória e o transporte OTLP por HTTP/protobuf usa um receptor TCP local temporário. Isso valida o envio, sem comprovar um deploy de Jaeger, Grafana ou outro serviço na nuvem.

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

Os testes de domínio também verificam as invariantes dos agendamentos, UTC, precisão de microssegundos e limites de motivo/observações. A integração inclui persistência dos detalhes, leitura de eventos legados, índice único e restrição de exclusão com SQLite. Os testes de migration geram SQL com o provedor Oracle sem conectar ao banco e verificam o ID já aplicado, o snapshot e a exclusão das referências Java das migrations. Essas verificações não comprovam uma atualização ou concorrência real no Oracle.

Os testes seguem o padrão Arrange, Act, Assert e nomes que descrevem método, cenário e resultado. Os testes HTTP usam uma aplicação compartilhada por `CollectionFixture`.

Na pasta `ClyvoCare`, execute:

```bash
dotnet test ClyvoCare.sln
```

Os testes de hospedagem verificam HTTPS encaminhado por proxies privados, recusa de cabeçalhos de proxies públicos, preservação da execução local e Swagger desabilitado por padrão ou habilitado explicitamente em Production, mantendo a autenticação. Eles usam SQLite e dependências externas simuladas; não comprovam o funcionamento do proxy real da Azure.

Na raiz do repositório, execute também `bash scripts/start-appservice.test.sh`. Esse teste usa uma chave temporária e simula o executável .NET para conferir configurações obrigatórias, recusa de Base64/PEM inválido ou privado, porta da plataforma, diretório de publicação, logs, reinicialização com a mesma pública e propagação de falhas. No Linux, verifica permissões `600` do PEM e `700` do diretório. Ele não conecta aos bancos nem provisiona recursos.

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

Nos testes HTTP, a factory substitui o DbContext por SQLite, fornece contas controladas e gera um par RSA temporário. O middleware JWT real verifica os tokens; as permissões não são simuladas por um handler de autenticação alternativo. Os health checks externos são substituídos por respostas saudáveis controladas, sem acesso ao Oracle ou aos sites. Passar nesses testes não comprova disponibilidade real das dependências nem particularidades do provider Oracle.

Os testes de autenticação cobrem assinatura alterada, algoritmo incorreto, token sem assinatura, emissor/prazo inválidos, claims ausentes ou ambíguas, identidade divergente, conta desativada e autorização de consultas/escritas. Há também verificações das chaves de configuração e do contrato Swagger. A integração com o login Java e o Oracle reais exige uma validação separada no ambiente integrado.

Os testes de auditoria e health check Mongo usam objetos do driver simulados; os testes HTTP substituem o repositório de auditoria por uma implementação em memória. Não precisam de servidor MongoDB. Para conferir os scripts com objetos isolados de shell, na raiz do repositório, execute:

```bash
node docs/mongodb/scripts.test.cjs
```

Esse comando precisa de Node.js e não comprova importação em servidor real. Para a entrega, execute também os scripts com MongoDB e o JSON exportado do Oracle, confira contagens e documentos e demonstre as gravações de auditoria e a indisponibilidade.

O provisionamento M0 tem uma verificação isolada: `node scripts/provision-atlas.test.cjs`. Ela simula a CLI para conferir região gratuita, recusa de planos pagos ou projetos alheios, reutilização sem alterações, permissões dos usuários, IPs individuais e ausência de senhas nos argumentos e na saída. A pipeline executa essa verificação sem credenciais reais de Atlas.

## Tecnologias utilizadas

| Ferramenta | Papel |
|---|---|
| .NET 9 e ASP.NET Core | Execução da API e endpoints HTTP |
| Entity Framework Core 9 e Oracle.EntityFrameworkCore | Persistência e mapeamento do Oracle |
| MongoDB.Driver | Auditoria de clínicas em MongoDB |
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

Tracing distribuído e a correlação consistente dos identificadores entre logs e respostas ainda precisam ser implementados para completar esses pontos de observabilidade. A autenticação JWT compartilhada com Java e os testes de autorização já estão implementados.

Criação e consulta de agendamentos já estão disponíveis, com autenticação, isolamento por tutor e testes HTTP de persistência, validação e duplicidade. Lembretes e cache continuam como evolução prevista; as demais tabelas presentes no SQL não representam endpoints já disponíveis. A concorrência real e a integração completa com Java, Oracle e dispositivo ainda exigem validação no ambiente integrado.
