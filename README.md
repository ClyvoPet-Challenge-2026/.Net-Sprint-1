# ClyvoCare API (.NET) — Sprint 1

API REST em ASP.NET Core que faz parte do projeto **ClyvoCare**, desenvolvido como Challenge FIAP 2026 (2TDSPG). O ClyvoCare é um sistema de convênio veterinário digital inspirado em serviços como o Petlove Saúde, com foco em continuidade do cuidado do pet (cadastro de tutores, planos, agendamentos, lembretes).

Essa API é a metade da operação clínica do sistema. Ela compartilha o banco Oracle FIAP com uma API em Java (irmã desse projeto) que cuida do cadastro e contratação.

## Por que duas APIs?

A divisão entre as duas APIs é feita por **bounded context** (contexto de negócio) e não por questão técnica. A ideia é simples: cada API é dona de um pedaço claro do domínio, e cada uma escreve só nas tabelas do seu pedaço, lendo as do outro lado quando precisa.

- A **API Java** é dona das tabelas de cadastro estável: tutores (`TB_CAD_OWNER`), pets (`TB_CAD_PET`), planos (`TB_CAD_PLAN`), contratações (`TB_CAD_SUBSCRIPTION`), além das tabelas de localização (`TB_CAD_STATE`, `TB_CAD_CITY`) e dos lookups (`TB_CAD_SPECIES`, `TB_CAD_BREED`, `TB_CAD_PAYMENT_METHOD`, `TB_CAD_SUB_STATUS`).
- A **API .NET (esta)** é dona da operação clínica: clínicas veterinárias (`TB_CAD_CLINIC`), eventos clínicos (`TB_HEA_CLINICAL_EVENT`) e lembretes (`TB_HEA_REMINDER`).

Essa separação faz com que cada API tenha um propósito claro: a Java cuida de quem somos (cadastros que mudam pouco), e a .NET cuida do que fazemos pelo pet (jornada de atendimento). O Oracle FIAP entra no meio das duas como banco compartilhado.

## O que essa API tem hoje (Sprint 1)

Nessa sprint a gente fechou a parte mais estável da .NET, que é o que dá pra começar a expor pra um front:

- **State** e **City** — leitura completa (vêm da API Java)
- **Clinic** — CRUD completo, com validação de FK pra City e CNPJ único

As outras entidades que a .NET vai escrever (ClinicalEvent e Reminder) ficaram pra próxima sprint.

## Stack

| Camada | Tecnologia |
|---|---|
| Linguagem | C# 13 |
| Framework | ASP.NET Core 9 |
| ORM | Entity Framework Core 9 |
| Banco | Oracle 19c (FIAP) |
| Provider Oracle | Oracle.EntityFrameworkCore 9.23.80 |
| Documentação | Swashbuckle (Swagger UI) |
| Build | .NET SDK 9 |
| Versionamento | Git + git-flow |

## Como rodar

Pré-requisitos:
- .NET SDK 9 instalado
- VPN da FIAP conectada (sem ela o `oracle.fiap.com.br` não é alcançado)
- Credenciais do banco Oracle FIAP (RM + senha)
- Schema da FIAP populado pelo arquivo `fix.sql` (rode no SQL Developer logado com seu RM)

Configurar credenciais. Abre `ClyvoCare/ClyvoCare.API/appsettings.json` e troca o `xxxx`:

```json
"ConnectionStrings": {
  "ClyvoCareOracle": "Data Source=oracle.fiap.com.br:1521/ORCL;User Id=SEU_RM;Password=SUA_SENHA;"
}
```

Rodando:

```bash
cd ClyvoCare
dotnet restore
dotnet run --project ClyvoCare.API
```

A API sobe em `http://localhost:5067` por padrão, e o Swagger UI fica na raiz (`http://localhost:5067/`).

Se quiser rodar a migration de baseline (já aplicada no banco da FIAP):

```bash
dotnet ef database update --project ClyvoCare.Infrastructure --startup-project ClyvoCare.API
```

## Estrutura de pastas

A solution segue Clean Architecture clássica em quatro projetos:

```
ClyvoCare/
├── ClyvoCare.Domain/         # entidades, exceptions, BaseEntity
├── ClyvoCare.Application/    # DTOs, interfaces de repositório, services
├── ClyvoCare.Infrastructure/ # DbContext, Configurations EF, repositórios, migrations
└── ClyvoCare.API/            # Controllers, Program.cs, exception handler global
```

A regra é a dependência aponta sempre pra dentro: Domain não conhece ninguém, Application conhece Domain, Infrastructure conhece Application + Domain, e API conhece todo mundo via DI.

## Rotas

Base path: `/api/`

### State (somente leitura)
| Método | Rota | Descrição | Status |
|---|---|---|---|
| GET | `/api/estados` | Lista todos os estados | 200 |
| GET | `/api/estados/{id}` | Estado por id | 200, 404 |

### City (somente leitura)
| Método | Rota | Descrição | Status |
|---|---|---|---|
| GET | `/api/cidades` | Lista todas as cidades com estado aninhado | 200 |
| GET | `/api/cidades/{id}` | Cidade por id | 200, 404 |

### Clinic (CRUD completo)
| Método | Rota | Descrição | Status |
|---|---|---|---|
| GET | `/api/clinicas` | Lista todas as clínicas | 200 |
| GET | `/api/clinicas/{id}` | Clínica por id | 200, 404 |
| GET | `/api/clinicas/por-cidade/{cityId}` | Clínicas de uma cidade | 200, 404 |
| GET | `/api/clinicas/buscar?nome=&cnpj=` | Busca por nome (parcial) e/ou CNPJ (exato) | 200 |
| POST | `/api/clinicas` | Cria clínica | 201, 400, 404 |
| PUT | `/api/clinicas/{id}` | Atualiza clínica | 204, 400, 404 |
| DELETE | `/api/clinicas/{id}` | Remove clínica | 204, 404 |

### Health
| Método | Rota | Descrição | Status |
|---|---|---|---|
| GET | `/api/healthcheck` | Verifica se a API está no ar | 200 |

Documentação interativa completa fica no Swagger em `http://localhost:5067/`.

### Exemplo: criar uma clínica

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

Resposta `201 Created` traz a clínica criada com a cidade e o estado aninhados:

```json
{
  "id": 23,
  "name": "Vet Care Center",
  "cnpj": "99.999.999/0001-99",
  "phone": "(11) 99999-9999",
  "city": {
    "id": 1,
    "name": "Sao Paulo",
    "state": { "id": 1, "name": "Sao Paulo", "uf": "SP" }
  }
}
```

Se o `cityId` não existir, volta `404` com mensagem amigável. Se o CNPJ já estiver cadastrado, volta `400`.

## Decisões importantes do projeto

Algumas escolhas que valem ser explicadas, principalmente as que não são óbvias só lendo o código.

**Por que Clean Arch em quatro projetos.** A gente seguiu o estilo de uma API de referência (Recommenda) que o professor de .NET passou na disciplina. A vantagem prática é que o Domain fica sem dependência de framework — dá pra mover ele de projeto a hora que quiser. Pra Sprint 1 é meio overkill, mas a ideia é que conforme a API cresce o benefício aparece.

**Entidades com `Create` e `Update` estáticos.** A entidade `Clinic` não tem um construtor público — só o factory `Clinic.Create(...)` e o método `Update(...)`. Os dois validam invariantes do domínio (nome não vazio, cnpj não vazio, cityId positivo) e lançam `DomainException` se algo estiver errado. Isso garante que toda instância de Clinic em memória já passou pela validação.

**Validação de FK no Service, não no banco.** Antes de inserir uma clínica, o `ClinicService` consulta o `IRepository<City>` pra ver se o `cityId` existe. Se não existir, lança `KeyNotFoundException` que o handler global transforma em `404` com mensagem amigável. Sem isso, o Oracle ia gritar `ORA-02291: integrity constraint violated` e o usuário ia receber um JSON feio de erro técnico.

**CNPJ único validado no Service também.** Mesmo padrão: o `ExistsByCnpj` no repositório roda antes do insert/update. Se já existir, lança `InvalidOperationException` → vira `400` no handler com mensagem clara.

**Tratamento global de erros via `IExceptionHandler`.** A classe `GlobalExceptionHandler` (registrada em `Program.cs`) intercepta qualquer exceção não tratada e mapeia pro status HTTP correto, retornando um `ProblemDetails` consistente:
- `KeyNotFoundException` → `404`
- `InvalidOperationException` / `DomainException` / `ArgumentException` → `400`
- Demais → `500`

**Repositório genérico + especializado quando precisa de Include.** O `Repository<T>` cobre os métodos básicos (GetAll, GetById, Add, Update, Delete, ExistsById). Quando uma entidade precisa de eager loading (tipo Clinic, que sempre vai aninhar a City no JSON de resposta), a gente cria um repositório especializado (`ClinicRepository`) que herda do genérico e adiciona métodos com `Include` + `ThenInclude`.

**Migration como baseline vazio.** O schema do banco já vem do `fix.sql` (que carrega 14 tabelas, procedures e seed). Por isso a migration `InitialCreate` tem `Up()` e `Down()` vazios — ela serve só pra registrar um ponto de partida no `__EFMigrationsHistory`, pra que as próximas migrations (quando entrarem ClinicalEvent e Reminder) funcionem normalmente. Se a gente deixasse a migration com `CreateTable` de verdade, o `database update` no banco da FIAP ia falhar dizendo que as tabelas já existem.

**Workaround do `ExistsById` com `.Count()` em vez de `.Any()`.** O provider `Oracle.EntityFrameworkCore` traduz `.Any()` num `CASE WHEN EXISTS ... THEN True ELSE False`, e Oracle não tem literal booleano em SQL, então quebra com `ORA-00904: "FALSE": invalid identifier`. Trocar pra `.Count(x => ...) > 0` gera `SELECT COUNT(*)` que sempre funciona.

**Rotas em português.** A gente alinhou os nomes dos endpoints com o que a API Java usa (`/estados`, `/cidades`, `/clinicas`), pra um front-end que consuma as duas não precisar lembrar que uma é PT e outra EN. O professor da disciplina usava `[Route("api/[controller]")]` no Recommenda (que vira `/api/Genre`, `/api/User`), mas como o Java já tinha definido o padrão em PT, mantivemos.

## Limitações conhecidas

A gente é honesto sobre o que não está pronto nessa sprint:

- **Sem autenticação.** Nenhum endpoint pede token, qualquer um consegue criar/atualizar/deletar clínicas. JWT entraria numa sprint futura.
- **Sem cache.** A API Java cacheia lookups estáveis (estados, cidades) com `@Cacheable`. A .NET ainda não — toda chamada bate no banco. Vale adicionar depois com `IMemoryCache`.
- **ClinicalEvent e Reminder não implementados.** As tabelas existem no `fix.sql` mas a API ainda não expõe nada delas. Ficou pra Sprint 2.
- **Sem testes automatizados.** Só validamos manualmente via Swagger.
- **`Repository<T>.Update` recebe entidade detached.** Funciona, mas não é o padrão mais idiomático do EF (que prefere entidades tracked sendo modificadas direto). Pra Sprint 1 tá bom.

## Próximos passos

- ClinicalEvent CRUD (com validação de FK pra Clinic e PET_ID via API Java)
- Reminder CRUD
- Autenticação via JWT
- Cache em rotas de leitura estáveis
- Testes de integração com TestContainers
- README documentando ClinicalEvent e Reminder quando entrarem
