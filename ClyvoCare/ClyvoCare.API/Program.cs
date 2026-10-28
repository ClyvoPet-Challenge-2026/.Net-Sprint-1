using ClyvoCare.API.Swagger;
using ClyvoCare.Application.Diagnostics;
using ClyvoCare.Infrastructure.Diagnostics;
using OpenTelemetry.Trace;
using System.Reflection;
using ClyvoCare.API.Exceptions;
using ClyvoCare.API.Extensions;
using ClyvoCare.API.Health;
using ClyvoCare.API.Serialization;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.OpenApi.Models;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using Serilog;

namespace ClyvoCare.API;

public partial class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        builder.Host.UseSerilog((context, configuration) => configuration
            .ReadFrom.Configuration(context.Configuration)
            .Enrich.FromLogContext()
            .WriteTo.Console()
            .WriteTo.File("logs/clyvocare-.log", rollingInterval: RollingInterval.Day));

        builder.Services.AddClyvoCareDbContext(builder.Configuration);

        builder.Services.AddClyvoCareRepositories();
        builder.Services.AddClyvoCareMongo(builder.Configuration);

        builder.Services.AddClyvoCareApplicationServices();

        builder.Services.AddClyvoCareAuthentication(builder.Configuration);

        builder.Services.AddControllers().AddJsonOptions(options =>
            options.JsonSerializerOptions.Converters.Add(new AppointmentDateTimeConverter()));

        builder.Services.AddEndpointsApiExplorer();

        builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
        builder.Services.AddProblemDetails();

        var corsAllowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
            ?? [];

        builder.Services.AddCors(options =>
        {
            options.AddDefaultPolicy(policy =>
            {
                policy
                    .WithOrigins(corsAllowedOrigins)
                    .WithMethods("GET", "POST", "PUT", "PATCH", "DELETE", "OPTIONS")
                    .WithHeaders("Authorization", "Content-Type");
            });
        });

        builder.Services.AddOpenTelemetry()
            .ConfigureResource(r => r.AddService("ClyvoCare.API"))
            .WithTracing(tracing =>
            {
                tracing
                    .AddSource(ApplicationDiagnostics.SourceName, PersistenceDiagnostics.SourceName)
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation();

                if (builder.Configuration.GetValue<bool>("Telemetry:OtlpEnabled"))
                    tracing.AddOtlpExporter();
            })
            .WithMetrics(metrics =>
            {
                metrics
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddRuntimeInstrumentation()
                    .AddMeter(
                        "Microsoft.AspNetCore.Hosting",
                        "Microsoft.AspNetCore.Routing",
                        "Microsoft.AspNetCore.Server.Kestrel",
                        "System.Net.Http",
                        "System.Runtime")
                    .AddPrometheusExporter();
            });

        var oracleConnectionString = builder.Configuration.GetConnectionString("ClyvoCareOracle")
            ?? throw new InvalidOperationException("Connection string 'ClyvoCareOracle' não encontrada.");

        builder.Services.AddHealthChecks()
            .AddCheck<MongoHealthCheck>("MongoDB Audit")
            .AddOracle(oracleConnectionString, name: "Oracle FIAP")
            .AddUrlGroup(new Uri("https://fiap.com.br"), "FIAP")
            .AddUrlGroup(new Uri("https://google.com.br"), "Google");

        builder.Services.AddSwaggerGen(options =>
        {
            options.OperationFilter<ClinicListResponseOperationFilter>();
            options.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = "ClyvoCare API",
                Version = "v1",
                Description =
                    "API REST do contexto de operação clínica do projeto ClyvoCare (Challenge FIAP 2026). " +
                    "Gerencia clínicas veterinárias e agendamentos vinculados aos pets cadastrados pela API Java.",
                Contact = new OpenApiContact
                {
                    Name = "Equipe ClyvoCare",
                    Email = "contato@example.com"
                }
            });

            options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
            {
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT",
                Description = "Token RS256 obtido em /auth/login da API Java."
            });
            options.AddSecurityRequirement(new OpenApiSecurityRequirement
            {
                [new OpenApiSecurityScheme
                {
                    Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
                }] = Array.Empty<string>()
            });

            var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
            var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
            options.IncludeXmlComments(xmlPath, includeControllerXmlComments: true);
        });

        var app = builder.Build();

        app.UseSerilogRequestLogging();

        app.UseExceptionHandler();

        app.UseOpenTelemetryPrometheusScrapingEndpoint();

        if (app.Environment.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI(options =>
            {
                options.SwaggerEndpoint("/swagger/v1/swagger.json", "ClyvoCare API v1");
                options.RoutePrefix = "";
            });
        }

        app.UseHttpsRedirection();
        app.UseCors();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapControllers();

        app.MapHealthChecks("/health", new HealthCheckOptions
        {
            ResponseWriter = HealthCheckResponseWriter.WriteJsonResponse
        });

        app.Run();
    }
}
