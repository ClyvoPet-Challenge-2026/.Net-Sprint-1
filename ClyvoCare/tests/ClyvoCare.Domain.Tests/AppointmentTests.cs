using ClyvoCare.Domain.Entities;
using ClyvoCare.Domain.Exceptions;

namespace ClyvoCare.Domain.Tests;

public class AppointmentTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 26, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Create_ComDadosValidos_DeveNormalizarUtcEPreservarObservacoes()
    {
        //Arrange
        var scheduledAt = new DateTimeOffset(2026, 10, 27, 9, 0, 0, TimeSpan.FromHours(-3));
        //Act
        var appointment = Appointment.Create(1, 2, "CONSULTATION", scheduledAt, "Retorno", "  Retorno anual  ", Now);
        //Assert
        Assert.Equal(new DateTime(2026, 10, 27, 12, 0, 0, DateTimeKind.Utc), appointment.EventDate);
        Assert.Equal("SCHEDULED", appointment.Status);
        Assert.Equal("Retorno anual", appointment.Notes);
    }

    [Theory]
    [InlineData("CONSULTATION")]
    [InlineData("VACCINATION")]
    [InlineData("SURGERY")]
    [InlineData("FOLLOW_UP")]
    [InlineData("EXAM")]
    public void Create_ComTipoPrevistoNoBanco_DeveAceitar(string eventType)
    {
        //Act
        var appointment = Appointment.Create(1, 2, eventType, Now.AddDays(1), "Consulta", null, Now);
        //Assert
        Assert.Equal(eventType, appointment.EventType);
    }

    [Theory]
    [InlineData(0, 2)]
    [InlineData(1, 0)]
    [InlineData(-1, 2)]
    public void Create_ComReferenciaInvalida_DeveRejeitar(long petId, long clinicId)
    {
        //Act / Assert
        Assert.Throws<DomainException>(() => Appointment.Create(petId, clinicId, "EXAM", Now.AddDays(1), "Consulta", null, Now));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    public void Create_ComHorarioNaoFuturo_DeveRejeitar(int days)
    {
        //Act / Assert
        Assert.Throws<DomainException>(() => Appointment.Create(1, 2, "EXAM", Now.AddDays(days), "Consulta", null, Now));
    }

    [Fact]
    public void Create_ComTipoOuObservacoesInvalidos_DeveRejeitar()
    {
        //Act / Assert
        Assert.Throws<DomainException>(() => Appointment.Create(1, 2, "checkup", Now.AddDays(1), "Consulta", null, Now));
        Assert.Throws<DomainException>(() => Appointment.Create(1, 2, "EXAM", Now.AddDays(1), "Consulta", new string('a', 1001), Now));
    }
}
