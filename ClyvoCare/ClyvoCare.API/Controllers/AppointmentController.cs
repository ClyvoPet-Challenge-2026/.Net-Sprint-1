using ClyvoCare.Application.DTOs;
using ClyvoCare.Application.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ClyvoCare.API.Controllers;

/// <summary>Criação e consulta de agendamentos clínicos.</summary>
[Route("api/agendamentos")]
[ApiController]
[Authorize(Roles = "OWNER,ADMIN")]
[Produces("application/json")]
public class AppointmentController(IAppointmentService appointmentService) : ControllerBase
{
    /// <summary>Lista agendamentos do tutor; ADMIN consulta todos. Horários em UTC.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<AppointmentResponse>), StatusCodes.Status200OK)]
    public IActionResult GetAll([FromQuery] long? petId, [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 20)
        => Ok(appointmentService.GetAll(petId, pageNumber, pageSize));

    /// <summary>Consulta um agendamento visível para a conta autenticada.</summary>
    [HttpGet("{id:long}")]
    [ProducesResponseType(typeof(AppointmentResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public IActionResult GetById(long id)
    {
        var appointment = appointmentService.GetById(id);
        return appointment is null ? NotFound() : Ok(appointment);
    }

    /// <summary>Agenda atendimento para um pet próprio; ADMIN pode agendar para qualquer pet.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(AppointmentResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public IActionResult Create([FromBody] AppointmentRequest request)
    {
        var created = appointmentService.Create(request);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }
}
