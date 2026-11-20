using Microsoft.AspNetCore.Http.HttpResults;

namespace AtmDemo.Web.Api.V1;

public static class AccountEndpoints
{
    private static readonly AccountSummary[] Accounts =
    [
        new("checking", "Checking", 500.00m, "USD"),
        new("savings", "Savings", 250.00m, "USD"),
    ];

    public static void MapAccountEndpoints(this RouteGroupBuilder api)
    {
        api.MapGet("/accounts", GetAccounts)
            .WithName("GetAccounts")
            .WithSummary("List ATM account balances");
    }

    private static Ok<AccountSummary[]> GetAccounts() => TypedResults.Ok(Accounts);
}

public sealed record AccountSummary(string Id, string Name, decimal Balance, string Currency);
