#!/bin/sh

# Fail fast on any error — without this, `test -n` below would log a non-zero
# exit but the script would keep going and `sed` would silently inject an empty
# value, leaving the app misconfigured at startup.
set -e

echo "Check that we have env vars"
test -n "$NEXT_PUBLIC_API_BASE_URL"


find /app/.next \( -type d -name .git -prune \) -o -type f -print0 | xargs -0 sed -i "s#APP_NEXT_PUBLIC_API_BASE_URL#$NEXT_PUBLIC_API_BASE_URL#g"

echo "Starting Nextjs"
exec "$@"