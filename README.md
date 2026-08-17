# ostrun-auth-service

Service Auth de référence pour [Ostrun](https://github.com/Thykimik/ostrun) — provider `Ostrun`, service `auth`.

## Statut

Manifeste, contrats d'events et service .NET 8 (register/login JWT) écrits. Ce service est développé séparément du repo cœur d'Ostrun : Ostrun ne fait que le référencer (via son manifeste, `repository` + éventuellement `registry`) et le catalogue central pointe ici plutôt que d'héberger son code.

## Rôle

- Gestion des comptes, sessions, JWT (register/login)
- Publie les events `Ostrun.Auth.UserRegistered`, `Ostrun.Auth.UserLoggedIn`
- Fait partie de la sélection par défaut du catalogue Ostrun, mais reste un service indépendant et remplaçable (d'autres providers Auth pourront apparaître au catalogue)

## Indépendance

- Sa propre base de données / son propre schéma (PostgreSQL)
- Son propre `Dockerfile`, déployable seul
- Communication uniquement via API ou events — jamais d'appel direct à un autre service

## Architecture

Clean Architecture légère en 4 couches :

- `src/OstrunAuthService.Domain` — entité `User`, aucune dépendance
- `src/OstrunAuthService.Application` — cas d'usage register/login, interfaces (repository, hasher, JWT, events)
- `src/OstrunAuthService.Infrastructure` — EF Core + Npgsql, hashing, génération JWT, publication d'events (stub loggé, aucun broker choisi pour l'instant)
- `src/OstrunAuthService.Api` — Minimal APIs .NET 8 (`/auth/register`, `/auth/login`, `/health`)
- `tests/OstrunAuthService.UnitTests` — tests unitaires de la couche Application

## Lancer en local

```bash
cp .env.example .env   # ajuster JWT_SECRET notamment
docker compose up --build
```

L'API écoute sur `http://localhost:8080` (`/health`, `/auth/register`, `/auth/login`). Les migrations EF Core sont appliquées automatiquement au démarrage.

## Développement

```bash
dotnet build
dotnet test
dotnet tool run dotnet-ef migrations add <Name> --project src/OstrunAuthService.Infrastructure --startup-project src/OstrunAuthService.Api --output-dir Persistence/Migrations
```
