using AtmDemo.Core;
using Microsoft.AspNetCore.Http.HttpResults;

namespace AtmDemo.Web.Api.V1;

public static class AtmEndpoints
{
    public static void MapAtmEndpoints(this RouteGroupBuilder api)
    {
        api.MapGet("/accounts", (AtmService atm) => TypedResults.Ok(atm.GetAccounts()))
            .WithName("GetAccounts")
            .WithSummary("List ATM account balances");

        api.MapGet("/transactions", (AtmService atm) => TypedResults.Ok(atm.GetTransactions()))
            .WithName("GetTransactions")
            .WithSummary("List successful ATM transactions");

        api.MapGet("/overview", (AtmService atm) => TypedResults.Ok(atm.GetOverview()))
            .WithName("GetOverview")
            .WithSummary("Read balances and transactions from one snapshot");

        api.MapPost("/accounts/{accountId}/deposits", (string accountId, AmountRequest request, AtmService atm) =>
            ToHttpResult(atm.Execute(new(request.OperationId, OperationKind.Deposit, request.Amount, DestinationAccountId: accountId))))
            .WithName("Deposit")
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        api.MapPost("/accounts/{accountId}/withdrawals", (string accountId, AmountRequest request, AtmService atm) =>
            ToHttpResult(atm.Execute(new(request.OperationId, OperationKind.Withdrawal, request.Amount, SourceAccountId: accountId))))
            .WithName("Withdraw")
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        api.MapPost("/transfers", (TransferRequest request, AtmService atm) =>
            ToHttpResult(atm.Execute(new(request.OperationId, OperationKind.Transfer, request.Amount,
                request.SourceAccountId, request.DestinationAccountId))))
            .WithName("Transfer")
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);
    }

    private static Results<Ok<Transaction>, ProblemHttpResult> ToHttpResult(AtmResult result)
    {
        if (result.Transaction is { } transaction)
        {
            return TypedResults.Ok(transaction);
        }

        var (status, code, title) = result.Error switch
        {
            AtmError.InvalidOperationId => (400, "invalid_operation_id", "An operation ID is required."),
            AtmError.InvalidAmount => (400, "invalid_amount", "Enter a positive amount with up to two decimals in the supported range."),
            AtmError.InvalidAccounts => (400, "invalid_accounts", "Select valid accounts. A transfer requires two different accounts."),
            AtmError.AccountNotFound => (404, "account_not_found", "The selected account could not be found."),
            AtmError.CurrencyMismatch => (400, "currency_mismatch", "Both accounts must use the same currency."),
            AtmError.InsufficientFunds => (409, "insufficient_funds", "The account has insufficient funds."),
            AtmError.BalanceLimitExceeded => (409, "balance_limit_exceeded", "The destination balance would exceed the supported range."),
            AtmError.OperationIdConflict => (409, "operation_id_conflict", "That operation ID belongs to a different movement."),
            _ => throw new InvalidOperationException("The ATM returned an unknown result.")
        };

        return TypedResults.Problem(statusCode: status, title: title,
            extensions: new Dictionary<string, object?> { ["code"] = code });
    }

    public sealed record AmountRequest(Guid OperationId, decimal Amount);

    public sealed record TransferRequest(
        Guid OperationId,
        decimal Amount,
        string? SourceAccountId,
        string? DestinationAccountId);
}
