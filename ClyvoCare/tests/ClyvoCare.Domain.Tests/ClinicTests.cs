using ClyvoCare.Domain.Entities;
using ClyvoCare.Domain.Exceptions;

namespace ClyvoCare.Domain.Tests;

public class ClinicTests
{
    [Fact]
    public void Create_ComDadosValidos_DeveCriarClinica()
    {
        //Arrange
        const string name = "Vet Care Center";
        const string cnpj = "99.999.999/0001-99";
        const long cityId = 1;
        const string phone = "(11) 99999-9999";

        //Act
        var clinic = Clinic.Create(name, cnpj, cityId, phone);

        //Assert
        Assert.Equal(name, clinic.Name);
        Assert.Equal(cnpj, clinic.Cnpj);
        Assert.Equal(cityId, clinic.CityId);
        Assert.Equal(phone, clinic.Phone);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public void Create_ComNomeInvalido_DeveLancarDomainException(string? name)
    {
        //Arrange

        //Act
        var act = () => Clinic.Create(name!, "99.999.999/0001-99", 1, null);

        //Assert
        var ex = Assert.Throws<DomainException>(act);
        Assert.Equal("Nome da clínica é obrigatório.", ex.Message);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public void Create_ComCnpjInvalido_DeveLancarDomainException(string? cnpj)
    {
        //Arrange

        //Act
        var act = () => Clinic.Create("Vet Care Center", cnpj!, 1, null);

        //Assert
        var ex = Assert.Throws<DomainException>(act);
        Assert.Equal("CNPJ é obrigatório.", ex.Message);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Create_ComCityIdInvalido_DeveLancarDomainException(long cityId)
    {
        //Arrange

        //Act
        var act = () => Clinic.Create("Vet Care Center", "99.999.999/0001-99", cityId, null);

        //Assert
        var ex = Assert.Throws<DomainException>(act);
        Assert.Equal("CityId é obrigatório.", ex.Message);
    }

    [Fact]
    public void Create_ComEspacosNosCampos_DeveFazerTrim()
    {
        //Arrange
        const string name = "  Vet Care Center  ";
        const string cnpj = "  99.999.999/0001-99  ";
        const string phone = "  (11) 99999-9999  ";

        //Act
        var clinic = Clinic.Create(name, cnpj, 1, phone);

        //Assert
        Assert.Equal("Vet Care Center", clinic.Name);
        Assert.Equal("99.999.999/0001-99", clinic.Cnpj);
        Assert.Equal("(11) 99999-9999", clinic.Phone);
    }

    [Fact]
    public void Create_ComTelefoneEmBranco_DeveGuardarNulo()
    {
        //Arrange

        //Act
        var clinic = Clinic.Create("Vet Care Center", "99.999.999/0001-99", 1, "   ");

        //Assert
        Assert.Null(clinic.Phone);
    }

    [Fact]
    public void Update_ComDadosValidos_DeveAtualizarClinica()
    {
        //Arrange
        var clinic = Clinic.Create("Vet Care Center", "99.999.999/0001-99", 1, "(11) 99999-9999");

        //Act
        clinic.Update("Nova Clínica", "11.111.111/0001-11", 2, "(11) 98888-8888");

        //Assert
        Assert.Equal("Nova Clínica", clinic.Name);
        Assert.Equal("11.111.111/0001-11", clinic.Cnpj);
        Assert.Equal(2, clinic.CityId);
        Assert.Equal("(11) 98888-8888", clinic.Phone);
    }

    [Fact]
    public void Update_ComNomeInvalido_DeveLancarDomainExceptionESemAlterarEstado()
    {
        //Arrange
        var clinic = Clinic.Create("Vet Care Center", "99.999.999/0001-99", 1, null);

        //Act
        var act = () => clinic.Update("", "11.111.111/0001-11", 2, null);

        //Assert
        Assert.Throws<DomainException>(act);
        Assert.Equal("Vet Care Center", clinic.Name);
        Assert.Equal("99.999.999/0001-99", clinic.Cnpj);
        Assert.Equal(1, clinic.CityId);
    }
}
