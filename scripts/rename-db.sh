#!/usr/bin/env bash
# Renames the database [moda] to [wayd] in place to preserve your local data after the
# Moda -> Wayd rename. The bash counterpart of rename-localdb.ps1; both run the same
# rename-localdb.sql, which is server-agnostic despite the file name.
#
# Usage, from the repository root:
#   ./scripts/rename-db.sh
#
# Runs against the compose `mssql` service by default. Override with the same variables
# used by docker-compose.yml / .env:
#   MSSQL_SERVER (default localhost,$MSSQL_PORT)  MSSQL_PORT (default 1433)
#   MSSQL_USER   (default sa)                     MSSQL_SA_PASSWORD
#
# Uses sqlcmd from PATH when present, otherwise runs it inside the compose container -
# so it works without SQL Server tools installed on the host.
set -euo pipefail

script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
sql_file="$script_dir/rename-localdb.sql"
[[ -f "$sql_file" ]] || { echo "error: SQL script not found: $sql_file" >&2; exit 1; }

port="${MSSQL_PORT:-1433}"
server="${MSSQL_SERVER:-localhost,$port}"
user="${MSSQL_USER:-sa}"
password="${MSSQL_SA_PASSWORD:-Wayd_Dev_P@ssw0rd!}"

echo 'Renaming database [moda] -> [wayd]...'

if command -v sqlcmd >/dev/null 2>&1; then
    sqlcmd -S "$server" -U "$user" -P "$password" -C -b -i "$sql_file"
elif docker compose ps --status running --services 2>/dev/null | grep -qx mssql; then
    docker compose exec -T mssql \
        /opt/mssql-tools18/bin/sqlcmd -S localhost -U "$user" -P "$password" -C -b < "$sql_file"
else
    echo "error: need sqlcmd on PATH, or the compose 'mssql' service running (docker compose up mssql)" >&2
    exit 1
fi

echo
echo 'Done. If new migrations need to be applied, run:'
echo '  dotnet ef database update --project Wayd.Infrastructure/src/Wayd.Infrastructure.Migrators.MSSQL --startup-project Wayd.Web/src/Wayd.Web.Api'
