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
