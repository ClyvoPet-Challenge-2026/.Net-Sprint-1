using ClyvoCare.Application.DTOs;
using ClyvoCare.Application.Repositories;
using ClyvoCare.Application.Services.Implementations;
using ClyvoCare.Domain.Entities;
using Moq;

namespace ClyvoCare.Application.Tests;

public class ClinicSearchTests
{
    [Fact]
    public void Normalize_WhitespaceAndCase_PreservesNormalizedFilters()
    {
        var search = new ClinicSearchRequest(SortBy: " CITYID ", SortDirection: " DESC ",
            Name: " Vet ", Cnpj: " 11.111.111/0001-11 ", CityId: 2).Normalize();
        Assert.Equal("cityid", search.SortBy);
        Assert.Equal("desc", search.SortDirection);
        Assert.Equal("Vet", search.Name);
        Assert.Equal("11.111.111/0001-11", search.Cnpj);
        Assert.Equal(2, search.CityId);
    }

    [Theory]
    [InlineData(0, 20)]
    [InlineData(-1, 20)]
    [InlineData(1, 0)]
    [InlineData(1, 101)]
    [InlineData(int.MaxValue, 100)]
    public void Normalize_InvalidPagination_Throws(int page, int size)
    {
        Assert.Throws<ArgumentException>(() => new ClinicSearchRequest(page, size).Normalize());
    }

    [Theory]
    [InlineData("unknown", "asc")]
    [InlineData("name; DROP TABLE", "asc")]
    [InlineData("name", "invalid")]
    [InlineData("", "asc")]
    public void Normalize_UnsupportedOrdering_Throws(string sortBy, string direction)
    {
        Assert.Throws<ArgumentException>(() => new ClinicSearchRequest(SortBy: sortBy, SortDirection: direction).Normalize());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Normalize_InvalidCity_Throws(long cityId)
    {
        Assert.Throws<ArgumentException>(() => new ClinicSearchRequest(CityId: cityId).Normalize());
    }

    [Fact]
    public void Normalize_InvalidTextFilters_ThrowsAndBlankFiltersBecomeNull()
    {
        Assert.Throws<ArgumentException>(() => new ClinicSearchRequest(Name: new string('x', 151)).Normalize());
        Assert.Throws<ArgumentException>(() => new ClinicSearchRequest(Cnpj: "11111111000111").Normalize());
        var empty = new ClinicSearchRequest(Name: " ", Cnpj: " ").Normalize();
        Assert.Null(empty.Name);
        Assert.Null(empty.Cnpj);
    }

    [Fact]
    public void SearchPage_ValidRequest_MapsResultsAndPreservesDatabaseCount()
    {
        var clinics = new Mock<IClinicRepository>();
        var service = new ClinicService(clinics.Object, Mock.Of<IRepository<City>>());
        var state = TestEntityFactory.CreateState(1, "Sao Paulo", "SP");
        var city = TestEntityFactory.CreateCity(1, "Sao Paulo", state);
        var clinic = Clinic.Create("Vet", "11.111.111/0001-11", 1, null);
        TestEntityFactory.SetId(clinic, 10);
        TestEntityFactory.AttachCity(clinic, city);
        var expectedSearch = new ClinicSearchRequest(2, 1, "name", "asc", "Vet");
        clinics.Setup(c => c.SearchPage(expectedSearch))
            .Returns((new[] { clinic }, 3));

        var result = service.SearchPage(new ClinicSearchRequest(2, 1, " NAME ", " ASC ", " Vet "));

        Assert.Equal(10, Assert.Single(result.Items).Id);
        Assert.Equal("SP", result.Items[0].City.State.UF);
        Assert.Equal(3, result.TotalCount);
        Assert.Equal(3, result.TotalPages);
        Assert.Equal(2, result.PageNumber);
    }

    [Fact]
    public void SearchPage_InvalidRequest_DoesNotReachRepository()
    {
        var clinics = new Mock<IClinicRepository>();
        var service = new ClinicService(clinics.Object, Mock.Of<IRepository<City>>());
        Assert.Throws<ArgumentException>(() => service.SearchPage(new ClinicSearchRequest(PageSize: 101)));
        clinics.VerifyNoOtherCalls();
    }
}
