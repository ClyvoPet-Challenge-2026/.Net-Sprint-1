using ClyvoCare.Application.DTOs;
using ClyvoCare.Application.Repositories;
using ClyvoCare.Application.Services.Implementations;
using ClyvoCare.Domain.Entities;
using Moq;

namespace ClyvoCare.Application.Tests;

public class ClinicServiceTests
{
    private readonly Mock<IClinicRepository> _clinics = new();
    private readonly Mock<IRepository<City>> _cities = new();
    private readonly ClinicService _service;

    public ClinicServiceTests()
    {
        _service = new ClinicService(_clinics.Object, _cities.Object);
    }

    private static ClinicRequest ValidRequest() =>
        new("Vet Care Center", "99.999.999/0001-99", 1, "(11) 99999-9999");

    private static Clinic BuildClinicWithCity(long id, City city)
    {
        var clinic = Clinic.Create("Vet Care Center", "99.999.999/0001-99", city.Id, null);
        TestEntityFactory.SetId(clinic, id);
        TestEntityFactory.AttachCity(clinic, city);
        return clinic;
    }

    [Fact]
    public void GetById_QuandoNaoExiste_DeveRetornarNull()
    {
        //Arrange
        _clinics.Setup(r => r.GetByIdWithCity(1)).Returns((Clinic?)null);

        //Act
        var result = _service.GetById(1);

        //Assert
        Assert.Null(result);
    }

    [Fact]
    public void GetById_QuandoExiste_DeveRetornarClinicResponse()
    {
        //Arrange
        var state = TestEntityFactory.CreateState(1, "Sao Paulo", "SP");
        var city = TestEntityFactory.CreateCity(1, "Sao Paulo", state);
        var clinic = BuildClinicWithCity(10, city);
        _clinics.Setup(r => r.GetByIdWithCity(10)).Returns(clinic);

        //Act
        var result = _service.GetById(10);

        //Assert
        Assert.NotNull(result);
        Assert.Equal(10, result!.Id);
        Assert.Equal("Vet Care Center", result.Name);
        Assert.Equal("Sao Paulo", result.City.Name);
    }

    [Fact]
    public void Create_QuandoCidadeNaoExiste_DeveLancarKeyNotFoundENaoPersistir()
    {
        //Arrange
        var request = ValidRequest();
        _cities.Setup(r => r.ExistsById(request.CityId)).Returns(false);

        //Act
        var act = () => _service.Create(request);

        //Assert
        var ex = Assert.Throws<KeyNotFoundException>(act);
        Assert.Equal("Cidade com ID 1 não encontrada.", ex.Message);
        _clinics.Verify(r => r.Add(It.IsAny<Clinic>()), Times.Never);
    }

    [Fact]
    public void Create_QuandoCnpjDuplicado_DeveLancarInvalidOperationENaoPersistir()
    {
        //Arrange
        var request = ValidRequest();
        _cities.Setup(r => r.ExistsById(request.CityId)).Returns(true);
        _clinics.Setup(r => r.ExistsByCnpj(request.Cnpj, null)).Returns(true);

        //Act
        var act = () => _service.Create(request);

        //Assert
        var ex = Assert.Throws<InvalidOperationException>(act);
        Assert.Equal("Já existe uma clínica cadastrada com o CNPJ 99.999.999/0001-99.", ex.Message);
        _clinics.Verify(r => r.Add(It.IsAny<Clinic>()), Times.Never);
    }

    [Fact]
    public void Create_ComDadosValidos_DevePersistirERetornarResponse()
    {
        //Arrange
        var request = ValidRequest();
        var state = TestEntityFactory.CreateState(1, "Sao Paulo", "SP");
        var city = TestEntityFactory.CreateCity(1, "Sao Paulo", state);
        var saved = BuildClinicWithCity(10, city);

        _cities.Setup(r => r.ExistsById(request.CityId)).Returns(true);
        _clinics.Setup(r => r.ExistsByCnpj(request.Cnpj, null)).Returns(false);
        _clinics.Setup(r => r.Add(It.IsAny<Clinic>())).Returns(saved);
        _clinics.Setup(r => r.GetByIdWithCity(10)).Returns(saved);

        //Act
        var result = _service.Create(request);

        //Assert
        Assert.Equal(10, result.Id);
        Assert.Equal(request.Name, result.Name);
        Assert.Equal("Sao Paulo", result.City.Name);
        _clinics.Verify(r => r.Add(It.IsAny<Clinic>()), Times.Once);
    }

    [Fact]
    public void Update_QuandoClinicaNaoExiste_DeveLancarKeyNotFound()
    {
        //Arrange
        _clinics.Setup(r => r.GetById(99)).Returns((Clinic?)null);

        //Act
        var act = () => _service.Update(99, ValidRequest());

        //Assert
        var ex = Assert.Throws<KeyNotFoundException>(act);
        Assert.Equal("Clínica com ID 99 não encontrada.", ex.Message);
        _clinics.Verify(r => r.Update(It.IsAny<Clinic>()), Times.Never);
    }

    [Fact]
    public void Update_QuandoCidadeNaoExiste_DeveLancarKeyNotFoundENaoPersistir()
    {
        //Arrange
        var state = TestEntityFactory.CreateState(1, "Sao Paulo", "SP");
        var city = TestEntityFactory.CreateCity(1, "Sao Paulo", state);
        var existing = BuildClinicWithCity(10, city);
        var request = new ClinicRequest("Vet Care Center", "99.999.999/0001-99", 2, null);

        _clinics.Setup(r => r.GetById(10)).Returns(existing);
        _cities.Setup(r => r.ExistsById(request.CityId)).Returns(false);

        //Act
        var act = () => _service.Update(10, request);

        //Assert
        Assert.Throws<KeyNotFoundException>(act);
        _clinics.Verify(r => r.Update(It.IsAny<Clinic>()), Times.Never);
    }

    [Fact]
    public void Update_QuandoCnpjPertenceAOutraClinica_DeveLancarInvalidOperation()
    {
        //Arrange
        var state = TestEntityFactory.CreateState(1, "Sao Paulo", "SP");
        var city = TestEntityFactory.CreateCity(1, "Sao Paulo", state);
        var existing = BuildClinicWithCity(10, city);
        var request = ValidRequest();

        _clinics.Setup(r => r.GetById(10)).Returns(existing);
        _cities.Setup(r => r.ExistsById(request.CityId)).Returns(true);
        _clinics.Setup(r => r.ExistsByCnpj(request.Cnpj, 10)).Returns(true);

        //Act
        var act = () => _service.Update(10, request);

        //Assert
        Assert.Throws<InvalidOperationException>(act);
        _clinics.Verify(r => r.Update(It.IsAny<Clinic>()), Times.Never);
    }

    [Fact]
    public void GetByCityId_QuandoCidadeNaoExiste_DeveLancarKeyNotFound()
    {
        //Arrange
        _cities.Setup(r => r.ExistsById(1)).Returns(false);

        //Act
        var act = () => _service.GetByCityId(1);

        //Assert
        Assert.Throws<KeyNotFoundException>(act);
    }

    [Fact]
    public void GetByCityId_QuandoCidadeExiste_DeveRetornarClinicas()
    {
        //Arrange
        var state = TestEntityFactory.CreateState(1, "Sao Paulo", "SP");
        var city = TestEntityFactory.CreateCity(1, "Sao Paulo", state);
        var clinic = BuildClinicWithCity(10, city);

        _cities.Setup(r => r.ExistsById(1)).Returns(true);
        _clinics.Setup(r => r.GetByCityIdWithCity(1)).Returns(new List<Clinic> { clinic });

        //Act
        var result = _service.GetByCityId(1);

        //Assert
        Assert.Single(result);
        Assert.Equal(10, result[0].Id);
    }

    [Fact]
    public void Delete_QuandoExiste_DeveRetornarTrue()
    {
        //Arrange
        _clinics.Setup(r => r.Delete(10)).Returns(true);

        //Act
        var result = _service.Delete(10);

        //Assert
        Assert.True(result);
    }

    [Fact]
    public void Delete_QuandoNaoExiste_DeveRetornarFalse()
    {
        //Arrange
        _clinics.Setup(r => r.Delete(99)).Returns(false);

        //Act
        var result = _service.Delete(99);

        //Assert
        Assert.False(result);
    }
}
