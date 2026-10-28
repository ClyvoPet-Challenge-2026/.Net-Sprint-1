using ClyvoCare.Application.Diagnostics;
using ClyvoCare.Application.DTOs;
using ClyvoCare.Application.Repositories;
using ClyvoCare.Application.Services.Interfaces;
using ClyvoCare.Domain.Entities;
using ClyvoCare.Domain.Exceptions;

namespace ClyvoCare.Application.Services.Implementations;

public sealed class AppointmentService(IAppointmentRepository appointments,
    IRepository<Clinic> clinics, IClinicalIdentityRepository identities,
    ICurrentUser currentUser, TimeProvider timeProvider) : IAppointmentService
{
    public AppointmentResponse Create(AppointmentRequest request)
    {
        using var activity = ApplicationDiagnostics.Source.StartActivity("AppointmentService.Create");
        ArgumentNullException.ThrowIfNull(request);
        var ownerId = identities.GetPetOwnerId(request.PetId)
            ?? throw new KeyNotFoundException("Pet não encontrado.");
        if (!currentUser.IsAdmin && ownerId != currentUser.OwnerId)
            throw new ForbiddenException("O pet pertence a outro tutor.");
        if (!clinics.ExistsById(request.ClinicId))
            throw new KeyNotFoundException("Clínica não encontrada.");
        if (request.ScheduledAt is null)
            throw new DomainException("Data e horário são obrigatórios.");

        var appointment = Appointment.Create(request.PetId, request.ClinicId, request.EventType,
            request.ScheduledAt.Value, request.Reason, request.Notes, timeProvider.GetUtcNow());
        if (appointments.ExistsAt(appointment.PetId, appointment.EventDate))
            throw new ConflictException("O pet já possui um evento clínico neste horário.");
        return AppointmentResponse.FromDomain(appointments.Add(appointment));
    }

    public AppointmentResponse? GetById(long id)
    {
        using var activity = ApplicationDiagnostics.Source.StartActivity("AppointmentService.GetById");
        var appointment = appointments.GetById(id, currentUser.IsAdmin ? null : currentUser.OwnerId);
        return appointment is null ? null : AppointmentResponse.FromDomain(appointment);
    }

    public PagedResult<AppointmentResponse> GetAll(long? petId, int pageNumber, int pageSize)
    {
        using var activity = ApplicationDiagnostics.Source.StartActivity("AppointmentService.GetAll");
        if (pageNumber < 1 || pageSize < 1 || pageSize > 100 || petId is <= 0
            || (long)(pageNumber - 1) * pageSize > int.MaxValue)
            throw new ArgumentException("Paginação ou filtro de pet inválido (pageNumber >= 1; pageSize entre 1 e 100).");
        var (items, total) = appointments.Search(currentUser.IsAdmin ? null : currentUser.OwnerId,
            petId, pageNumber, pageSize);
        return new PagedResult<AppointmentResponse>(items.Select(AppointmentResponse.FromDomain).ToList(),
            pageNumber, pageSize, total);
    }
}
