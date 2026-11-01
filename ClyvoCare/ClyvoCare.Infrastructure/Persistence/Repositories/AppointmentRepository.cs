using ClyvoCare.Application.Repositories;
using ClyvoCare.Domain.Entities;
using ClyvoCare.Domain.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace ClyvoCare.Infrastructure.Persistence.Repositories;

public sealed class AppointmentRepository(ClyvoCareContext context) : IAppointmentRepository
{
    public Appointment Add(Appointment appointment)
    {
        context.Appointments.Add(appointment);
        try
        {
            context.SaveChanges();
        }
        catch (DbUpdateException)
        {
            context.Entry(appointment).State = EntityState.Detached;
            // The unique index also rejects a competing insert after the service's initial check.
            if (ExistsAt(appointment.PetId, appointment.EventDate))
                throw new ConflictException("The pet already has an event at this instant.");
            throw;
        }
        return appointment;
    }

    public Appointment? GetById(long id, long? ownerId) => VisibleTo(ownerId).FirstOrDefault(a => a.Id == id);

    public bool ExistsAt(long petId, DateTime eventDate) =>
        context.Appointments.Count(a => a.PetId == petId && a.EventDate == eventDate) > 0;

    public (IReadOnlyList<Appointment> Items, int TotalCount) Search(long? ownerId, long? petId, int pageNumber, int pageSize)
    {
        var query = VisibleTo(ownerId);
        if (petId.HasValue) query = query.Where(a => a.PetId == petId.Value);
        var total = query.Count();
        var items = query.OrderBy(a => a.EventDate).ThenBy(a => a.Id)
            .Skip((pageNumber - 1) * pageSize).Take(pageSize).ToList();
        return (items, total);
    }

    private IQueryable<Appointment> VisibleTo(long? ownerId)
    {
        var query = context.Appointments.AsNoTracking();
        return ownerId.HasValue ? query.Where(a => a.Pet.OwnerId == ownerId.Value) : query;
    }
}
