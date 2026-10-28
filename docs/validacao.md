# Validação completa da API

## Execução local

Instale o SDK .NET 9 completo (incluindo runtime ASP.NET Core 9) e disponibilize acesso
ao NuGet. O projeto continua em `net9.0`; não altere o target para contornar o ambiente.
Consulte a [instalação oficial do .NET no Linux](https://learn.microsoft.com/dotnet/core/install/linux).
O SDK pode ser instalado no diretório do usuário, sem alterar a instalação do sistema.

No terminal, a partir da raiz deste repositório:

```bash
dotnet --list-sdks
dotnet --list-runtimes
bash scripts/validate.sh
```

Se o SDK foi instalado em `~/.dotnet`, disponibilize-o no terminal antes de executar:

```bash
export DOTNET_ROOT="$HOME/.dotnet"
export PATH="$DOTNET_ROOT:$PATH"
bash scripts/validate.sh
```

O script verifica SDK e runtimes 9, restaura dependências, compila a solução em Release
e executa cada projeto de testes. As execuções geram diretórios próprios em
`artifacts/validation.*/`, com relatórios TRX separados por projeto. Saída 2 indica
pré-requisito ausente; falhas de restore/build/testes encerram a validação sem aprovação.
Se uma suíte falhar, as outras suítes ainda são executadas para coletar seus resultados.

Não são necessários credenciais de produção, Oracle, MongoDB ou coletor OTLP para esta
suíte. Os testes HTTP usam SQLite em memória, chaves RSA temporárias, auditoria substituída
e health checks de teste. A factory desabilita exportação OTLP e usa configuração Oracle
fictícia. Isso testa o contrato de `/health`, não a disponibilidade real das dependências.

`OracleMigrationTests` usa o provedor Oracle sem abrir conexão para verificar:

- Correspondência do modelo atual com o snapshot das migrations.
- SQL de evolução entre `InitialCreate` e `AddAppointmentDetails`, sem recriação de
  tabelas ou alteração das tabelas de tutores/pets.

Essas verificações não aplicam a migration nem comprovam seu funcionamento sobre dados
legados. Se falharem, confira os mapeamentos e o SQL antes de atualizar o snapshot.

## GitHub Actions

O workflow `.github/workflows/validate.yml` instala SDK 9, executa o mesmo script e
publica somente relatórios TRX como artefatos por sete dias. Usa as ações oficiais
[setup-dotnet](https://github.com/actions/setup-dotnet) e upload-artifact.

Os gatilhos preparados são pull request, push nas branches `main` e `teste/sprint-4`,
e execução manual. O arquivo precisa estar no repositório remoto para o GitHub
executá-lo; execução manual também depende de o workflow estar na branch padrão.
Não há deploy, migration ou gravação em bancos externos no workflow.

Nesta etapa o arquivo foi apenas criado localmente: nenhum push, publicação de
artefato ou execução remota foi realizado.

## Após a suíte passar

Validar separadamente, em ambiente de teste:

1. Schema Oracle compartilhado, snapshot, SQL da migration e dados legados de eventos
   (duplicidades de pet/data e fuso). Aplicar somente após revisar o SQL e os dados.
2. JWT emitido pela Java, permissões e persistência dos agendamentos:
   [agendamentos.md](agendamentos.md).
3. Paginação, filtros, tradução SQL e links: [clinicas.md](clinicas.md).
4. Auditoria e indisponibilidade MongoDB: [mongodb.md](mongodb.md).
5. Hierarquia dos spans e exportação ao coletor: [tracing.md](tracing.md).
6. Integração do Mobile aos endpoints validados.

## Estado local em 27/10/2026 — atualizado

SDK 9.0.122 disponível. O usuário executou a validação completa; os TRX de
`artifacts/validation.Z7vB6V` foram conferidos: 42 testes Application, 25 Domain e
44 Integration aprovados, sem falhas. O assistente também compilou a solução com
`dotnet build ClyvoCare.sln --no-restore --disable-build-servers -m:1`, sem erros ou avisos.

A reexecução dos testes pelo assistente foi bloqueada pela restrição de sockets do
ambiente (`SocketException: Permission denied` no VSTest). A evidência dos 111 testes
aprovados vem dos relatórios da execução do usuário. Bancos reais, JWT emitido pela
Java, migration aplicada e OTLP continuam exigindo validação integrada.
