using ClyvoCare.Application.Repositories;
using ClyvoCare.Application.Services.Implementations;
using ClyvoCare.Application.Services.Interfaces;
using ClyvoCare.Infrastructure.Persistence;
using ClyvoCare.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;

namespace ClyvoCare.API.Extensions;

/// <summary>
/// Extensões para registrar persistência, repositórios e serviços da aplicação na injeção de dependências.
/// </summary>
public static class ClyvoCareServiceCollectionExtensions
{
    /// <summary>
    /// Registra o <see cref="ClyvoCareContext"/> com Oracle FIAP.
    /// </summary>
    /// <param name="services">Coleção de serviços da aplicação.</param>
    /// <param name="configuration">Configuração (appsettings, variáveis de ambiente, etc.).</param>
    /// <param name="connectionStringName">Chave em ConnectionStrings para Oracle (padrão: ClyvoCareOracle).</param>
    /// <returns>A mesma instância de <see cref="IServiceCollection"/> para encadeamento.</returns>
    /// <exception cref="InvalidOperationException">Quando a connection string não for encontrada.</exception>
    public static IServiceCollection AddClyvoCareDbContext(
        this IServiceCollection services,
        IConfiguration configuration,
        string connectionStringName = "ClyvoCareOracle")
    {
        var connectionString = configuration.GetConnectionString(connectionStringName)
            ?? throw new InvalidOperationException(
                $"Connection string '{connectionStringName}' não encontrada. Configure em appsettings.json ou no ambiente.");

        services.AddDbContext<ClyvoCareContext>(options =>
            options.UseOracle(connectionString));

        return services;
    }

    /// <summary>
    /// Registra as implementações de repositório como <c>Scoped</c> (um por requisição HTTP).
    /// </summary>
    public static IServiceCollection AddClyvoCareRepositories(this IServiceCollection services)
    {
        services.AddScoped(typeof(IRepository<>), typeof(Repository<>));
        services.AddScoped<ICityRepository, CityRepository>();
        services.AddScoped<IClinicRepository, ClinicRepository>();

        return services;
    }

    /// <summary>
    /// Adiciona os serviços de aplicação que orquestram repositórios.
    /// Serviços específicos por entidade serão registrados aqui conforme forem implementados.
    /// </summary>
    public static IServiceCollection AddClyvoCareApplicationServices(this IServiceCollection services)
    {
        services.AddScoped<IStateService, StateService>();
        services.AddScoped<ICityService, CityService>();
        services.AddScoped<IClinicService, ClinicService>();

        return services;
    }
}
