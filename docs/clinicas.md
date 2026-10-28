# Consultas de clínicas

As consultas continuam públicas. As rotas existentes sem paginação preservam o array
JSON: `GET /api/clinicas`, `GET /api/clinicas/por-cidade/{cityId}` e
`GET /api/clinicas/buscar?nome=...&cnpj=...`. Cada clínica passa a incluir `_links`.

## Consulta paginada

Enviar ao menos um dos parâmetros abaixo preenchido em `GET /api/clinicas` seleciona
o formato paginado. Os demais parâmetros recebem os padrões indicados.

| Parâmetro | Padrão | Regra |
|---|---|---|
| `pageNumber` | `1` | Inteiro positivo; deslocamento deve caber em Int32 |
| `pageSize` | `20` | Entre 1 e 100 |
| `sortBy` | `id` | `id`, `name`, `cnpj` ou `cityId` |
| `sortDirection` | `asc` | `asc` ou `desc` |
| `nome` | Ausente | Trecho literal do nome, sem distinção de maiúsculas/minúsculas |
| `cnpj` | Ausente | Correspondência exata, incluindo a formatação |
| `cityId` | Ausente | ID positivo da cidade |

Os filtros combinam-se com AND. Espaços externos são removidos. Campos de ordenação
e direção são normalizados sem distinção de caixa. Acentuação segue o comportamento
do banco; não há normalização de acentos. Ordenação usa apenas os campos permitidos,
com ID crescente como desempate. Filtros, contagem, ordenação e paginação são aplicados
no banco, antes de materializar os resultados.

Exemplo: `GET /api/clinicas?pageNumber=1&pageSize=10&sortBy=name&sortDirection=asc&cityId=1&nome=Vet`

A resposta possui `items`, `pageNumber`, `pageSize`, `totalCount`, `totalPages` e
`_links`. `items` contém clínicas com os mesmos campos do contrato anterior
(`id`, `name`, `cnpj`, `phone`, `city`), acrescidos de `_links`.

Parâmetros inválidos retornam 400. Filtros sem correspondência retornam 200 com
`items: []`, `totalCount: 0` e `totalPages: 0`, inclusive cidade inexistente.
Uma página além do fim retorna array vazio, mantendo o total filtrado.
A rota legada `/por-cidade/{cityId}` continua retornando 404 para cidade inexistente.

Contagem e leitura são consultas separadas: alterações concorrentes podem mudar o
conteúdo entre páginas; esta paginação não oferece um snapshot da coleção.

## Links HATEOAS

Cada link tem `href` (caminho relativo à origem da API) e `method` (verbo HTTP).
O cliente deve resolver o `href` contra a origem da API .NET, não contra a API Java.
Os caminhos são gerados pelo roteamento ASP.NET Core, incluindo o PathBase configurado.

- Clínicas: `self` e `by-city` para qualquer visitante; `update` e `delete` apenas
  quando o principal autenticado tem o papel ADMIN.
- Página: `self`, `first` e `last`; `prev` e `next` quando há páginas navegáveis.
- Para resultados vazios, `first`/`last` apontam à página 1 e não há `prev`/`next`.
- Para página além do fim, `prev` aponta à última página disponível.

Os links de página preservam tamanho, ordenação e filtros. A ausência de links de
escrita não substitui autorização: PUT/DELETE continuam protegidos por ADMIN.
O POST também retorna a clínica enriquecida com links, mantendo Location e status 201.

O Swagger descreve os dois formatos do GET principal com `oneOf` (array ou página).
Para clientes novos, envie ao menos `pageNumber=1` para usar sempre o formato paginado.
Não é necessário alterar o Mobile nesta etapa; sua integração real continua pendente.

## Validação

Testes preparados em `ClinicSearchTests` e `ClinicSearchEndpointsTests` cobrem
validação antes do banco, normalização, totais, filtros combinados, desempate,
navegação preservando caracteres especiais, páginas vazias/fora do intervalo,
compatibilidade dos arrays e links por papel.

Domínio/aplicação e o controller foram compilados isoladamente com SDK 8. Quinze
verificações de regras, serviço e serialização passaram em um harness temporário.
O controller não foi executado: o ambiente também não possui runtime ASP.NET Core 8.
Isso não substitui os testes HTTP/EF/Swagger. Com SDK .NET 9 e dependências restauradas,
execute `dotnet build` e `dotnet test` na solução e confirme a consulta no Oracle.
Não houve alteração de schema nesta etapa.
