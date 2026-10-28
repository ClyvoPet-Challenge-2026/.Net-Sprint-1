using System.Diagnostics;
using ClyvoCare.Application.Diagnostics;
using ClyvoCare.Application.Repositories;
using ClyvoCare.Application.Services.Implementations;
using ClyvoCare.Domain.Entities;

namespace ClyvoCare.Application.Tests;

public sealed class TracingTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Service_PreservesParentTrace_AndRestoresContext(bool fail)
    {
        using var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == ApplicationDiagnostics.SourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData
        };
        ActivitySource.AddActivityListener(listener);
        using var parent = new Activity("request").SetIdFormat(ActivityIdFormat.W3C).Start();
        var repository = new ObservingRepository { Fail = fail };
        var service = new StateService(repository);

        if (fail)
            Assert.Throws<InvalidOperationException>(() => service.GetAll());
        else
            Assert.Empty(service.GetAll());

        Assert.NotNull(repository.Observed);
        Assert.Equal("StateService.GetAll", repository.Observed!.OperationName);
        Assert.Equal(parent.TraceId, repository.Observed.TraceId);
        Assert.Equal(parent.SpanId, repository.Observed.ParentSpanId);
        Assert.Same(parent, Activity.Current);
        Assert.Empty(repository.Observed.TagObjects);
    }

    private sealed class ObservingRepository : IRepository<State>
    {
        public bool Fail { get; init; }
        public Activity? Observed { get; private set; }

        public IReadOnlyList<State> GetAll()
        {
            Observed = Activity.Current;
            if (Fail) throw new InvalidOperationException("Falha simulada de persistência.");
            return [];
        }

        public State? GetById(long id) => throw new NotSupportedException();
        public State Add(State entity) => throw new NotSupportedException();
        public State Update(State entity) => throw new NotSupportedException();
        public bool Delete(long id) => throw new NotSupportedException();
        public bool ExistsById(long id) => throw new NotSupportedException();
    }
}
