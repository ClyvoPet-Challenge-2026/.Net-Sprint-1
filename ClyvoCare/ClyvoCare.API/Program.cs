using System.Reflection;
using ClyvoCare.API.Exceptions;
using ClyvoCare.API.Extensions;
using ClyvoCare.API.Health;
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

        builder.Services.AddClyvoCareApplicationServices();

        builder.Services.AddControllers();

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
            .AddOracle(oracleConnectionString, name: "Oracle FIAP")
            .AddUrlGroup(new Uri("https://fiap.com.br"), "FIAP")
            .AddUrlGroup(new Uri("https://google.com.br"), "Google");

        builder.Services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = "ClyvoCare API",
                Version = "v1",
                Description =
                    "API REST do contexto de operação clínica do projeto ClyvoCare (Challenge FIAP 2026). " +
                    "Gerencia clínicas veterinárias, eventos clínicos e lembretes vinculados aos pets cadastrados pela API Java.",
                Contact = new OpenApiContact
                {
                    Name = "Equipe ClyvoCare",
                    Email = "contato@example.com"
                }
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
        app.UseAuthorization();
        app.MapControllers();

        app.MapHealthChecks("/health", new HealthCheckOptions
        {
            ResponseWriter = HealthCheckResponseWriter.WriteJsonResponse
        });

        app.Run();
    }
}
