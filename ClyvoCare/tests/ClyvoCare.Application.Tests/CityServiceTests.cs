using ClyvoCare.Application.Repositories;
using ClyvoCare.Application.Services.Implementations;
using ClyvoCare.Domain.Entities;
using Moq;

namespace ClyvoCare.Application.Tests;

public class CityServiceTests
{
    private readonly Mock<ICityRepository> _cities = new();
    private readonly CityService _service;

    public CityServiceTests()
    {
        _service = new CityService(_cities.Object);
    }

    [Fact]
    public void GetAll_DeveRetornarTodasAsCidades()
    {
        //Arrange
        var state = TestEntityFactory.CreateState(1, "Sao Paulo", "SP");
        var city = TestEntityFactory.CreateCity(1, "Sao Paulo", state);
        _cities.Setup(r => r.GetAllWithState()).Returns(new List<City> { city });

        //Act
        var result = _service.GetAll();

        //Assert
        Assert.Single(result);
        Assert.Equal("Sao Paulo", result[0].Name);
        Assert.Equal("SP", result[0].State.UF);
    }

    [Fact]
    public void GetById_QuandoExiste_DeveRetornarCityResponse()
    {
        //Arrange
        var state = TestEntityFactory.CreateState(1, "Rio de Janeiro", "RJ");
        var city = TestEntityFactory.CreateCity(2, "Niteroi", state);
        _cities.Setup(r => r.GetByIdWithState(2)).Returns(city);

        //Act
        var result = _service.GetById(2);

        //Assert
        Assert.NotNull(result);
        Assert.Equal("Niteroi", result!.Name);
        Assert.Equal("RJ", result.State.UF);
    }

    [Fact]
    public void GetById_QuandoNaoExiste_DeveRetornarNull()
    {
        //Arrange
        _cities.Setup(r => r.GetByIdWithState(99)).Returns((City?)null);

        //Act
        var result = _service.GetById(99);

        //Assert
        Assert.Null(result);
    }
}
