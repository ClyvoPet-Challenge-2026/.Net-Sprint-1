using System.Reflection;
using ClyvoCare.API.Exceptions;
using ClyvoCare.API.Extensions;
using Microsoft.OpenApi.Models;

namespace ClyvoCare.API;

public class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        builder.Services.AddClyvoCareDbContext(builder.Configuration);

        builder.Services.AddClyvoCareRepositories();

        builder.Services.AddClyvoCareApplicationServices();

        builder.Services.AddControllers();

        builder.Services.AddEndpointsApiExplorer();

        builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
        builder.Services.AddProblemDetails();

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

        app.UseExceptionHandler();

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
        app.UseAuthorization();
        app.MapControllers();

        app.Run();
    }
}
