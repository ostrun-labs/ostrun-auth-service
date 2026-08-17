# ostrun-auth-service

Service Auth de référence pour [Ostrun](https://github.com/Thykimik/ostrun) — provider `Ostrun`, service `auth`.

## Statut

Repo créé, code pas encore écrit. Ce service est développé séparément du repo cœur d'Ostrun : Ostrun ne fait que le référencer (via son manifeste, `repository` + éventuellement `registry`) et le catalogue central pointe ici plutôt que d'héberger son code.

## Rôle

- Gestion des comptes, sessions, JWT (register/login)
- Publie les events `Ostrun.Auth.UserRegistered`, `Ostrun.Auth.UserLoggedIn`
- Fait partie de la sélection par défaut du catalogue Ostrun, mais reste un service indépendant et remplaçable (d'autres providers Auth pourront apparaître au catalogue)

## Indépendance

- Sa propre base de données / son propre schéma
- Son propre `Dockerfile`, déployable seul
- Communication uniquement via API ou events — jamais d'appel direct à un autre service
