using System.Text.Json;
using ClyvoCare.Application.DTOs;
using ClyvoCare.Application.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;

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
    /// Exports relational clinics with embedded city/state documents for MongoDB import.
    /// </summary>
    [HttpGet("exportar")]
    [Authorize(Roles = "ADMIN")]
    [ProducesResponseType(typeof(IReadOnlyList<ClinicExportResponse>), StatusCodes.Status200OK)]
    public IActionResult Export()
    {
        var documents = clinicService.GetAll().Select(clinic => new ClinicExportResponse(
            clinic.Id, clinic.Name, clinic.Cnpj, clinic.Phone, clinic.City));
        return File(JsonSerializer.SerializeToUtf8Bytes(documents,
            new JsonSerializerOptions(JsonSerializerDefaults.Web)), "application/json", "clinics.json");
    }

    /// <summary>
    /// Lists the legacy array without parameters, or a filtered page with navigation links.
    /// </summary>
    /// <response code="200">Lista retornada.</response>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<ClinicResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public IActionResult GetAll([FromQuery] int? pageNumber, [FromQuery] int? pageSize,
        [FromQuery] string? sortBy, [FromQuery] string? sortDirection,
        [FromQuery] string? nome, [FromQuery] string? cnpj, [FromQuery] long? cityId)
    {
        if (pageNumber is null && pageSize is null && sortBy is null && sortDirection is null
            && nome is null && cnpj is null && cityId is null)
            return Ok(clinicService.GetAll().Select(WithLinks).ToList());

        var search = new ClinicSearchRequest(pageNumber ?? 1, pageSize ?? 20,
            sortBy ?? "id", sortDirection ?? "asc", nome, cnpj, cityId).Normalize();
        var page = clinicService.SearchPage(search);
        var links = new Dictionary<string, LinkResponse>
        {
            ["self"] = PageLink(search, page.PageNumber),
            ["first"] = PageLink(search, 1),
            ["last"] = PageLink(search, Math.Max(1, page.TotalPages))
        };
        if (page.PageNumber > 1 && page.TotalPages > 0)
            links["prev"] = PageLink(search, Math.Min(page.PageNumber - 1, page.TotalPages));
        if (page.PageNumber < page.TotalPages)
            links["next"] = PageLink(search, page.PageNumber + 1);
        return Ok(new ClinicPageResponse(page.Items.Select(WithLinks).ToList(),
            page.PageNumber, page.PageSize, page.TotalCount) { Links = links });
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

        return Ok(WithLinks(clinic));
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
        return Ok(clinicService.GetByCityId(cityId).Select(WithLinks).ToList());
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
        return Ok(clinicService.Search(nome, cnpj).Select(WithLinks).ToList());
    }

    /// <summary>
    /// Cria uma clínica.
    /// </summary>
    /// <param name="request">Dados da clínica.</param>
    /// <response code="201">Clínica criada.</response>
    /// <response code="400">CNPJ duplicado ou modelo inválido.</response>
    /// <response code="404">Cidade informada não encontrada.</response>
    [HttpPost]
    [Authorize(Roles = "ADMIN")]
    [ProducesResponseType(typeof(ClinicResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public IActionResult Create([FromBody] ClinicRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var created = clinicService.Create(request);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, WithLinks(created));
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
    [Authorize(Roles = "ADMIN")]
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
    [Authorize(Roles = "ADMIN")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public IActionResult Delete(long id)
    {
        return clinicService.Delete(id) ? NoContent() : NotFound();
    }

    private ClinicResponse WithLinks(ClinicResponse clinic)
    {
        var links = new Dictionary<string, LinkResponse>
        {
            ["self"] = ActionLink(nameof(GetById), new { id = clinic.Id }, "GET"),
            ["by-city"] = ActionLink(nameof(GetByCityId), new { cityId = clinic.City.Id }, "GET")
        };
        if (User.IsInRole("ADMIN"))
        {
            links["update"] = ActionLink(nameof(Update), new { id = clinic.Id }, "PUT");
            links["delete"] = ActionLink(nameof(Delete), new { id = clinic.Id }, "DELETE");
        }
        return clinic with { Links = links };
    }

    private LinkResponse PageLink(ClinicSearchRequest search, int pageNumber) =>
        ActionLink(nameof(GetAll), new { pageNumber, pageSize = search.PageSize,
            sortBy = search.SortBy, sortDirection = search.SortDirection,
            nome = search.Name, cnpj = search.Cnpj, cityId = search.CityId }, "GET");

    private LinkResponse ActionLink(string action, object values, string method) =>
        new(Url.Action(action, "Clinic", values)
            ?? throw new InvalidOperationException("Cannot generate clinic link."), method);
}
