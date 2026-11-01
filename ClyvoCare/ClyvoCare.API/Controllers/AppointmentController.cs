using ClyvoCare.Application.DTOs;
using ClyvoCare.Application.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ClyvoCare.API.Controllers;

[ApiController]
[Route("api/agendamentos")]
[Authorize(Roles = "OWNER,ADMIN")]
[Produces("application/json")]
public class AppointmentController(IAppointmentService appointments) : ControllerBase
{
    [HttpPost]
    [ProducesResponseType(typeof(AppointmentResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public IActionResult Create(AppointmentRequest request)
    {
        var result = appointments.Create(request);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    [HttpGet("{id:long}")]
    [ProducesResponseType(typeof(AppointmentResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public IActionResult GetById(long id)
    {
        var result = appointments.GetById(id);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<AppointmentResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public IActionResult Search([FromQuery] long? petId, [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 20) =>
        Ok(appointments.Search(petId, pageNumber, pageSize));
}
