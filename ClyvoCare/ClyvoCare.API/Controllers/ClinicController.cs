using ClyvoCare.Application.DTOs;
using ClyvoCare.Application.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace ClyvoCare.API.Controllers;

/// <summary>
/// Endpoints de clínicas veterinárias.
/// </summary>
[Route("api/clinicas")]
[ApiController]
[Produces("application/json")]
public class ClinicController(IClinicService clinicService) : ControllerBase
{
    /// <summary>
    /// Lista todas as clínicas.
    /// </summary>
    /// <response code="200">Lista retornada.</response>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<ClinicResponse>), StatusCodes.Status200OK)]
    public IActionResult GetAll()
    {
        return Ok(clinicService.GetAll());
    }

    /// <summary>
    /// Obtém uma clínica pelo Id.
    /// </summary>
    /// <param name="id">Identificador único.</param>
    /// <response code="200">Clínica encontrada.</response>
    /// <response code="404">Não encontrada.</response>
    [HttpGet("{id:long}")]
    [ProducesResponseType(typeof(ClinicResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public IActionResult GetById(long id)
    {
        var clinic = clinicService.GetById(id);
        if (clinic is null)
            return NotFound();

        return Ok(clinic);
    }

    /// <summary>
    /// Lista as clínicas de uma cidade.
    /// </summary>
    /// <param name="cityId">Identificador da cidade.</param>
    /// <response code="200">Lista retornada.</response>
    /// <response code="404">Cidade não encontrada.</response>
    [HttpGet("por-cidade/{cityId:long}")]
    [ProducesResponseType(typeof(IReadOnlyList<ClinicResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public IActionResult GetByCityId(long cityId)
    {
        return Ok(clinicService.GetByCityId(cityId));
    }

    /// <summary>
    /// Busca clínicas por nome (parcial) e/ou CNPJ (exato).
    /// </summary>
    /// <param name="nome">Trecho do nome. Opcional.</param>
    /// <param name="cnpj">CNPJ exato. Opcional.</param>
    /// <response code="200">Resultados da busca.</response>
    [HttpGet("buscar")]
    [ProducesResponseType(typeof(IReadOnlyList<ClinicResponse>), StatusCodes.Status200OK)]
    public IActionResult Search([FromQuery] string? nome, [FromQuery] string? cnpj)
    {
        return Ok(clinicService.Search(nome, cnpj));
    }

    /// <summary>
    /// Cria uma clínica.
    /// </summary>
    /// <param name="request">Dados da clínica.</param>
    /// <response code="201">Clínica criada.</response>
    /// <response code="400">CNPJ duplicado ou modelo inválido.</response>
    /// <response code="404">Cidade informada não encontrada.</response>
    [HttpPost]
    [ProducesResponseType(typeof(ClinicResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public IActionResult Create([FromBody] ClinicRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var created = clinicService.Create(request);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    /// <summary>
    /// Atualiza os dados de uma clínica.
    /// </summary>
    /// <param name="id">Identificador da clínica.</param>
    /// <param name="request">Novos dados.</param>
    /// <response code="204">Atualizada.</response>
    /// <response code="400">CNPJ pertence a outra clínica ou modelo inválido.</response>
    /// <response code="404">Clínica ou cidade não encontrada.</response>
    [HttpPut("{id:long}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public IActionResult Update(long id, [FromBody] ClinicRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        clinicService.Update(id, request);
        return NoContent();
    }

    /// <summary>
    /// Remove uma clínica pelo Id.
    /// </summary>
    /// <param name="id">Identificador único.</param>
    /// <response code="204">Removida.</response>
    /// <response code="404">Não encontrada.</response>
    [HttpDelete("{id:long}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public IActionResult Delete(long id)
    {
        return clinicService.Delete(id) ? NoContent() : NotFound();
    }
}
