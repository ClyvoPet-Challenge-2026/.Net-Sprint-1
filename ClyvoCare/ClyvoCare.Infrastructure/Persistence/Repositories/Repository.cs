using System.Diagnostics;
using ClyvoCare.Infrastructure.Diagnostics;
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
        using var activity = PersistenceTelemetry.Source.StartActivity($"Repository.{typeof(T).Name}.GetAll", ActivityKind.Client);
        return _set
            .OrderBy(e => e.Id)
            .ToList();
    }

    public T? GetById(long id)
    {
        using var activity = PersistenceTelemetry.Source.StartActivity($"Repository.{typeof(T).Name}.GetById", ActivityKind.Client);
        return _set.Find(id);
    }

    public T Add(T entity)
    {
        using var activity = PersistenceTelemetry.Source.StartActivity($"Repository.{typeof(T).Name}.Add", ActivityKind.Client);
        ArgumentNullException.ThrowIfNull(entity);

        _set.Add(entity);
        Context.SaveChanges();

        return entity;
    }

    public T Update(T entity)
    {
        using var activity = PersistenceTelemetry.Source.StartActivity($"Repository.{typeof(T).Name}.Update", ActivityKind.Client);
        ArgumentNullException.ThrowIfNull(entity);

        _set.Update(entity);
        Context.SaveChanges();

        return entity;
    }

    public bool Delete(long id)
    {
        using var activity = PersistenceTelemetry.Source.StartActivity($"Repository.{typeof(T).Name}.Delete", ActivityKind.Client);
        var entity = GetById(id);
        if (entity is null)
            return false;

        _set.Remove(entity);
        Context.SaveChanges();

        return true;
    }

    public bool ExistsById(long id)
    {
        using var activity = PersistenceTelemetry.Source.StartActivity($"Repository.{typeof(T).Name}.ExistsById", ActivityKind.Client);
        return _set.Count(e => e.Id == id) > 0;
    }
}
