# Auditoria de clínicas no MongoDB

Após uma criação, atualização ou exclusão de clínica confirmada no banco relacional,
`ClinicService` envia um registro ao contrato `IClinicAuditRepository`. A implementação
na infraestrutura usa `MongoDB.Driver` e a coleção `ClinicAuditLogs`. O cliente MongoDB
é compartilhado pela aplicação via injeção de dependência.

Cada documento contém `_id` (identificador do evento), `clinicId`, `operation`
(`Created`, `Updated` ou `Deleted`), `actorId` (identidade autenticada),
`occurredAtUtc` (data BSON UTC) e `traceId` (quando houver contexto de tracing).
O registro contém metadados da operação, sem snapshot dos dados da clínica.
Exclusão de clínica inexistente e falhas de validação/persistência não geram eventos.

## Configuração

Em Development, `appsettings.Development.json` usa `mongodb://localhost:27017`
e o banco `clyvocare`. Para outros ambientes, configure obrigatoriamente:

```bash
export Mongo__ConnectionString='mongodb://localhost:27017'
export Mongo__DatabaseName='clyvocare'
```

Substitua a URI pelo servidor do ambiente e forneça credenciais por configuração
externa. O exemplo é para desenvolvimento local. Configuração ausente/inválida
é erro de configuração, não é tratada como indisponibilidade temporária.
A coleção é criada na primeira inserção, desde que a conta tenha permissão.
Não há migration Oracle adicional para a auditoria.

O driver usa seleção de servidor, conexão e socket com timeouts de três segundos,
confirmação `majority` com timeout e sem retry de escrita. Esses limites são por etapa,
não um prazo total de três segundos para toda a requisição.
Veja as [opções oficiais de conexão do driver](https://www.mongodb.com/docs/drivers/csharp/current/connect/connection-options/).

## Comportamento em falhas

A escrita no Oracle acontece antes da escrita MongoDB. Falhas do driver MongoDB ou
timeout são registradas em log estruturado; a operação da clínica mantém o resultado
da persistência relacional. O log inclui o ID do evento e TraceId, mas não a mensagem
completa do driver ou a URI. O span do repositório recebe status de erro.

Não há transação distribuída, outbox nem reenvio automático. Eventos podem ser
perdidos se o processo parar entre as escritas ou se o MongoDB falhar. Um timeout
também pode significar que a inserção ocorreu sem que sua confirmação chegasse.
Não considerar esta implementação uma trilha de auditoria com entrega garantida.

O check `MongoDB Audit` em `/health` executa `ping`: retorna `Healthy` quando acessível
e `Degraded` quando o driver reporta falha/timeout. Ping não comprova permissão de
inserção nem recuperação de eventos perdidos. Pelo comportamento padrão dos health
checks ASP.NET Core, `Degraded` pode responder HTTP 200; confira o corpo JSON.

## Validação pendente com .NET 9

1. Na pasta `ClyvoCare`, execute `dotnet restore`, `dotnet build --no-restore` e
   `dotnet test --no-build`.
2. Configure Oracle, JWT e MongoDB e inicie a API. Confira `MongoDB Audit` em `/health`.
3. Com token ADMIN, crie, atualize e exclua uma clínica de teste pelas rotas existentes.
4. No `mongosh`, selecione o banco configurado e consulte pelo ID retornado pela API:

   ```javascript
   db.ClinicAuditLogs.find({ clinicId: NumberLong("ID_DA_CLINICA") })
     .sort({ occurredAtUtc: 1 })
   ```

5. Confira os três eventos, identidade do administrador, datas UTC e TraceId.
6. Em ambiente de teste, torne o MongoDB indisponível e repita uma alteração:
   confira persistência Oracle, log de falha e health check degradado. Após restabelecer
   MongoDB, novas operações devem voltar a gerar documentos; não existe replay das antigas.

## Testes e evidências locais

`ClinicServiceTests` cobre metadados, atualização após persistência, exclusão inexistente,
falha relacional e falha de auditoria. `ClinicAuditRepositoryTests` cobre o BSON e o
tratamento de falhas com doubles do driver. Os testes HTTP substituem auditoria e check
MongoDB por doubles para permanecerem independentes de um servidor externo.

Na implementação, domínio/aplicação compilaram isoladamente com SDK 8 e oito verificações
de regras de auditoria passaram, além das duas verificações de tracing existentes.
Isso não executou xUnit, driver, HTTP ou servidor MongoDB. O build da solução .NET 9 e
as verificações reais acima continuam pendentes devido à falta de SDK/dependências.
