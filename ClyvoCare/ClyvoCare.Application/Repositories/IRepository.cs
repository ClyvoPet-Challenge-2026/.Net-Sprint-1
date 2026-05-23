using ClyvoCare.Domain.Common;

namespace ClyvoCare.Application.Repositories;

/// <summary>
/// Contrato genérico de persistência para entidades que derivam de <see cref="BaseEntity"/>.
/// </summary>
/// <typeparam name="T">Tipo da entidade de domínio.</typeparam>
public interface IRepository<T> where T : BaseEntity
{
    IReadOnlyList<T> GetAll();

    T? GetById(long id);

    T Add(T entity);

    T Update(T entity);

    bool Delete(long id);

    bool ExistsById(long id);
}
