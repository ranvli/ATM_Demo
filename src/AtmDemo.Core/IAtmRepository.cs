namespace AtmDemo.Core;

public interface IAtmRepository
{
    AtmState Read();

    // Apply the change to a consistent state and publish the result as one unit.
    TResult Update<TResult>(Func<AtmState, (AtmState NextState, TResult Result)> change);
}
