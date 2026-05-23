using System.ComponentModel.DataAnnotations;
using ClyvoCare.Domain.Entities;

namespace ClyvoCare.Application.DTOs;

/// <summary>
/// DTO de requisição para criar ou atualizar uma clínica.
/// </summary>
public record ClinicRequest(
    [param:Required(ErrorMessage = "O nome é obrigatório.")]
    [param:StringLength(150, MinimumLength = 2, ErrorMessage = "O nome deve ter entre 2 e 150 caracteres.")]
    string Name,

    [param:Required(ErrorMessage = "O CNPJ é obrigatório.")]
    [param:RegularExpression(
        @"^\d{2}\.\d{3}\.\d{3}/\d{4}-\d{2}$",
        ErrorMessage = "O CNPJ deve estar no formato XX.XXX.XXX/XXXX-XX.")]
    string Cnpj,

    [param:Required(ErrorMessage = "O CityId é obrigatório.")]
    [param:Range(1, long.MaxValue, ErrorMessage = "O CityId deve ser positivo.")]
    long CityId,

    [param:StringLength(20, ErrorMessage = "O telefone deve ter no máximo 20 caracteres.")]
    string? Phone)
{
    /// <summary>
    /// Constrói a entidade <see cref="Clinic"/>.
    /// </summary>
    public Clinic ToDomain() => Clinic.Create(Name, Cnpj, CityId, Phone);
}
