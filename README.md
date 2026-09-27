# ostrun-auth-service

Reference Auth service for [Ostrun](https://github.com/ostrun-labs/ostrun), provider `Ostrun`, service `auth`.

## Status

Manifest, event contracts, and a .NET 8 service (register/login JWT) are written. This service is developed separately from Ostrun's core repo. Ostrun only references it (through its manifest, `repository` and optionally `registry`), and the central catalog points here instead of hosting its code.

## Role

- Account, session, and JWT management (register/login)
- Publishes the `Ostrun.Auth.UserRegistered` and `Ostrun.Auth.UserLoggedIn` events
- Part of the Ostrun catalog's default selection, but remains an independent, replaceable service (other Auth providers may appear in the catalog)

## Independence

- Its own database and schema (PostgreSQL)
- Its own `Dockerfile`, deployable standalone
- Communication only via API or events, never a direct call into another service

## Architecture

Light Clean Architecture in 4 layers:

- `src/OstrunAuthService.Domain`: the `User` and `Account` entities (a user signs in through one or more accounts: password or social provider), no dependencies
- `src/OstrunAuthService.Application`: register/login use cases, interfaces (repository, hasher, JWT, events)
- `src/OstrunAuthService.Infrastructure`: EF Core + Npgsql, hashing, JWT generation, event publishing via MassTransit + RabbitMQ (in-memory transport if `RabbitMq__Host` is absent)
- `src/OstrunAuthService.Api`: .NET 8 Minimal APIs (`/auth/register`, `/auth/login`, `/auth/session`, `/auth/token`, `/auth/sign-out`, `/auth/.well-known/jwks.json`, `/health`)
- `tests/OstrunAuthService.UnitTests`: unit tests for the Application layer
- `tests/OstrunAuthService.IntegrationTests`: HTTP tests against the real API and a throwaway Postgres container (Testcontainers)

## Running locally

```bash
cp .env.example .env   # then set JWT_SIGNING_KEY (see the command in .env.example)
docker compose up --build
```

The API listens on `http://localhost:8080` (`/health`, `/auth/register`, `/auth/login`, `/auth/session`, `/auth/token`, `/auth/sign-out`, `/auth/.well-known/jwks.json`). EF Core migrations apply automatically on startup.

## Development

```bash
dotnet build
dotnet test   # the integration tests need Docker running
dotnet tool run dotnet-ef migrations add <Name> --project src/OstrunAuthService.Infrastructure --startup-project src/OstrunAuthService.Api --output-dir Persistence/Migrations
```
