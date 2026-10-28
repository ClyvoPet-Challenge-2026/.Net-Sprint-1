# Tracing distribuído

A API usa a instrumentação ASP.NET Core e HttpClient do OpenTelemetry. As fontes
`ClyvoCare.Application` e `ClyvoCare.Infrastructure` acrescentam spans aos serviços
e às operações de repositório. O contexto W3C recebido em `traceparent` conecta
a requisição aos spans internos; chamadas feitas com HttpClient propagam o contexto.

Os spans manuais registram apenas nomes de operações, sem incluir IDs de tutores,
e-mails, tokens, motivos de consulta, parâmetros SQL ou connection strings.
São spans de operação de repositório, não spans individuais de comandos SQL.
Exceções inesperadas mapeadas para HTTP 500 marcam o span HTTP como erro.
Em desenvolvimento, o ProblemDetails e o log de erro usam o mesmo TraceId.

## Exportação OTLP

A exportação é optativa para permitir execução local sem coletor. Configure no
ambiente do processo da API:

```bash
export Telemetry__OtlpEnabled=true
export OTEL_EXPORTER_OTLP_ENDPOINT=http://localhost:4317
export OTEL_EXPORTER_OTLP_PROTOCOL=grpc
```

O destino deve ser um coletor OTLP ou backend compatível, como Jaeger com receptor
OTLP habilitado. Em containers, use o endereço do coletor acessível pela API.
Endpoint, protocolo e autenticação seguem as variáveis padrão do
[exportador OTLP](https://github.com/open-telemetry/opentelemetry-dotnet/blob/main/src/OpenTelemetry.Exporter.OpenTelemetryProtocol/README.md).
Não coloque credenciais no repositório. `/metrics` continua disponível para Prometheus.

## Validação no ambiente .NET 9

1. Execute `dotnet restore`, `dotnet build --no-restore` e `dotnet test --no-build`
   no diretório `ClyvoCare`.
2. Inicie a API com Oracle e JWT configurados conforme o README e com as variáveis
   OTLP acima apontando para o coletor ativo.
3. Faça uma consulta `GET /api/clinicas` usando a URL da API e o cabeçalho
   `traceparent: 00-11111111111111111111111111111111-2222222222222222-01`.
4. No Jaeger, procure o serviço `ClyvoCare.API` e o TraceId
   `11111111111111111111111111111111`. Confira a hierarquia HTTP →
   `ClinicService.GetAll` → `ClinicRepository.GetAllWithCity`.
5. Com token válido, crie e consulte um agendamento e confira os spans de
   `AppointmentService` e `AppointmentRepository` no mesmo trace da requisição.

Os testes `TracingTests` verificam a propagação do trace até o serviço/repositório
substituto e a restauração do contexto tanto em sucesso quanto em exceção.
Eles não substituem a validação HTTP, Oracle e exportação OTLP descrita acima.

## Estado da verificação local

O ambiente de implementação só possui SDK 8. A compilação isolada de domínio e
aplicação e duas verificações de contexto foram executadas em um projeto temporário
com esse SDK, sem alterar o target da solução. A suíte xUnit, a API, os repositórios
EF e a entrega de traces ao coletor ainda precisam ser validados com .NET 9.
