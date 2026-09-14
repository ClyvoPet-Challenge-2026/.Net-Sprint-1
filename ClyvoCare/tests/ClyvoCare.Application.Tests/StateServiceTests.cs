using ClyvoCare.Application.Repositories;
using ClyvoCare.Application.Services.Implementations;
using ClyvoCare.Domain.Entities;
using Moq;

namespace ClyvoCare.Application.Tests;

public class StateServiceTests
{
    private readonly Mock<IRepository<State>> _states = new();
    private readonly StateService _service;

    public StateServiceTests()
    {
        _service = new StateService(_states.Object);
    }

    [Fact]
    public void GetAll_DeveRetornarTodosOsEstados()
    {
        //Arrange
        var state = TestEntityFactory.CreateState(1, "Sao Paulo", "SP");
        _states.Setup(r => r.GetAll()).Returns(new List<State> { state });

        //Act
        var result = _service.GetAll();

        //Assert
        Assert.Single(result);
        Assert.Equal("SP", result[0].UF);
    }

    [Fact]
    public void GetById_QuandoExiste_DeveRetornarStateResponse()
    {
        //Arrange
        var state = TestEntityFactory.CreateState(3, "Minas Gerais", "MG");
        _states.Setup(r => r.GetById(3)).Returns(state);

        //Act
        var result = _service.GetById(3);

        //Assert
        Assert.NotNull(result);
        Assert.Equal("Minas Gerais", result!.Name);
        Assert.Equal("MG", result.UF);
    }

    [Fact]
    public void GetById_QuandoNaoExiste_DeveRetornarNull()
    {
        //Arrange
        _states.Setup(r => r.GetById(99)).Returns((State?)null);

        //Act
        var result = _service.GetById(99);

        //Assert
        Assert.Null(result);
    }
}
