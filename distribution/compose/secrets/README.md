# Secrets Management for Docker Compose

This directory stores sensitive configuration secrets mounted into containers by Docker Compose (`distribution/compose/compose.yaml`).

## Required Secret Files

The compose stack expects the following files in this directory:

1. `db_password.txt`: Raw database password read by PostgreSQL via `POSTGRES_PASSWORD_FILE=/run/secrets/db_password`.
2. `ConnectionStrings__Database.txt`: Complete ADO.NET connection string mounted into the API container at `/run/secrets/ConnectionStrings__Database`.

> [!WARNING]
> Never commit plaintext secret files to version control. The repository `.gitignore` ignores all files in this directory except `*.example` templates and this `README.md`.

## Generating Secrets

Generate a cryptographically secure 32-byte hexadecimal secret using `openssl rand -hex 32`. Hexadecimal characters `[0-9a-f]` are safe for connection strings and URLs without requiring percent-encoding or delimiter escaping (avoiding semicolons or equals signs).

From the repository root or the `distribution/compose` directory:

```bash
# Ensure the secrets directory exists
mkdir -p distribution/compose/secrets

# Generate a 32-byte hex random password
DB_PASS=$(openssl rand -hex 32)

# Write the postgres password secret (no trailing newline)
printf "%s" "$DB_PASS" > distribution/compose/secrets/db_password.txt

# Write the API connection string secret
printf "Host=postgres;Port=5432;Database=ragpipeline;Username=ragpipeline;Password=%s" "$DB_PASS" > distribution/compose/secrets/ConnectionStrings__Database.txt

# Set permissions: directory 700 keeps other host users out, files 644 readable by container user
chmod 700 distribution/compose/secrets
chmod 644 distribution/compose/secrets/db_password.txt
chmod 644 distribution/compose/secrets/ConnectionStrings__Database.txt
```

## File Permissions

With plain Docker Compose, file secrets are bind-mounted keeping host owner/mode, and the api container runs as non-root uid 1654 (user app), so chmod 600 files owned by the host user would be unreadable and the api would fail to start.

Apply `chmod 700` (`drwx------`) to the directory and `chmod 644` (`-rw-r--r--`) to the secret files:
- Directory `distribution/compose/secrets` `chmod 700`: keeps other host users out.
- The two secret files `chmod 644`: readable by the container user (protection comes from the 700 directory).

## Troubleshooting

If api logs a missing/empty ConnectionStrings:Database after secrets exist, check file readability: verify that directory `distribution/compose/secrets` is `chmod 700` (keeps other host users out) and the two secret files are `chmod 644` (readable by the container user; protection comes from the 700 directory).
