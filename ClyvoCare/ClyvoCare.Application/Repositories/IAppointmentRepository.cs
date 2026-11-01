using ClyvoCare.Domain.Entities;

namespace ClyvoCare.Application.Repositories;

public interface IAppointmentRepository
{
    Appointment Add(Appointment appointment);
    Appointment? GetById(long id, long? ownerId);
    bool ExistsAt(long petId, DateTime eventDate);
    (IReadOnlyList<Appointment> Items, int TotalCount) Search(long? ownerId, long? petId, int pageNumber, int pageSize);
}
