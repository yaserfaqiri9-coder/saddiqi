# DB Cleaner

Small helper to back up and truncate application data tables in the `public` schema while preserving authentication tables.

Usage (PowerShell):

```powershell
dotnet run --project tools/db-cleaner/DbCleaner.csproj
```

The tool preserves `Users`, `Roles`, and `__EFMigrationsHistory`.

The tool reads connection string from `DATABASE_URL` or `ConnectionStrings__DefaultConnection` environment variables, falling back to `Host=localhost;Port=5432;Username=postgres;Password=postgres;Database=ptg_oil_system;`.

Backups will be written to `artifacts/db-backup-<timestamp>/`.
