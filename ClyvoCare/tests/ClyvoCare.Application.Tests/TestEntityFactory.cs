using System.Reflection;
using ClyvoCare.Domain.Entities;

namespace ClyvoCare.Application.Tests;

internal static class TestEntityFactory
{
    public static State CreateState(long id, string name, string uf)
    {
        var state = Instantiate<State>();
        SetProperty(state, nameof(State.Id), id);
        SetProperty(state, nameof(State.Name), name);
        SetProperty(state, nameof(State.UF), uf);
        return state;
    }

    public static City CreateCity(long id, string name, State state)
    {
        var city = Instantiate<City>();
        SetProperty(city, nameof(City.Id), id);
        SetProperty(city, nameof(City.Name), name);
        SetProperty(city, nameof(City.StateId), state.Id);
        SetProperty(city, nameof(City.State), state);
        return city;
    }

    public static void AttachCity(Clinic clinic, City city)
    {
        SetProperty(clinic, nameof(Clinic.City), city);
    }

    public static void SetId(Clinic clinic, long id)
    {
        SetProperty(clinic, nameof(Clinic.Id), id);
    }

    private static T Instantiate<T>() =>
        (T)Activator.CreateInstance(typeof(T), nonPublic: true)!;

    private static void SetProperty<T>(T target, string propertyName, object? value)
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public
            | BindingFlags.NonPublic | BindingFlags.FlattenHierarchy;

        var property = typeof(T).GetProperty(propertyName, flags)
            ?? throw new InvalidOperationException($"Propriedade {propertyName} não encontrada em {typeof(T).Name}.");

        property.SetValue(target, value);
    }
}
