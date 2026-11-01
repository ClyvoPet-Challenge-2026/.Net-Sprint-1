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
    private static readonly DateTimeOffset Now = new(2030, 1, 1, 12, 0, 0, TimeSpan.Zero);
    private readonly Mock<IAppointmentRepository> _appointments = new();
    private readonly Mock<IRepository<PetReference>> _pets = new();
    private readonly Mock<IRepository<Clinic>> _clinics = new();
    private readonly Mock<ICurrentUser> _user = new();
    private readonly AppointmentService _service;

    public AppointmentServiceTests()
    {
        _user.SetupGet(u => u.OwnerId).Returns(7);
        _pets.Setup(p => p.GetById(1)).Returns(TestEntityFactory.CreatePet(1, 7));
        _clinics.Setup(c => c.ExistsById(2)).Returns(true);
        _appointments.Setup(a => a.Add(It.IsAny<Appointment>())).Returns((Appointment appointment) =>
        {
            TestEntityFactory.SetId(appointment, 10);
            return appointment;
        });
        _service = new AppointmentService(_appointments.Object, _pets.Object, _clinics.Object, _user.Object, new FixedClock());
    }

    private static AppointmentRequest Request() => new(1, 2, "CONSULTATION", Now.AddDays(1).AddTicks(19), " Reason ", " Notes ");

    [Fact]
    public void Create_ValidRequest_PreservesDetailsAndUsesStoredPrecision()
    {
        var result = _service.Create(Request());
        Assert.Equal(10, result.Id);
        Assert.Equal("SCHEDULED", result.Status);
        Assert.Equal("Reason", result.Reason);
        Assert.Equal("Notes", result.Notes);
        Assert.Equal(TimeSpan.Zero, result.ScheduledAt.Offset);
        _appointments.Verify(a => a.ExistsAt(1, Now.AddDays(1).UtcDateTime.AddTicks(10)), Times.Once);
    }

    [Fact]
    public void Create_AnotherOwnersPet_IsForbiddenWithoutSaving()
    {
        _pets.Setup(p => p.GetById(1)).Returns(TestEntityFactory.CreatePet(1, 8));
        Assert.Throws<ForbiddenException>(() => _service.Create(Request()));
        _appointments.Verify(a => a.Add(It.IsAny<Appointment>()), Times.Never);
    }

    [Fact]
    public void Create_AdminCanBookForAnotherOwner()
    {
        _user.SetupGet(u => u.IsAdmin).Returns(true);
        _pets.Setup(p => p.GetById(1)).Returns(TestEntityFactory.CreatePet(1, 8));
        Assert.Equal(10, _service.Create(Request()).Id);
    }

    [Theory]
    [InlineData("pet")]
    [InlineData("clinic")]
    public void Create_MissingReference_DoesNotSave(string reference)
    {
        if (reference == "pet") _pets.Setup(p => p.GetById(1)).Returns((PetReference?)null);
        else _clinics.Setup(c => c.ExistsById(2)).Returns(false);
        Assert.Throws<KeyNotFoundException>(() => _service.Create(Request()));
        _appointments.Verify(a => a.Add(It.IsAny<Appointment>()), Times.Never);
    }

    [Fact]
    public void Create_DuplicateInstant_IsConflictWithoutSaving()
    {
        _appointments.Setup(a => a.ExistsAt(1, It.IsAny<DateTime>())).Returns(true);
        Assert.Throws<ConflictException>(() => _service.Create(Request()));
        _appointments.Verify(a => a.Add(It.IsAny<Appointment>()), Times.Never);
    }

    [Fact]
    public void Create_InvalidDomain_DoesNotQueryOrSave()
    {
        Assert.Throws<DomainException>(() => _service.Create(Request() with { ScheduledAt = Now }));
        _pets.Verify(p => p.GetById(It.IsAny<long>()), Times.Never);
        _appointments.Verify(a => a.Add(It.IsAny<Appointment>()), Times.Never);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void GetById_Visibility_IsAppliedByRepository(bool admin)
    {
        _user.SetupGet(u => u.IsAdmin).Returns(admin);
        Assert.Null(_service.GetById(10));
        _appointments.Verify(a => a.GetById(10, admin ? null : 7L), Times.Once);
    }

    [Theory]
    [InlineData(null, 0, 20)]
    [InlineData(null, 1, 0)]
    [InlineData(null, 1, 101)]
    [InlineData(null, int.MaxValue, 100)]
    [InlineData(0L, 1, 20)]
    [InlineData(-1L, 1, 20)]
    public void Search_InvalidParameters_RejectsBeforeRepository(long? petId, int pageNumber, int pageSize)
    {
        Assert.ThrowsAny<ArgumentException>(() => _service.Search(petId, pageNumber, pageSize));
        _appointments.Verify(a => a.Search(It.IsAny<long?>(), It.IsAny<long?>(), It.IsAny<int>(), It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public void Search_EmptyPage_KeepsCountAndOwnerScope()
    {
        _appointments.Setup(a => a.Search(7, 1, 2, 1)).Returns((Array.Empty<Appointment>(), 1));
        var page = _service.Search(1, 2, 1);
        Assert.Empty(page.Items);
        Assert.Equal(1, page.TotalCount);
        Assert.Equal(1, page.TotalPages);
        Assert.Equal(2, page.PageNumber);
    }

    private sealed class FixedClock : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => Now;
    }
}
