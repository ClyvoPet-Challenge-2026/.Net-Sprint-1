using ClyvoCare.Application.DTOs;
using ClyvoCare.Application.Repositories;
using ClyvoCare.Application.Services.Implementations;
using ClyvoCare.Application.Services.Interfaces;
using ClyvoCare.Domain.Entities;
using ClyvoCare.Domain.Exceptions;
using Moq;

namespace ClyvoCare.Application.Tests;

public class AppointmentServiceTests
{
    private readonly Mock<IAppointmentRepository> _appointments = new();
    private readonly Mock<IRepository<Clinic>> _clinics = new();
    private readonly Mock<IClinicalIdentityRepository> _identities = new();
    private readonly Mock<ICurrentUser> _currentUser = new();
    private readonly AppointmentService _service;
    private static readonly DateTimeOffset Now = new(2026, 10, 26, 12, 0, 0, TimeSpan.Zero);

    private sealed class FixedTime : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => Now;
    }

    public AppointmentServiceTests()
    {
        _currentUser.SetupGet(u => u.OwnerId).Returns(200);
        _identities.Setup(i => i.GetPetOwnerId(1000)).Returns(200L);
        _clinics.Setup(c => c.ExistsById(100)).Returns(true);
        _service = new AppointmentService(_appointments.Object, _clinics.Object,
            _identities.Object, _currentUser.Object, new FixedTime());
    }

    private static AppointmentRequest ValidRequest() => new(1000, 100, "CONSULTATION", Now.AddDays(1), "Check-up Geral", null);

    [Fact]
    public void Create_ComPetProprio_DevePersistir()
    {
        //Arrange
        _appointments.Setup(a => a.Add(It.IsAny<Appointment>())).Returns((Appointment a) => a);
        //Act
        var result = _service.Create(ValidRequest());
        //Assert
        Assert.Equal(1000, result.PetId);
        Assert.Equal("SCHEDULED", result.Status);
        _appointments.Verify(a => a.Add(It.IsAny<Appointment>()), Times.Once);
    }

    [Fact]
    public void Create_ComPetDeOutroTutor_DeveNegarSemPersistir()
    {
        //Arrange
        _identities.Setup(i => i.GetPetOwnerId(1000)).Returns(300L);
        //Act / Assert
        Assert.Throws<ForbiddenException>(() => _service.Create(ValidRequest()));
        _appointments.Verify(a => a.Add(It.IsAny<Appointment>()), Times.Never);
    }

    [Fact]
    public void Create_ComAdministrador_DevePermitirOutroTutor()
    {
        //Arrange
        _currentUser.SetupGet(u => u.IsAdmin).Returns(true);
        _identities.Setup(i => i.GetPetOwnerId(1000)).Returns(300L);
        _appointments.Setup(a => a.Add(It.IsAny<Appointment>())).Returns((Appointment a) => a);
        //Act / Assert
        Assert.Equal(1000, _service.Create(ValidRequest()).PetId);
    }

    [Fact]
    public void Create_ComPetOuClinicaInexistente_DeveRejeitar()
    {
        //Arrange
        _identities.Setup(i => i.GetPetOwnerId(1000)).Returns((long?)null);
        //Act / Assert
        Assert.Throws<KeyNotFoundException>(() => _service.Create(ValidRequest()));
        _identities.Setup(i => i.GetPetOwnerId(1000)).Returns(200L);
        _clinics.Setup(c => c.ExistsById(100)).Returns(false);
        Assert.Throws<KeyNotFoundException>(() => _service.Create(ValidRequest()));
        _appointments.Verify(a => a.Add(It.IsAny<Appointment>()), Times.Never);
    }

    [Fact]
    public void Create_ComMesmoPetEHorario_DeveRetornarConflito()
    {
        //Arrange
        _appointments.Setup(a => a.ExistsAt(1000, Now.AddDays(1).UtcDateTime)).Returns(true);
        //Act / Assert
        Assert.Throws<ConflictException>(() => _service.Create(ValidRequest()));
        _appointments.Verify(a => a.Add(It.IsAny<Appointment>()), Times.Never);
    }

    [Fact]
    public void GetAll_ComTutor_DeveAplicarFiltroAntesDaPaginacao()
    {
        //Arrange
        _appointments.Setup(a => a.Search(200L, null, 1, 20)).Returns((Array.Empty<Appointment>(), 0));
        //Act
        var result = _service.GetAll(null, 1, 20);
        //Assert
        Assert.Empty(result.Items);
        _appointments.Verify(a => a.Search(200L, null, 1, 20), Times.Once);
    }

    [Theory]
    [InlineData(0, 20)]
    [InlineData(1, 0)]
    [InlineData(1, 101)]
    [InlineData(int.MaxValue, 100)]
    public void GetAll_ComPaginacaoInvalida_DeveRejeitar(int page, int size)
    {
        //Act / Assert
        Assert.Throws<ArgumentException>(() => _service.GetAll(null, page, size));
    }

    [Fact]
    public void GetById_ComTutor_DeveConsultarSomenteRecursosProprios()
    {
        //Act
        Assert.Null(_service.GetById(99));
        //Assert
        _appointments.Verify(a => a.GetById(99, 200L), Times.Once);
    }
}
