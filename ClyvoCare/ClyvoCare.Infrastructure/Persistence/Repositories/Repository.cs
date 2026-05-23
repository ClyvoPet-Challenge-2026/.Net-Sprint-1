using ClyvoCare.Application.Repositories;
using ClyvoCare.Domain.Common;
using Microsoft.EntityFrameworkCore;

namespace ClyvoCare.Infrastructure.Persistence.Repositories;

public class Repository<T>(ClyvoCareContext context) : IRepository<T> where T : BaseEntity
{
    protected ClyvoCareContext Context { get; } = context;

    private readonly DbSet<T> _set = context.Set<T>();

    public IReadOnlyList<T> GetAll()
    {
        return _set
            .OrderBy(e => e.Id)
            .ToList();
    }

    public T? GetById(long id)
    {
        return _set.Find(id);
    }

    public T Add(T entity)
    {
        ArgumentNullException.ThrowIfNull(entity);

        _set.Add(entity);
        Context.SaveChanges();

        return entity;
    }

    public T Update(T entity)
    {
        ArgumentNullException.ThrowIfNull(entity);

        _set.Update(entity);
        Context.SaveChanges();

        return entity;
    }

    public bool Delete(long id)
    {
        var entity = GetById(id);
        if (entity is null)
            return false;

        _set.Remove(entity);
        Context.SaveChanges();

        return true;
    }

    public bool ExistsById(long id)
    {
        return _set.Count(e => e.Id == id) > 0;
    }
}
