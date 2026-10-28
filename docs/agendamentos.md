# Agendamentos — contrato da API .NET

Implementação na branch `teste/sprint-4`. Segue API → Application → Domain/Infrastructure. O Mobile ainda não envia agendamentos. A compilação completa e os testes de integração requerem SDK .NET 9 e dependências NuGet; ainda não foram executados neste ambiente.

## Identidade e configuração

As APIs devem usar o mesmo banco/schema Oracle. A Java mantém tutores e pets; a .NET consulta essas tabelas para conferir a identidade e a propriedade. Nenhum endpoint de escrita de tutores/pets foi adicionado à .NET.

A Java emite o JWT em `/auth/login`. A .NET valida assinatura RS256, issuer `clyvocare-api`, expiração e os claims `sub` (e-mail), `ownerId` e `role`, comparando-os com uma conta ativa no banco compartilhado. Tokens antigos sem `ownerId` exigem novo login. A Java não emite audience; por isso essa validação está desativada explicitamente na .NET.

Configure a chave **pública do mesmo par RSA usado pela Java**:

```bash
export Jwt__PublicKeyPath='/caminho/absoluto/public_key.pem'
# Opcional; precisa corresponder ao issuer da Java:
export Jwt__Issuer='clyvocare-api'
```

A .NET não precisa da chave privada. Sem a variável, usa `.keys/public_key.pem` a partir do diretório de execução. Não gere um par diferente para a .NET.

## Criar

`POST /api/agendamentos`, com `Authorization: Bearer <token>` de OWNER ou ADMIN.

```json
{
  "petId": 1000,
  "clinicId": 100,
  "eventType": "CONSULTATION",
  "scheduledAt": "2030-10-27T09:00:00-03:00",
  "reason": "Check-up Geral",
  "notes": "Avaliação preventiva"
}
```

Use IDs reais de `/pets` na Java e `/api/clinicas` na .NET e uma data futura. O cliente não informa tutor, status ou ID do agendamento.

- `eventType`: `CONSULTATION`, `VACCINATION`, `SURGERY`, `FOLLOW_UP` ou `EXAM`, conforme os valores já aceitos pelo banco.
- `scheduledAt`: data/hora ISO 8601 com `Z` ou offset explícito. Horário sem fuso é rejeitado. A persistência usa UTC com precisão de microssegundos (`TIMESTAMP(6)`).
- `reason`: obrigatório, até 150 caracteres. Preserva o motivo apresentado no Mobile, separado da classificação clínica e das observações.
- `notes`: opcional, até 1000 caracteres; vazio é armazenado como nulo.
- OWNER só cadastra para pet próprio; ADMIN pode cadastrar para qualquer pet.
- O status inicial é definido pelo servidor como `SCHEDULED`.
- Dois eventos do mesmo pet no mesmo instante são rejeitados, independentemente da clínica e do status. O índice único protege essa regra; isso não representa reserva de profissional ou controle da capacidade da clínica.

Resposta `201 Created`, com cabeçalho `Location: /api/agendamentos/{id}`:

```json
{
  "id": 42,
  "petId": 1000,
  "clinicId": 100,
  "eventType": "CONSULTATION",
  "scheduledAt": "2030-10-27T12:00:00+00:00",
  "status": "SCHEDULED",
  "reason": "Check-up Geral",
  "notes": "Avaliação preventiva"
}
```

## Consultar

- `GET /api/agendamentos/{id}`: retorna um agendamento visível para a conta. Recurso inexistente ou de outro tutor retorna `404`.
- `GET /api/agendamentos?petId=1000&pageNumber=1&pageSize=20`: retorna `items`, `pageNumber`, `pageSize`, `totalCount` e `totalPages`. `petId` é opcional; tamanho da página entre 1 e 100. Ordenação por data crescente e ID como desempate.
- OWNER vê somente eventos dos próprios pets; ADMIN pode consultar todos. O filtro de propriedade é aplicado antes da contagem e paginação.
- A listagem inclui eventos existentes com status `SCHEDULED`, `COMPLETED` ou `CANCELED`. Não há endpoint de edição/cancelamento nesta etapa.

## Erros e permissões

| Status | Situação |
|---|---|
| 400 | Campos inválidos, ausência de fuso, horário não futuro ou paginação inválida |
| 401 | Token ausente/inválido/expirado ou conta incompatível/inativa |
| 403 | Tutor tenta cadastrar para pet alheio ou alterar clínicas |
| 404 | Pet/clínica inexistente; agendamento inexistente ou não visível |
| 409 | Evento duplicado para o mesmo pet e instante |

Os erros de negócio usam `ProblemDetails` (`title`, `detail`, `status`); erros de validação MVC podem incluir `errors`. GETs de clínicas, estados e cidades permanecem públicos. POST/PUT/DELETE de clínicas agora exigem ADMIN.

## Banco e migration

A tabela `TB_HEA_CLINICAL_EVENT` já existe no schema compartilhado, preparado pelo script de baseline ou Flyway da Java. A migration `.NET` `20261026190000_AddAppointmentDetails` acrescenta `REASON` (`NVARCHAR2(150)`), `NOTES` (`NVARCHAR2(1000)`), índice de clínica e índice único `(PET_ID, EVENT_DATE)`. As novas colunas aceitam nulo para preservar registros legados; a API exige motivo em novos cadastros.

Tutores/pets são projeções de leitura excluídas das migrations EF. A migration não recria as tabelas Java nem altera seu histórico Flyway. O `docs/script.sql` continua sendo o baseline legado; após preparar um schema novo, aplique a migration EF também. Não execute o script destrutivo para atualizar um banco em uso.

Antes de aplicar no Oracle:

1. Conferir a existência do baseline e os históricos EF/Flyway do schema correto.
2. Auditar duplicidades, sem apagar registros automaticamente:

   ```sql
   SELECT PET_ID, EVENT_DATE, COUNT(*)
   FROM TB_HEA_CLINICAL_EVENT
   GROUP BY PET_ID, EVENT_DATE
   HAVING COUNT(*) > 1;
   ```

3. Confirmar o fuso dos eventos existentes. `EVENT_DATE` não armazena fuso; a API interpreta os valores como UTC. Esta migration não converte datas legadas. Se foram gravadas em hora local, alinhar e executar a conversão antes de disponibilizar a listagem.
4. Gerar e revisar o SQL da migration e conferir o snapshot com as ferramentas EF 9:

   ```bash
   cd ClyvoCare
   dotnet restore
   dotnet build
   dotnet test
   dotnet ef migrations has-pending-model-changes --project ClyvoCare.Infrastructure --startup-project ClyvoCare.API
   dotnet ef migrations script 20260523133532_InitialCreate 20261026190000_AddAppointmentDetails --project ClyvoCare.Infrastructure --startup-project ClyvoCare.API --output /tmp/clyvocare-agendamentos.sql
   ```

5. Após revisão e configuração do banco/chave pública, aplicar na pasta `ClyvoCare`, usando o mesmo ambiente e a mesma conexão Oracle da API:

   ```bash
   dotnet ef database update 20261026190000_AddAppointmentDetails \
     --project ClyvoCare.Infrastructure \
     --startup-project ClyvoCare.API \
     -- --environment Development
   ```

   Se `dotnet ef` não estiver instalado, instale a ferramenta compatível com o EF Core 9 do projeto: `dotnet tool install --global dotnet-ef --version 9.0.0`. No Linux, inclua `$HOME/.dotnet/tools` no `PATH` caso necessário.

6. Confirme no mesmo schema Oracle:

   ```sql
   SELECT COLUMN_NAME, DATA_TYPE, CHAR_LENGTH
   FROM USER_TAB_COLUMNS
   WHERE TABLE_NAME = 'TB_HEA_CLINICAL_EVENT'
     AND COLUMN_NAME IN ('REASON', 'NOTES');
   ```

   O resultado deve incluir `REASON` e `NOTES`. No mobile, faça um agendamento com motivo e observações e confira o retorno `201` e a leitura posterior via `GET /api/agendamentos/{id}`. Motivo é obrigatório; observações são opcionais, com limite de 1000 caracteres. O mobile já envia ambos. A API não aplica migrations automaticamente ao iniciar; `ORA-00904: "REASON"` ao salvar indica que o schema acessado não contém a coluna esperada. Não execute o baseline destrutivo para corrigir esse erro.

O snapshot e o designer foram preparados junto com a migration, mas não puderam ser conferidos pelo EF 9 neste ambiente. Nenhum comando de migration foi executado contra banco real. O rollback da migration remove as novas colunas e seus dados.

## Integração Mobile — próxima etapa

Manter clientes HTTP separados para Java e .NET, reutilizando o token da sessão. Pets vêm da Java; clínicas e agendamentos, da .NET. Substituir o alerta demonstrativo por mutação POST, invalidar a lista e só confirmar após `201`.

Mapeamento proposto para os motivos atuais: `checkup`, `emergency`, `specialist` e `dental` → `CONSULTATION`; `vaccine` → `VACCINATION`; `return` → `FOLLOW_UP`. Enviar o título completo em `reason`, preservando a escolha. Datas e horários fixos do app precisam ser substituídos por datas reais; não há endpoint de disponibilidade da clínica nesta etapa.

## Validação realizada e pendente

- Compilação isolada de Domain/Application, controller de agendamentos, acesso aos claims e conversor de data com SDK 8, sem alterar o target `net9.0` dos projetos.
- 29 verificações isoladas das regras e do conversor executadas com sucesso; não usam EF, JWT real ou HTTP.
- Testes xUnit adicionados ao domínio, aplicação e integração, incluindo assinatura/issuer/expiração, permissões, persistência, filtros e duplicidade. Os testes anteriores de clínicas foram adaptados para token ADMIN.
- **Pendente:** build/test completos em .NET 9, revisão EF, aplicação/validação da migration no Oracle e integração em dispositivo. O ambiente só possui SDK 8 e não resolve os hosts para baixar SDK/dependências.
