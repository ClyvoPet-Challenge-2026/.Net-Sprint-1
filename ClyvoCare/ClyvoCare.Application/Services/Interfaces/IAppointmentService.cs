using ClyvoCare.Application.DTOs;

namespace ClyvoCare.Application.Services.Interfaces;

public interface IAppointmentService
{
    AppointmentResponse Create(AppointmentRequest request);
    AppointmentResponse? GetById(long id);
    PagedResult<AppointmentResponse> GetAll(long? petId, int pageNumber, int pageSize);
}
