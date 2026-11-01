using ClyvoCare.Domain.Entities;
using ClyvoCare.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace ClyvoCare.IntegrationTests;

public class AppointmentPersistenceTests : IDisposable
{
    private readonly SqliteConnection _connection = new("Data Source=:memory:");
    private readonly ClyvoCareContext _db;
    private readonly long _clinicId;
    private static readonly DateTimeOffset Now = new(2030, 1, 1, 12, 0, 0, TimeSpan.Zero);

    public AppointmentPersistenceTests()
    {
        _connection.Open();
        _db = new ClyvoCareContext(new DbContextOptionsBuilder<ClyvoCareContext>().UseSqlite(_connection).Options);
        _db.Database.EnsureCreated();
        // These tables are supplied by Java/Flyway in Oracle and excluded from EF migrations.
        _db.Database.ExecuteSqlRaw("""
            CREATE TABLE TB_CAD_OWNER (ID INTEGER PRIMARY KEY, EMAIL TEXT NOT NULL, ROLE_NAME TEXT NOT NULL, ENABLED TEXT NOT NULL);
            CREATE TABLE TB_CAD_PET (ID INTEGER PRIMARY KEY, OWNER_ID INTEGER NOT NULL REFERENCES TB_CAD_OWNER(ID));
            INSERT INTO TB_CAD_OWNER VALUES (1, 'owner@example.com', 'OWNER', 'Y');
            INSERT INTO TB_CAD_PET VALUES (1, 1);
            """);
        var state = TestEntityFactory.CreateState(1, "Sao Paulo", "SP");
        var city = TestEntityFactory.CreateCity(1, "Sao Paulo", state);
        _db.Cities.Add(city);
        var clinic = Clinic.Create("Test clinic", "11.111.111/0001-11", 1, null);
        _db.Clinics.Add(clinic);
        _db.SaveChanges();
        _clinicId = clinic.Id;
    }

    [Fact]
    public void Save_ValidAppointment_RoundTripsDetailsAndUtc()
    {
        var appointment = Appointment.Create(1, _clinicId, "CONSULTATION", Now.AddDays(1), "Check-up", "Notes", Now);
        _db.Appointments.Add(appointment);
        _db.SaveChanges();
        _db.ChangeTracker.Clear();

        var saved = _db.Appointments.Include(a => a.Pet).ThenInclude(p => p.Owner).Single();

        Assert.Equal("Check-up", saved.Reason);
        Assert.Equal("Notes", saved.Notes);
        Assert.Equal("SCHEDULED", saved.Status);
        Assert.Equal(appointment.EventDate, saved.EventDate);
        Assert.Equal(DateTimeKind.Utc, saved.EventDate.Kind);
        Assert.Equal(1, saved.Pet.OwnerId);
        Assert.Equal("owner@example.com", saved.Pet.Owner.Email);
    }

    [Fact]
    public void Read_LegacyEventWithoutDetails_PreservesNullsAndTimestamp()
    {
        var date = new DateTime(2024, 1, 10, 9, 0, 0);
        _db.Database.ExecuteSqlInterpolated($"""
            INSERT INTO TB_HEA_CLINICAL_EVENT (PET_ID, CLINIC_ID, EVENT_TYPE, EVENT_DATE, STATUS)
            VALUES (1, {_clinicId}, 'CONSULTATION', {date}, 'COMPLETED')
            """);

        var saved = _db.Appointments.Single();

        Assert.Null(saved.Reason);
        Assert.Null(saved.Notes);
        Assert.Equal("COMPLETED", saved.Status);
        Assert.Equal(date.Ticks, saved.EventDate.Ticks);
        Assert.Equal(DateTimeKind.Utc, saved.EventDate.Kind);
    }

    [Fact]
    public void Save_EquivalentPetAndInstant_UniqueIndexRejectsDuplicate()
    {
        _db.Appointments.Add(Appointment.Create(1, _clinicId, "EXAM", Now.AddDays(1), "First", null, Now));
        _db.SaveChanges();
        _db.Appointments.Add(Appointment.Create(1, _clinicId, "EXAM",
            Now.AddDays(1).ToOffset(TimeSpan.FromHours(-3)), "Duplicate", null, Now));

        Assert.Throws<DbUpdateException>(() => _db.SaveChanges());
    }

    [Fact]
    public void Delete_ClinicWithAppointment_IsRejectedWithoutDeletingEvent()
    {
        _db.Appointments.Add(Appointment.Create(1, _clinicId, "EXAM", Now.AddDays(1), "Reason", null, Now));
        _db.SaveChanges();
        _db.ChangeTracker.Clear();

        Assert.Throws<SqliteException>(() => _db.Database.ExecuteSqlInterpolated(
            $"DELETE FROM TB_CAD_CLINIC WHERE ID = {_clinicId}"));
        Assert.Equal(1, _db.Appointments.Count());
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }
}
