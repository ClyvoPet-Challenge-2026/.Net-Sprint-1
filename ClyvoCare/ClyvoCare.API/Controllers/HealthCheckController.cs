using Microsoft.AspNetCore.Mvc;

namespace ClyvoCare.API.Controllers;

/// <summary>
/// Endpoint para verificar se a API está no ar.
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
