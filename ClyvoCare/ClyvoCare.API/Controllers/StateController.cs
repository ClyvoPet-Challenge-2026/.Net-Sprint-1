using ClyvoCare.Application.DTOs;
using ClyvoCare.Application.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace ClyvoCare.API.Controllers;

/// <summary>
/// Endpoints de leitura para estados brasileiros.
/// Esta API apenas consulta a tabela TB_CAD_STATE; a escrita é feita pela API Java.
/// </summary>
[Route("api/estados")]
[ApiController]
[Produces("application/json")]
public class StateController(IStateService stateService) : ControllerBase
{
    /// <summary>
    /// Lista todos os estados cadastrados.
    /// </summary>
    /// <response code="200">Lista retornada.</response>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<StateResponse>), StatusCodes.Status200OK)]
    public IActionResult GetAll()
    {
        return Ok(stateService.GetAll());
    }

    /// <summary>
    /// Busca um estado pelo Id.
    /// </summary>
    /// <param name="id">Identificador único do estado.</param>
    /// <response code="200">Estado encontrado.</response>
    /// <response code="404">Estado não encontrado.</response>
    [HttpGet("{id:long}")]
    [ProducesResponseType(typeof(StateResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public IActionResult GetById(long id)
    {
        var state = stateService.GetById(id);
        if (state is null)
            return NotFound();

        return Ok(state);
    }
}
