using ClyvoCare.API.Controllers;
using ClyvoCare.Application.DTOs;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace ClyvoCare.API.Swagger;

public sealed class ClinicListResponseOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        if (context.MethodInfo.DeclaringType != typeof(ClinicController)
            || context.MethodInfo.Name != nameof(ClinicController.GetAll)) return;
        foreach (var content in operation.Responses["200"].Content.Values)
            content.Schema = new OpenApiSchema
            {
                OneOf =
                [
                    context.SchemaGenerator.GenerateSchema(typeof(IReadOnlyList<ClinicResponse>), context.SchemaRepository),
                    context.SchemaGenerator.GenerateSchema(typeof(ClinicPageResponse), context.SchemaRepository)
                ]
            };
        operation.Description = "Sem parâmetros preenchidos, retorna array. Com filtros ou paginação, retorna página com links.";
    }
}
