# ATM Demo

A web ATM built with ASP.NET Core Razor Pages, Vue and TypeScript. The terminal supports deposits, withdrawals, transfers and transaction history for two fictional USD accounts.

## Run in Visual Studio 2026

1. Install the **ASP.NET and web development** workload with the **.NET 10 SDK** in Visual Studio 2026. Make sure Node.js 24 and npm are on your PATH.
2. Open `AtmDemo.slnx` and press **F5** with the `AtmDemo.Web` launch profile.

The build installs the frontend dependencies and compiles Vue and TypeScript. The ASP.NET Core app serves both the terminal and the API.

## Run from the command line

Install the .NET 10 SDK and Node.js 24. Run this command from the repository root.

```sh
dotnet run --project src/AtmDemo.Web
```

Open [http://localhost:5155](http://localhost:5155) to see the terminal. The account endpoint is at [/api/v1/accounts](http://localhost:5155/api/v1/accounts) and the development OpenAPI document is at [/openapi/v1.json](http://localhost:5155/openapi/v1.json).

## Try the terminal

1. Start with $500.00 in Checking and $250.00 in Savings.
2. Deposit $25.00 into Checking, then withdraw $10.00 from Savings.
3. Transfer $100.00 from Checking to Savings. The balances should be $425.00 and $340.00, respectively.
4. Open **History** to see the three movements. Try withdrawing more than the available balance to see the rejection.

## Read and debug the frontend

- [AtmTerminal.vue](src/AtmDemo.Web/ClientApp/src/AtmTerminal.vue) contains the terminal markup.
- [AtmTerminal.ts](src/AtmDemo.Web/ClientApp/src/AtmTerminal.ts) handles user input, confirmation and retries.
- [atmApi.ts](src/AtmDemo.Web/ClientApp/src/atmApi.ts) contains the typed HTTP client.
- [atm.css](src/AtmDemo.Web/ClientApp/src/atm.css) contains the terminal styles.

Razor loads the JavaScript and stylesheet as separate files. Vite generates those files and a JavaScript source map during the build. With the app running, open **Sources** in your browser developer tools and open `AtmTerminal.ts` to set a breakpoint in the original TypeScript.

## Architecture

- **AtmDemo.Web** hosts the Razor page, Vue components and `/api/v1` in one ASP.NET Core process. The Vue component calls a typed HTTP client. The endpoints translate requests and return standard problem details for rejections.
- **AtmDemo.Core** contains `AtmService`, which validates accounts, amounts and available funds. The service gets its clock and `IAtmRepository` through dependency injection. The repository contract gives a storage provider one boundary for reading and publishing state.
- **In-memory repository** protects each operation with a lock. It publishes a complete immutable snapshot of balances and history. Each run starts with fresh balances and serves one shared demo state per process.

Amounts use C# `decimal` in USD with up to two decimal places. The supported range fits the browser's numeric precision. Each successful movement includes an operation ID and a UTC timestamp. The same ID and details return the original transaction. A different valid request with that ID returns HTTP 409.

The terminal reads `/api/v1/overview` for a consistent view of balances and history. The API also exposes the following routes.

| Method | Route |
| --- | --- |
| GET | `/api/v1/accounts` |
| GET | `/api/v1/transactions` |
| POST | `/api/v1/accounts/{accountId}/deposits` |
| POST | `/api/v1/accounts/{accountId}/withdrawals` |
| POST | `/api/v1/transfers` |

## Unit tests

Run the MSTest project with the .NET SDK or from **Test Explorer** in Visual Studio.

```sh
dotnet test tests/AtmDemo.Tests/AtmDemo.Tests.csproj
```

The suite covers the three operations, invalid inputs, insufficient funds, concurrent withdrawals, idempotent retries and failure before the snapshot is published.

## CI and Azure deployment

GitHub Actions builds the frontend and backend on pushes and pull requests targeting `main`. The workflow runs the .NET tests defined in the solution.

The build job uses the repository token with read access. The optional manual deployment from `main` requests an OIDC token, which Azure exchanges for a short-lived access token. Set up an Azure App Service for Linux with the .NET 10 runtime and give a user-assigned managed identity the **Website Contributor** role scoped to that app.

Add a federated credential to the identity with these exact values for this repository.

| Setting | Value |
| --- | --- |
| Issuer | `https://token.actions.githubusercontent.com` |
| Audience | `api://AzureADTokenExchange` |
| Subject | `repo:ranvli@12191461/ATM_Demo@1465960254:ref:refs/heads/main` |

In **Settings → Secrets and variables → Actions → Variables**, add `AZURE_CLIENT_ID` for the identity, `AZURE_TENANT_ID`, `AZURE_SUBSCRIPTION_ID`, and `AZURE_WEBAPP_NAME`.

In **Actions → ATM CI and Azure deploy**, select **Run workflow** on `main` and check **Deploy to Azure App Service**.

### Replace the deployment identity

OIDC tokens expire automatically and each deployment requests a fresh token. The four GitHub variables identify the target and identity.

1. Create a replacement identity with the same App Service role and federated credential.
2. Update `AZURE_CLIENT_ID` to the replacement identity's client ID and verify a manual deployment.
3. Remove the previous identity's federated credential and App Service role assignment.
