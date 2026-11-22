namespace AtmDemo.Core;

public interface IAtmRepository
{
    /// <summary>Returns an immutable snapshot of balances and history.</summary>
    AtmState Read();

    /// <summary>Runs a change against the latest snapshot and publishes the next state as one update.</summary>
    /// <remarks>If the change throws, the previous snapshot remains visible.</remarks>
    /// <param name="change">The function that returns the next state and the operation result.</param>
    /// <typeparam name="TResult">The operation result type.</typeparam>
    /// <returns>The result supplied by the change.</returns>
    TResult Update<TResult>(Func<AtmState, (AtmState NextState, TResult Result)> change);
}
