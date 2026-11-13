# TaskFlow — Employee Task & Leave Management

React 19 + TypeScript SPA · ASP.NET Core .NET 10 REST API · PostgreSQL · Redis · RabbitMQ · background worker.

Architecture docs: [`docs/architecture`](docs/architecture/01-architecture-overview.md).

## Run locally

Prerequisites: .NET 10 SDK and Node 22+. PostgreSQL, Redis and RabbitMQ are optional: without a Postgres connection string the app uses SQLite, without Redis it reads straight from the database, and without RabbitMQ notifications are processed in-process.

```bash
dotnet tool restore
# API (http://localhost:5080, Swagger at /swagger). In Development it applies migrations and seeds demo data.
dotnet run --project src/TaskFlow.Api
# Worker (outbox publisher + notification consumer)
dotnet run --project src/TaskFlow.Worker
# Frontend (http://localhost:5173, proxies /api to :5080)
cd frontend && npm install && npm run dev
```

The local connection strings are in `src/TaskFlow.Api/appsettings.Development.json` and `src/TaskFlow.Worker/appsettings.Development.json` (user/password `taskflow` / `taskflow_dev_password`). Override them with environment variables such as `ConnectionStrings__Postgres`.

Demo accounts (Development seed): `admin@taskflow.com` / `Admin@123`, `john.doe@taskflow.com` / `Employee@123`.

## Migrations

```bash
dotnet tool run dotnet-ef migrations add <Name> -p src/TaskFlow.Infrastructure -s src/TaskFlow.Api -o Persistence/Migrations
dotnet tool run dotnet-ef database update  -p src/TaskFlow.Infrastructure -s src/TaskFlow.Api   # uses TASKFLOW_DESIGN_CONNECTION or the localhost default
dotnet tool run dotnet-ef migrations script --idempotent -p src/TaskFlow.Infrastructure -s src/TaskFlow.Api -o migrate.sql
```

## Health
`GET /health/live`, `GET /health/ready` (postgres, redis, outbox backlog).
