using ClyvoCare.Application.DTOs;
using ClyvoCare.Application.Repositories;
using ClyvoCare.Application.Services.Implementations;
using ClyvoCare.Application.Services.Interfaces;
using ClyvoCare.Domain.Entities;
using Moq;

namespace ClyvoCare.Application.Tests;

public sealed class ClinicSearchTests
{
    [Theory]
    [InlineData(0, 20, "id", "asc", null)]
    [InlineData(1, 0, "id", "asc", null)]
    [InlineData(1, 101, "id", "asc", null)]
    [InlineData(int.MaxValue, 100, "id", "asc", null)]
    [InlineData(1, 20, "invalid", "asc", null)]
    [InlineData(1, 20, "id", "invalid", null)]
    [InlineData(1, 20, "id", "asc", 0L)]
    public void SearchPage_ComParametrosInvalidos_NaoDeveConsultarBanco(
        int page, int size, string sort, string direction, long? cityId)
    {
        var repository = new Mock<IClinicRepository>(MockBehavior.Strict);
        var service = CreateService(repository.Object);

        Assert.Throws<ArgumentException>(() => service.SearchPage(
            new ClinicSearchRequest(page, size, sort, direction, CityId: cityId)));

        repository.VerifyNoOtherCalls();
    }

    [Fact]
    public void SearchPage_DeveNormalizarFiltrosEPreservarTotal()
    {
        var repository = new Mock<IClinicRepository>();
        var normalized = new ClinicSearchRequest(2, 5, "name", "desc", "Vet", "123", 1);
        repository.Setup(r => r.SearchPage(normalized)).Returns((Array.Empty<Clinic>(), 8));
        var service = CreateService(repository.Object);

        var page = service.SearchPage(new ClinicSearchRequest(2, 5, " NAME ", " DESC ", " Vet ", " 123 ", 1));

        Assert.Equal(8, page.TotalCount);
        Assert.Equal(2, page.TotalPages);
        Assert.Equal(2, page.PageNumber);
        Assert.Equal(5, page.PageSize);
        repository.Verify(r => r.SearchPage(normalized), Times.Once);
    }

    private static ClinicService CreateService(IClinicRepository repository) => new(repository,
        Mock.Of<IRepository<City>>(), Mock.Of<IClinicAuditRepository>(),
        Mock.Of<ICurrentUser>(), TimeProvider.System);
}
