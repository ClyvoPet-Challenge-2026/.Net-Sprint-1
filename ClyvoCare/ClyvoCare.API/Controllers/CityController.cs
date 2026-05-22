using ClyvoCare.Application.DTOs;
using ClyvoCare.Application.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace ClyvoCare.API.Controllers;

/// <summary>
/// Endpoints de leitura para cidades.
/// Esta API apenas consulta a tabela TB_CAD_CITY; a escrita é feita pela API Java.
/// </summary>
[Route("api/cidades")]
[ApiController]
[Produces("application/json")]
public class CityController(ICityService cityService) : ControllerBase
{
    /// <summary>
    /// Lista todas as cidades cadastradas, com o estado aninhado.
    /// </summary>
    /// <response code="200">Lista retornada.</response>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<CityResponse>), StatusCodes.Status200OK)]
    public IActionResult GetAll()
    {
        return Ok(cityService.GetAll());
    }

    /// <summary>
    /// Busca uma cidade pelo Id.
    /// </summary>
    /// <param name="id">Identificador único da cidade.</param>
    /// <response code="200">Cidade encontrada.</response>
    /// <response code="404">Cidade não encontrada.</response>
    [HttpGet("{id:long}")]
    [ProducesResponseType(typeof(CityResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public IActionResult GetById(long id)
    {
        var city = cityService.GetById(id);
        if (city is null)
            return NotFound();

        return Ok(city);
    }
}
