using ClyvoCare.Application.Diagnostics;
using ClyvoCare.Application.DTOs;
using ClyvoCare.Application.Repositories;
using ClyvoCare.Application.Services.Interfaces;
using ClyvoCare.Domain.Entities;
using ClyvoCare.Domain.Exceptions;

namespace ClyvoCare.Application.Services.Implementations;

public sealed class AppointmentService(IAppointmentRepository appointments, IRepository<PetReference> pets,
    IRepository<Clinic> clinics, ICurrentUser currentUser, TimeProvider clock) : IAppointmentService
{
    public AppointmentResponse Create(AppointmentRequest request)
    {
        using var activity = ApplicationTelemetry.Source.StartActivity("AppointmentService.Create");
        ArgumentNullException.ThrowIfNull(request);
        var appointment = Appointment.Create(request.PetId, request.ClinicId, request.EventType,
            request.ScheduledAt, request.Reason, request.Notes, clock.GetUtcNow());
        var pet = pets.GetById(request.PetId) ?? throw new KeyNotFoundException("Pet not found.");
        if (!currentUser.IsAdmin && pet.OwnerId != currentUser.OwnerId)
            throw new ForbiddenException("The pet belongs to another owner.");
        if (!clinics.ExistsById(request.ClinicId))
            throw new KeyNotFoundException("Clinic not found.");
        if (appointments.ExistsAt(appointment.PetId, appointment.EventDate))
            throw new ConflictException("The pet already has an event at this instant.");
        return AppointmentResponse.FromDomain(appointments.Add(appointment));
    }

    public AppointmentResponse? GetById(long id)
    {
        using var activity = ApplicationTelemetry.Source.StartActivity("AppointmentService.GetById");
        if (id <= 0) throw new ArgumentOutOfRangeException(nameof(id), "Id must be positive.");
        var appointment = appointments.GetById(id, currentUser.IsAdmin ? null : currentUser.OwnerId);
        return appointment is null ? null : AppointmentResponse.FromDomain(appointment);
    }

    public PagedResult<AppointmentResponse> Search(long? petId, int pageNumber, int pageSize)
    {
        using var activity = ApplicationTelemetry.Source.StartActivity("AppointmentService.Search");
        if (petId <= 0) throw new ArgumentOutOfRangeException(nameof(petId), "PetId must be positive.");
        if (pageNumber < 1 || pageSize is < 1 or > 100 || (long)(pageNumber - 1) * pageSize > int.MaxValue)
            throw new ArgumentException("PageNumber must be positive, PageSize must be 1–100 and the offset must fit an integer.");
        var page = appointments.Search(currentUser.IsAdmin ? null : currentUser.OwnerId, petId, pageNumber, pageSize);
        return new PagedResult<AppointmentResponse>(page.Items.Select(AppointmentResponse.FromDomain).ToList(),
            pageNumber, pageSize, page.TotalCount);
    }
}
