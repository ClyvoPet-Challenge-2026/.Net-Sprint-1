using System.Net;
using System.Reflection;
using ClyvoCare.API.Diagnostics;
using ClyvoCare.API.Exceptions;
using ClyvoCare.API.Extensions;
using ClyvoCare.API.Health;
using ClyvoCare.API.Swagger;
using ClyvoCare.API.Serialization;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.HttpOverrides;
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
            .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] [TraceId={TraceId}] {Message:lj}{NewLine}{Exception}")
            .WriteTo.File(context.Configuration["Logging:FilePath"] ?? "logs/clyvocare-.log", rollingInterval: RollingInterval.Day,
                outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} {Level:u3}] [TraceId={TraceId}] {Message:lj}{NewLine}{Exception}"));

        builder.Services.AddClyvoCareDbContext(builder.Configuration);
        builder.Services.AddClyvoCareMongo(builder.Configuration);

        builder.Services.AddClyvoCareRepositories();

        builder.Services.AddClyvoCareApplicationServices();
        builder.Services.AddClyvoCareAuthentication(builder.Configuration);

        builder.Services.Configure<ForwardedHeadersOptions>(options =>
        {
            if (!builder.Configuration.GetValue<bool>("AppService:Enabled")) return;
            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
            // App Service proxies use private networks; keep unknown public proxies untrusted.
            options.KnownNetworks.Add(new(IPAddress.Parse("10.0.0.0"), 8));
            options.KnownNetworks.Add(new(IPAddress.Parse("172.16.0.0"), 12));
            options.KnownNetworks.Add(new(IPAddress.Parse("192.168.0.0"), 16));
            options.KnownNetworks.Add(new(IPAddress.Parse("::ffff:10.0.0.0"), 104));
            options.KnownNetworks.Add(new(IPAddress.Parse("::ffff:172.16.0.0"), 108));
            options.KnownNetworks.Add(new(IPAddress.Parse("::ffff:192.168.0.0"), 112));
        });

        builder.Services.AddControllers().AddJsonOptions(options =>
            options.JsonSerializerOptions.Converters.Add(new AppointmentDateTimeConverter()));

        builder.Services.AddEndpointsApiExplorer();

        builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
        builder.Services.AddProblemDetails(options => options.CustomizeProblemDetails = context =>
            context.ProblemDetails.Extensions["traceId"] = context.HttpContext.TraceIdentifier);

        var corsAllowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
            ?? [];

        builder.Services.AddCors(options =>
        {
            options.AddDefaultPolicy(policy =>
            {
                policy
                    .WithOrigins(corsAllowedOrigins)
                    .WithMethods("GET", "POST", "PUT", "PATCH", "DELETE", "OPTIONS")
                    .WithHeaders("Authorization", "Content-Type", "traceparent", "tracestate")
                    .WithExposedHeaders("X-Trace-Id");
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

        builder.Services.AddClyvoCareTracing(builder.Configuration);

        var oracleConnectionString = builder.Configuration.GetConnectionString("ClyvoCareOracle")
            ?? throw new InvalidOperationException("Connection string 'ClyvoCareOracle' não encontrada.");

        builder.Services.AddHealthChecks()
            .AddOracle(oracleConnectionString, name: "Oracle FIAP")
            .AddUrlGroup(new Uri("https://fiap.com.br"), "FIAP")
            .AddUrlGroup(new Uri("https://google.com.br"), "Google");

        builder.Services.AddSwaggerGen(options =>
        {
            options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
            {
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT",
                Description = "JWT emitido pelo login Java. Informe somente o token."
            });
            options.OperationFilter<AuthorizationOperationFilter>();
            options.OperationFilter<ClinicListResponseOperationFilter>();
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

        app.UseForwardedHeaders();
        app.UseMiddleware<TraceContextMiddleware>();
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
