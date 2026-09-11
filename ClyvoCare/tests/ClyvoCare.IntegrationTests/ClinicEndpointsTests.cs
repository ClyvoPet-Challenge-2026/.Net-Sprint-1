using System.Net;
using System.Net.Http.Json;
using ClyvoCare.Application.DTOs;

namespace ClyvoCare.IntegrationTests;

public class ClinicEndpointsTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;

    public ClinicEndpointsTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GetAll_SemFiltro_DeveRetornar200()
    {
        //Arrange

        //Act
        var response = await _client.GetAsync("/api/clinicas");

        //Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Post_ComDadosValidos_DeveRetornar201()
    {
        //Arrange
        var request = new ClinicRequest("Vet Care Center", "11.222.333/0001-44", 1, "(11) 99999-9999");

        //Act
        var response = await _client.PostAsJsonAsync("/api/clinicas", request);

        //Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.Content.ReadFromJsonAsync<ClinicResponse>();
        Assert.NotNull(created);
        Assert.Equal("Vet Care Center", created!.Name);
        Assert.Equal("Sao Paulo", created.City.Name);
    }

    [Fact]
    public async Task Post_ComCidadeInexistente_DeveRetornar404()
    {
        //Arrange
        var request = new ClinicRequest("Clinica X", "22.333.444/0001-55", 999, null);

        //Act
        var response = await _client.PostAsJsonAsync("/api/clinicas", request);

        //Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Post_ComCnpjDuplicado_DeveRetornar400()
    {
        //Arrange
        var first = new ClinicRequest("Clinica Y", "33.444.555/0001-66", 1, null);
        await _client.PostAsJsonAsync("/api/clinicas", first);
        var duplicate = new ClinicRequest("Clinica Y2", "33.444.555/0001-66", 1, null);

        //Act
        var response = await _client.PostAsJsonAsync("/api/clinicas", duplicate);

        //Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetById_Inexistente_DeveRetornar404()
    {
        //Arrange

        //Act
        var response = await _client.GetAsync("/api/clinicas/999999");

        //Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetByCidade_ComCidadeValida_DeveRetornar200()
    {
        //Arrange

        //Act
        var response = await _client.GetAsync("/api/clinicas/por-cidade/1");

        //Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Put_Existente_DeveRetornar204()
    {
        //Arrange
        var create = new ClinicRequest("Clinica Z", "44.555.666/0001-77", 1, null);
        var createResponse = await _client.PostAsJsonAsync("/api/clinicas", create);
        var created = await createResponse.Content.ReadFromJsonAsync<ClinicResponse>();
        var update = new ClinicRequest("Clinica Z Atualizada", "44.555.666/0001-77", 1, "(11) 98888-7777");

        //Act
        var response = await _client.PutAsJsonAsync($"/api/clinicas/{created!.Id}", update);

        //Assert
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task Delete_Existente_DeveRetornar204()
    {
        //Arrange
        var create = new ClinicRequest("Clinica W", "55.666.777/0001-88", 1, null);
        var createResponse = await _client.PostAsJsonAsync("/api/clinicas", create);
        var created = await createResponse.Content.ReadFromJsonAsync<ClinicResponse>();

        //Act
        var response = await _client.DeleteAsync($"/api/clinicas/{created!.Id}");

        //Assert
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task Delete_Inexistente_DeveRetornar404()
    {
        //Arrange

        //Act
        var response = await _client.DeleteAsync("/api/clinicas/999999");

        //Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
