using System.Collections.Immutable;

namespace AtmDemo.Core;

public sealed record Account(string Id, string Name, decimal Balance, string Currency);

public enum OperationKind
{
    Deposit,
    Withdrawal,
    Transfer
}

public sealed record AtmCommand(
    Guid OperationId,
    OperationKind Kind,
    decimal Amount,
    string? SourceAccountId = null,
    string? DestinationAccountId = null);

public sealed record Transaction(AtmCommand Operation, string Currency, DateTimeOffset CompletedAt);

public sealed record AtmState(
    ImmutableDictionary<string, Account> Accounts,
    ImmutableList<Transaction> Transactions);

public sealed record AtmOverview(Account[] Accounts, ImmutableList<Transaction> Transactions);

public enum AtmError
{
    InvalidOperationId,
    InvalidAmount,
    InvalidAccounts,
    AccountNotFound,
    CurrencyMismatch,
    InsufficientFunds,
    BalanceLimitExceeded,
    OperationIdConflict
}

public sealed record AtmResult(Transaction? Transaction, AtmError? Error)
{
    public static AtmResult Accepted(Transaction transaction) => new(transaction, null);

    public static AtmResult Rejected(AtmError error) => new(null, error);
}
