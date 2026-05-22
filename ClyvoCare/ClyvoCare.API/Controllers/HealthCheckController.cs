using Microsoft.AspNetCore.Mvc;

namespace ClyvoCare.API.Controllers;

/// <summary>
/// Endpoint mínimo para verificar se a API está no ar.
/// Útil para health checks de load balancer e para validar que o projeto sobe corretamente.
/// </summary>
[Route("api/[controller]")]
[ApiController]
[Produces("application/json")]
public class HealthCheckController : ControllerBase
{
    /// <summary>
    /// Retorna o status atual da API.
    /// </summary>
    /// <response code="200">API está respondendo normalmente.</response>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult Get()
    {
        return Ok(new
        {
            status = "ok",
            service = "ClyvoCare.API",
            timestamp = DateTime.UtcNow
        });
    }
}
