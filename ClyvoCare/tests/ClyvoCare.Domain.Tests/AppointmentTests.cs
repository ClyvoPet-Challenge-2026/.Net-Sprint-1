using ClyvoCare.Domain.Entities;
using ClyvoCare.Domain.Exceptions;

namespace ClyvoCare.Domain.Tests;

public class AppointmentTests
{
    private static readonly DateTimeOffset Now = new(2030, 1, 1, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Create_ValidInput_PreservesDetailsAndNormalizesUtc()
    {
        var scheduledAt = new DateTimeOffset(2030, 1, 2, 9, 0, 0, TimeSpan.FromHours(-3)).AddTicks(19);

        var appointment = Appointment.Create(1, 2, "CONSULTATION", scheduledAt, " Check-up ", " Notes ", Now);

        Assert.Equal(1, appointment.PetId);
        Assert.Equal(2, appointment.ClinicId);
        Assert.Equal("CONSULTATION", appointment.EventType);
        Assert.Equal("SCHEDULED", appointment.Status);
        Assert.Equal("Check-up", appointment.Reason);
        Assert.Equal("Notes", appointment.Notes);
        Assert.Equal(new DateTime(2030, 1, 2, 12, 0, 0, DateTimeKind.Utc).AddTicks(10), appointment.EventDate);
        Assert.Equal(DateTimeKind.Utc, appointment.EventDate.Kind);
    }

    [Theory]
    [InlineData("CONSULTATION")]
    [InlineData("VACCINATION")]
    [InlineData("SURGERY")]
    [InlineData("FOLLOW_UP")]
    [InlineData("EXAM")]
    public void Create_SupportedEventType_IsAccepted(string eventType)
    {
        var appointment = Appointment.Create(1, 2, eventType, Now.AddDays(1), "Reason", null, Now);
        Assert.Equal(eventType, appointment.EventType);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(-1, 1)]
    [InlineData(1, 0)]
    [InlineData(1, -1)]
    public void Create_InvalidReference_ThrowsDomainException(long petId, long clinicId)
    {
        Assert.Throws<DomainException>(() => Appointment.Create(petId, clinicId, "EXAM", Now.AddDays(1), "Reason", null, Now));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("consultation")]
    [InlineData("UNKNOWN")]
    public void Create_InvalidEventType_ThrowsDomainException(string? eventType)
    {
        Assert.Throws<DomainException>(() => Appointment.Create(1, 2, eventType!, Now.AddDays(1), "Reason", null, Now));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(9)]
    public void Create_InstantNotFutureAtOraclePrecision_ThrowsDomainException(long ticks)
    {
        Assert.Throws<DomainException>(() => Appointment.Create(1, 2, "EXAM", Now.AddTicks(ticks), "Reason", null, Now));
    }

    [Fact]
    public void Create_EquivalentOffsets_ProduceTheSameStoredInstant()
    {
        var scheduledAt = Now.AddDays(1);
        var first = Appointment.Create(1, 2, "EXAM", scheduledAt, "Reason", null, Now);
        var second = Appointment.Create(1, 2, "EXAM", scheduledAt.ToOffset(TimeSpan.FromHours(5.5)), "Reason", null, Now);
        Assert.Equal(first.EventDate, second.EventDate);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void Create_MissingReason_ThrowsDomainException(string? reason)
    {
        Assert.Throws<DomainException>(() => Appointment.Create(1, 2, "EXAM", Now.AddDays(1), reason!, null, Now));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" \t ")]
    public void Create_EmptyNotes_StoresNull(string? notes)
    {
        var appointment = Appointment.Create(1, 2, "EXAM", Now.AddDays(1), "Reason", notes, Now);
        Assert.Null(appointment.Notes);
    }

    [Fact]
    public void Create_MaximumDetailLengths_AreAccepted()
    {
        var appointment = Appointment.Create(1, 2, "EXAM", Now.AddDays(1), new string('r', 150), new string('n', 1000), Now);
        Assert.Equal(150, appointment.Reason!.Length);
        Assert.Equal(1000, appointment.Notes!.Length);
    }

    [Fact]
    public void Create_OversizedDetails_ThrowsDomainException()
    {
        Assert.Throws<DomainException>(() => Appointment.Create(1, 2, "EXAM", Now.AddDays(1), new string('r', 151), null, Now));
        Assert.Throws<DomainException>(() => Appointment.Create(1, 2, "EXAM", Now.AddDays(1), "Reason", new string('n', 1001), Now));
    }
}
