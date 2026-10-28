using ClyvoCare.API.Controllers;
using ClyvoCare.Application.DTOs;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace ClyvoCare.API.Swagger;

/// <summary>Documenta os dois formatos da listagem sem alterar o contrato legado.</summary>
public sealed class ClinicListResponseOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        if (context.MethodInfo.DeclaringType != typeof(ClinicController)
            || context.MethodInfo.Name != nameof(ClinicController.GetAll))
            return;

        var response = operation.Responses["200"];
        response.Description = "Sem parâmetros: lista. Com filtros, ordenação ou paginação: página com _links.";
        foreach (var content in response.Content.Values)
            content.Schema = new OpenApiSchema
            {
                OneOf = new List<OpenApiSchema>
                {
                    context.SchemaGenerator.GenerateSchema(typeof(IReadOnlyList<ClinicResponse>), context.SchemaRepository),
                    context.SchemaGenerator.GenerateSchema(typeof(ClinicPageResponse), context.SchemaRepository)
                }
            };
    }
}
