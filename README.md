# ATM Demo

A web ATM built with ASP.NET Core Razor Pages and Vue. The terminal loads balances for two fictional accounts from the versioned C# API.

## Run locally

Install the .NET 10 SDK and Node.js 24. Run these commands from the repository root.

```sh
npm ci --prefix src/AtmDemo.Web/ClientApp
npm run build --prefix src/AtmDemo.Web/ClientApp
dotnet run --project src/AtmDemo.Web
```

Open [http://localhost:5155](http://localhost:5155) to see the terminal. The account endpoint is at [/api/v1/accounts](http://localhost:5155/api/v1/accounts) and the development OpenAPI document is at [/openapi/v1.json](http://localhost:5155/openapi/v1.json).

## Project structure

- **Pages** holds the Razor page that loads the terminal.
- **ClientApp** holds the Vue 3 and TypeScript source. The build checks types with Vue TSC and bundles static assets with Vite.
- **Api/V1** defines the HTTP contract and supplies the initial account balances.

Run the frontend build again after editing the Vue, TypeScript, or CSS sources.

## CI and Azure deployment

GitHub Actions builds the frontend and backend on pushes and pull requests targeting `main`. The workflow runs the .NET tests defined in the solution.

The optional manual deployment uses a short-lived Azure access token. Set up an Azure App Service for Linux with the .NET 10 runtime and give a user-assigned managed identity the **Website Contributor** role scoped to that app.

Add a federated credential to the identity with these exact values for this repository.

| Setting | Value |
| --- | --- |
| Issuer | `https://token.actions.githubusercontent.com` |
| Audience | `api://AzureADTokenExchange` |
| Subject | `repo:ranvli@12191461/ATM_Demo@1465960254:ref:refs/heads/main` |

In **Settings → Secrets and variables → Actions → Variables**, add `AZURE_CLIENT_ID` for the identity, `AZURE_TENANT_ID`, `AZURE_SUBSCRIPTION_ID`, and `AZURE_WEBAPP_NAME`.

In **Actions → ATM CI and Azure deploy**, select **Run workflow** on `main` and check **Deploy to Azure App Service**.
