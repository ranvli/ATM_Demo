using System.Collections.Immutable;

namespace AtmDemo.Core;

public sealed class AtmService(IAtmRepository repository, TimeProvider clock)
{
    // Keep amounts small enough for JavaScript to distinguish every cent.
    private const decimal MaximumBalance = 9_999_999_999_999.99m;

    public Account[] GetAccounts() => OrderedAccounts(repository.Read());

    public ImmutableList<Transaction> GetTransactions() => repository.Read().Transactions;

    public AtmOverview GetOverview()
    {
        var state = repository.Read();
        return new(OrderedAccounts(state), state.Transactions);
    }

    public AtmResult Execute(AtmCommand command)
    {
        if (Validate(command) is { } error)
        {
            return AtmResult.Rejected(error);
        }

        return repository.Update(state => Apply(state, command));
    }

    private static Account[] OrderedAccounts(AtmState state) =>
        state.Accounts.Values.OrderBy(account => account.Id, StringComparer.Ordinal).ToArray();

    private static AtmError? Validate(AtmCommand command)
    {
        if (command.OperationId == Guid.Empty)
        {
            return AtmError.InvalidOperationId;
        }

        if (command.Amount <= 0 || command.Amount > MaximumBalance ||
            command.Amount != decimal.Round(command.Amount, 2))
        {
            return AtmError.InvalidAmount;
        }

        var hasSource = !string.IsNullOrWhiteSpace(command.SourceAccountId);
        var hasDestination = !string.IsNullOrWhiteSpace(command.DestinationAccountId);
        var validAccounts = command.Kind switch
        {
            OperationKind.Deposit => command.SourceAccountId is null && hasDestination,
            OperationKind.Withdrawal => hasSource && command.DestinationAccountId is null,
            OperationKind.Transfer => hasSource && hasDestination &&
                command.SourceAccountId != command.DestinationAccountId,
            _ => false
        };

        return validAccounts ? null : AtmError.InvalidAccounts;
    }

    private (AtmState NextState, AtmResult Result) Apply(AtmState state, AtmCommand command)
    {
        // Look up retries before checking funds. The balance may have changed since the first request.
        var previous = state.Transactions.Find(transaction => transaction.Operation.OperationId == command.OperationId);
        if (previous is not null)
        {
            var result = previous.Operation == command
                ? AtmResult.Accepted(previous)
                : AtmResult.Rejected(AtmError.OperationIdConflict);
            return (state, result);
        }

        var accounts = state.Accounts;
        Account? source = null;
        Account? destination = null;

        if ((command.SourceAccountId is { } sourceId && !accounts.TryGetValue(sourceId, out source)) ||
            (command.DestinationAccountId is { } destinationId && !accounts.TryGetValue(destinationId, out destination)))
        {
            return (state, AtmResult.Rejected(AtmError.AccountNotFound));
        }

        if (source is not null && destination is not null && source.Currency != destination.Currency)
        {
            return (state, AtmResult.Rejected(AtmError.CurrencyMismatch));
        }

        if (source is not null && source.Balance < command.Amount)
        {
            return (state, AtmResult.Rejected(AtmError.InsufficientFunds));
        }

        if (destination is not null && destination.Balance > MaximumBalance - command.Amount)
        {
            return (state, AtmResult.Rejected(AtmError.BalanceLimitExceeded));
        }

        if (source is not null)
        {
            accounts = accounts.SetItem(source.Id, source with { Balance = source.Balance - command.Amount });
        }

        if (destination is not null)
        {
            accounts = accounts.SetItem(destination.Id, destination with { Balance = destination.Balance + command.Amount });
        }

        var transaction = new Transaction(command, (source ?? destination)!.Currency, clock.GetUtcNow());
        var nextState = new AtmState(accounts, state.Transactions.Insert(0, transaction));
        return (nextState, AtmResult.Accepted(transaction));
    }
}
