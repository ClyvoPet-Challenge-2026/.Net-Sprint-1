using ClyvoCare.Domain.Common;
using ClyvoCare.Domain.Exceptions;

namespace ClyvoCare.Domain.Entities;

/// <summary>
/// Clínica veterinária credenciada no plano.
/// </summary>
public sealed class Clinic : BaseEntity
{
    public string Name { get; private set; } = null!;

    public string Cnpj { get; private set; } = null!;

    public long CityId { get; private set; }

    public City City { get; private set; } = null!;

    public string? Phone { get; private set; }

    private Clinic()
    {
    }

    /// <summary>
    /// Cria uma nova clínica aplicando as regras de domínio.
    /// </summary>
    public static Clinic Create(string name, string cnpj, long cityId, string? phone)
    {
        var clinic = new Clinic();
        clinic.Apply(name, cnpj, cityId, phone);
        return clinic;
    }

    /// <summary>
    /// Atualiza os dados da clínica.
    /// </summary>
    public void Update(string name, string cnpj, long cityId, string? phone)
    {
        Apply(name, cnpj, cityId, phone);
    }

    private void Apply(string name, string cnpj, long cityId, string? phone)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException("Nome da clínica é obrigatório.");

        if (string.IsNullOrWhiteSpace(cnpj))
            throw new DomainException("CNPJ é obrigatório.");

        if (cityId <= 0)
            throw new DomainException("CityId é obrigatório.");

        Name = name.Trim();
        Cnpj = cnpj.Trim();
        CityId = cityId;
        Phone = string.IsNullOrWhiteSpace(phone) ? null : phone.Trim();
    }
}
