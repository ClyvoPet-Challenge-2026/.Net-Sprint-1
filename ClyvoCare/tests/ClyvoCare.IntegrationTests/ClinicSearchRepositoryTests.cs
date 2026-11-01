using ClyvoCare.Application.DTOs;
using ClyvoCare.Domain.Entities;
using ClyvoCare.Infrastructure.Persistence;
using ClyvoCare.Infrastructure.Persistence.Repositories;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace ClyvoCare.IntegrationTests;

public class ClinicSearchRepositoryTests : IDisposable
{
    private readonly SqliteConnection _connection = new("Data Source=:memory:");
    private readonly ClyvoCareContext _db;
    private readonly ClinicRepository _repository;

    public ClinicSearchRepositoryTests()
    {
        _connection.Open();
        _db = new ClyvoCareContext(new DbContextOptionsBuilder<ClyvoCareContext>().UseSqlite(_connection).Options);
        _db.Database.EnsureCreated();
        var state = TestEntityFactory.CreateState(1, "Sao Paulo", "SP");
        _db.Cities.AddRange(TestEntityFactory.CreateCity(1, "Sao Paulo", state),
            TestEntityFactory.CreateCity(2, "Campinas", state));
        _db.Clinics.AddRange(
            Clinic.Create("Alpha", "11.111.111/0001-11", 1, null),
            Clinic.Create("Alpha", "22.222.222/0001-22", 2, null),
            Clinic.Create("Beta", "33.333.333/0001-33", 1, null),
            Clinic.Create("Vet 100%_Care", "44.444.444/0001-44", 1, null));
        _db.SaveChanges();
        _repository = new ClinicRepository(_db);
    }

    [Theory]
    [InlineData("id", "asc", "1,2,3,4")]
    [InlineData("id", "desc", "4,3,2,1")]
    [InlineData("name", "asc", "1,2,3,4")]
    [InlineData("name", "desc", "4,3,1,2")]
    [InlineData("cnpj", "asc", "1,2,3,4")]
    [InlineData("cnpj", "desc", "4,3,2,1")]
    [InlineData("cityId", "asc", "1,3,4,2")]
    [InlineData("cityId", "desc", "2,1,3,4")]
    public void SearchPage_SupportedOrdering_UsesStableIdTieBreak(string sortBy, string direction, string expected)
    {
        var result = _repository.SearchPage(new ClinicSearchRequest(SortBy: sortBy, SortDirection: direction).Normalize());
        Assert.Equal(expected.Split(',').Select(long.Parse), result.Items.Select(c => c.Id));
        Assert.Equal(4, result.TotalCount);
    }

    [Fact]
    public void SearchPage_FiltersAreCombinedAndIncludeCityState()
    {
        var request = new ClinicSearchRequest(Name: "aLpH", Cnpj: "22.222.222/0001-22", CityId: 2);
        var result = _repository.SearchPage(request.Normalize());
        var clinic = Assert.Single(result.Items);
        Assert.Equal("Campinas", clinic.City.Name);
        Assert.Equal("SP", clinic.City.State.UF);
        Assert.Equal(1, result.TotalCount);
        Assert.Empty(_repository.SearchPage((request with { CityId = 1 }).Normalize()).Items);
    }

    [Fact]
    public void SearchPage_PagesAndEmptyResults_PreserveFilteredCounts()
    {
        var result = _repository.SearchPage(new ClinicSearchRequest(2, 1, "name", Name: "ALPH").Normalize());
        Assert.Equal(2, result.TotalCount);
        Assert.Equal(2, Assert.Single(result.Items).Id);
        var outside = _repository.SearchPage(new ClinicSearchRequest(3, 1, Name: "ALPH").Normalize());
        Assert.Empty(outside.Items);
        Assert.Equal(2, outside.TotalCount);
        Assert.Equal(0, _repository.SearchPage(new ClinicSearchRequest(CityId: 999).Normalize()).TotalCount);
    }

    [Fact]
    public void Search_NameWildcards_AreLiteralSubstringsInBothRoutes()
    {
        Assert.Equal(4, Assert.Single(_repository.SearchPage(new ClinicSearchRequest(Name: "%_").Normalize()).Items).Id);
        Assert.Equal(4, Assert.Single(_repository.SearchWithCity("%_", null)).Id);
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }
}
