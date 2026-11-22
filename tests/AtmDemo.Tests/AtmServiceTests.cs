using System.Globalization;
using AtmDemo.Core;
using AtmDemo.Core.Storage;
using Microsoft.VisualStudio.TestTools.UnitTesting;

[assembly: Parallelize(Scope = ExecutionScope.MethodLevel)]

namespace AtmDemo.Tests;

[TestClass]
public sealed class AtmServiceTests
{
    private static readonly DateTimeOffset CompletedAt = new(2026, 11, 20, 12, 0, 0, TimeSpan.Zero);
    private readonly InMemoryAtmRepository repository = new();
    private readonly AtmService atm;

    public AtmServiceTests()
    {
        atm = new AtmService(repository, new FixedClock());
    }

    [TestMethod]
    public void DepositAddsFundsAndRecordsTheMovement()
    {
        var command = new AtmCommand(Guid.NewGuid(), OperationKind.Deposit, 25.50m, DestinationAccountId: "checking");

        var result = atm.Execute(command);

        Assert.IsNull(result.Error);
        Assert.AreEqual(525.50m, Balance("checking"));
        Assert.AreEqual(250m, Balance("savings"));
        Assert.AreEqual(new Transaction(command, "USD", CompletedAt), atm.GetTransactions().Single());
    }

    [TestMethod]
    public void WithdrawalSubtractsFundsAndRecordsTheMovement()
    {
        var command = new AtmCommand(Guid.NewGuid(), OperationKind.Withdrawal, 10m, SourceAccountId: "savings");

        var result = atm.Execute(command);

        Assert.IsNull(result.Error);
        Assert.AreEqual(240m, Balance("savings"));
        Assert.AreEqual(command, atm.GetTransactions().Single().Operation);
    }

    [TestMethod]
    [Description("The transfer keeps the combined balance at $750 and records one receipt.")]
    public void TransferPublishesBothBalancesAndHistoryTogether()
    {
        var result = atm.Execute(new(Guid.NewGuid(), OperationKind.Transfer, 100.25m, "checking", "savings"));
        var overview = atm.GetOverview();

        Assert.IsNull(result.Error);
        Assert.AreEqual(399.75m, overview.Accounts.Single(account => account.Id == "checking").Balance);
        Assert.AreEqual(350.25m, overview.Accounts.Single(account => account.Id == "savings").Balance);
        Assert.AreEqual(750m, overview.Accounts.Sum(account => account.Balance));
        Assert.AreEqual(result.Transaction, overview.Transactions.Single());
    }

    [TestMethod]
    [Description("An overdrawn transfer leaves the original snapshot in place.")]
    public void InsufficientFundsLeavesBothAccountsAndHistoryUnchanged()
    {
        var before = repository.Read();

        var result = atm.Execute(new(Guid.NewGuid(), OperationKind.Transfer, 500.01m, "checking", "savings"));

        Assert.AreEqual(AtmError.InsufficientFunds, result.Error);
        Assert.AreSame(before, repository.Read());
    }

    [TestMethod]
    [Description("Zero, negative, fractional-cent and out-of-range amounts are rejected before any write.")]
    [DataRow("0")]
    [DataRow("-0.01")]
    [DataRow("10.001")]
    [DataRow("10000000000000")]
    public void InvalidAmountsAreRejectedWithoutAWrite(string input)
    {
        var before = repository.Read();
        var amount = decimal.Parse(input, CultureInfo.InvariantCulture);

        var result = atm.Execute(new(Guid.NewGuid(), OperationKind.Deposit, amount, DestinationAccountId: "checking"));

        Assert.AreEqual(AtmError.InvalidAmount, result.Error);
        Assert.AreSame(before, repository.Read());
    }

    [TestMethod]
    public void TransferToTheSameAccountIsRejected()
    {
        var result = atm.Execute(new(Guid.NewGuid(), OperationKind.Transfer, 10m, "checking", "checking"));

        Assert.AreEqual(AtmError.InvalidAccounts, result.Error);
        Assert.IsEmpty(atm.GetTransactions());
    }

    [TestMethod]
    public void UnknownAccountIsRejected()
    {
        var result = atm.Execute(new(Guid.NewGuid(), OperationKind.Deposit, 10m, DestinationAccountId: "unknown"));

        Assert.AreEqual(AtmError.AccountNotFound, result.Error);
        Assert.IsEmpty(atm.GetTransactions());
    }

    [TestMethod]
    public void EmptyOperationIdIsRejected()
    {
        var result = atm.Execute(new(Guid.Empty, OperationKind.Withdrawal, 10m, SourceAccountId: "checking"));

        Assert.AreEqual(AtmError.InvalidOperationId, result.Error);
        Assert.IsEmpty(atm.GetTransactions());
    }

    [TestMethod]
    [Description("A retry returns the original receipt and leaves exactly one movement in history.")]
    public void RepeatingTheSameOperationReturnsItsOriginalTransaction()
    {
        var command = new AtmCommand(Guid.NewGuid(), OperationKind.Transfer, 25m, "checking", "savings");
        var first = atm.Execute(command);

        var repeated = atm.Execute(command);

        Assert.IsNotNull(first.Transaction);
        Assert.AreEqual(first.Transaction, repeated.Transaction);
        Assert.AreEqual(475m, Balance("checking"));
        Assert.AreEqual(275m, Balance("savings"));
        Assert.HasCount(1, atm.GetTransactions());
    }

    [TestMethod]
    [Description("Changing the amount under an existing operation ID leaves the original deposit in place.")]
    public void ReusingTheIdWithDifferentDetailsIsRejected()
    {
        var command = new AtmCommand(Guid.NewGuid(), OperationKind.Deposit, 10m, DestinationAccountId: "checking");
        atm.Execute(command);

        var result = atm.Execute(command with { Amount = 20m });

        Assert.AreEqual(AtmError.OperationIdConflict, result.Error);
        Assert.AreEqual(510m, Balance("checking"));
        Assert.HasCount(1, atm.GetTransactions());
    }

    [TestMethod]
    [Description("Two $400 withdrawals compete for $500. Only one commits and $100 remains.")]
    public async Task ConcurrentWithdrawalsCannotSpendTheSameFunds()
    {
        var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Task<AtmResult> Withdraw() => Task.Run(async () =>
        {
            await start.Task;
            return atm.Execute(new(Guid.NewGuid(), OperationKind.Withdrawal, 400m, SourceAccountId: "checking"));
        });

        var first = Withdraw();
        var second = Withdraw();
        start.SetResult();
        var results = await Task.WhenAll(first, second);

        Assert.HasCount(1, results.Where(result => result.Transaction is not null));
        Assert.HasCount(1, results.Where(result => result.Error == AtmError.InsufficientFunds));
        Assert.AreEqual(100m, Balance("checking"));
        Assert.HasCount(1, atm.GetTransactions());
    }

    [TestMethod]
    [Description("An exception while creating the receipt leaves the previous snapshot in place.")]
    public void ExceptionBeforeCommitDoesNotPublishPartialState()
    {
        var before = repository.Read();
        var failingAtm = new AtmService(repository, new FailingClock());
        var command = new AtmCommand(Guid.NewGuid(), OperationKind.Transfer, 80m, "checking", "savings");

        Assert.ThrowsExactly<InvalidOperationException>(() => failingAtm.Execute(command));

        Assert.AreSame(before, repository.Read());
        Assert.IsEmpty(atm.GetTransactions());
    }

    private decimal Balance(string id) => atm.GetAccounts().Single(account => account.Id == id).Balance;

    private sealed class FixedClock : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => CompletedAt;
    }

    private sealed class FailingClock : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => throw new InvalidOperationException("Simulated write failure.");
    }
}
