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
- `src/OstrunAuthService.Api`: .NET 8 Minimal APIs (`/auth/register`, `/auth/login`, `/auth/session`, `/auth/token`, `/auth/sign-out`, `/auth/providers`, `/auth/.well-known/jwks.json`, `/health`)
- `tests/OstrunAuthService.UnitTests`: unit tests for the Application layer
- `tests/OstrunAuthService.IntegrationTests`: HTTP tests against the real API and a throwaway Postgres container (Testcontainers)

## Running locally

```bash
cp .env.example .env   # then set JWT_SIGNING_KEY (see the command in .env.example)
docker compose up --build
```

The API listens on `http://localhost:8080` (`/health`, `/auth/register`, `/auth/login`, `/auth/session`, `/auth/token`, `/auth/sign-out`, `/auth/providers`, `/auth/.well-known/jwks.json`). EF Core migrations apply automatically on startup.

## Social sign-in

Google sign-in is enabled when `Google__ClientId` and `Google__ClientSecret` are both set. `GET /auth/providers` lists what's enabled.

1. The frontend sends the browser to `GET /auth/sign-in/social/google?callbackURL=<where to land>`. `callbackURL` must be a path on this service's host or an origin listed in `Auth__TrustedOrigins`.
2. After Google, the service finds, links or creates the user, sets the session cookie, and redirects to `callbackURL`.
3. The frontend calls `POST /auth/token` to get a JWT for other services.

Failures come back as `callbackURL?error=` with `access_denied` (the user declined), `email_not_verified` (Google has no verified email for the account) or `sign_in_failed`.

Register `<public URL>/auth/callback/google` as the redirect URI on the Google OAuth client.

Behind a gateway that terminates TLS, set `Auth__TrustedProxies` to the gateway's IPs or CIDR range. The service then takes the public scheme, host and client IP from the gateway's `X-Forwarded-*` headers, so the redirect URI sent to Google is the public `https://` one. Headers from any other caller are ignored.

## Development

```bash
dotnet build
dotnet test   # the integration tests need Docker running
dotnet tool run dotnet-ef migrations add <Name> --project src/OstrunAuthService.Infrastructure --startup-project src/OstrunAuthService.Api --output-dir Persistence/Migrations
```
