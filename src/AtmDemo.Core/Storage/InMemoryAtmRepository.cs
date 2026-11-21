using System.Collections.Immutable;

namespace AtmDemo.Core.Storage;

public sealed class InMemoryAtmRepository : IAtmRepository
{
    private readonly Lock gate = new();

    private AtmState state = new(
        ImmutableDictionary.CreateRange(StringComparer.Ordinal, new[]
        {
            KeyValuePair.Create("checking", new Account("checking", "Checking", 500.00m, "USD")),
            KeyValuePair.Create("savings", new Account("savings", "Savings", 250.00m, "USD"))
        }),
        ImmutableList<Transaction>.Empty);

    public AtmState Read()
    {
        lock (gate)
        {
            return state;
        }
    }

    public TResult Update<TResult>(Func<AtmState, (AtmState NextState, TResult Result)> change)
    {
        lock (gate)
        {
            var (nextState, result) = change(state);
            state = nextState;
            return result;
        }
    }
}
