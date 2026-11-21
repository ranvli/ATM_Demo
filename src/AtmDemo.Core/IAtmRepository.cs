namespace AtmDemo.Core;

public interface IAtmRepository
{
    /// <summary>Returns balances and history from the same point in time.</summary>
    AtmState Read();

    /// <summary>
    /// Runs the change on the latest state and commits the returned balances and history together.
    /// The change must finish successfully before the new state is visible.
    /// </summary>
    TResult Update<TResult>(Func<AtmState, (AtmState NextState, TResult Result)> change);
}
