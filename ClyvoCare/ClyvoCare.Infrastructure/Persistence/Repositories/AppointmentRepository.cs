using ClyvoCare.Infrastructure.Diagnostics;
using ClyvoCare.Application.Repositories;
using ClyvoCare.Domain.Entities;
using ClyvoCare.Domain.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace ClyvoCare.Infrastructure.Persistence.Repositories;

public sealed class AppointmentRepository(ClyvoCareContext context) : IAppointmentRepository
{
    public Appointment Add(Appointment appointment)
    {
        using var activity = PersistenceDiagnostics.Source.StartActivity("AppointmentRepository.Add");
        context.Appointments.Add(appointment);
        try
        {
            context.SaveChanges();
        }
        catch (DbUpdateException)
        {
            context.Entry(appointment).State = EntityState.Detached;
            // Também cobre requisições concorrentes, protegidas pelo índice único.
            if (ExistsAt(appointment.PetId, appointment.EventDate))
                throw new ConflictException("O pet já possui um evento clínico neste horário.");
            throw;
        }
        return appointment;
    }

    public Appointment? GetById(long id, long? ownerId)
    {
        using var activity = PersistenceDiagnostics.Source.StartActivity("AppointmentRepository.GetById");
        return VisibleTo(ownerId).FirstOrDefault(a => a.Id == id);
    }

    public (IReadOnlyList<Appointment> Items, int TotalCount) Search(long? ownerId,
        long? petId, int pageNumber, int pageSize)
    {
        using var activity = PersistenceDiagnostics.Source.StartActivity("AppointmentRepository.Search");
        var query = VisibleTo(ownerId);
        if (petId.HasValue)
            query = query.Where(a => a.PetId == petId.Value);
        var total = query.Count();
        var items = query.OrderBy(a => a.EventDate).ThenBy(a => a.Id)
            .Skip((pageNumber - 1) * pageSize).Take(pageSize).ToList();
        return (items, total);
    }

    public bool ExistsAt(long petId, DateTime eventDate)
    {
        using var activity = PersistenceDiagnostics.Source.StartActivity("AppointmentRepository.ExistsAt");
        return context.Appointments.Count(a => a.PetId == petId && a.EventDate == eventDate) > 0;
    }

    private IQueryable<Appointment> VisibleTo(long? ownerId)
    {
        var query = context.Appointments.AsNoTracking();
        return ownerId.HasValue ? query.Where(a => a.Pet.OwnerId == ownerId.Value) : query;
    }
}
