#!/usr/bin/env bash
set -euo pipefail

for variable in ConnectionStrings__ClyvoCareOracle Mongo__ConnectionString Mongo__DatabaseName JWT_PUBLIC_KEY_BASE64; do
    if [[ -z "${!variable:-}" ]]; then
        echo "Missing required app setting: $variable" >&2
        exit 1
    fi
done

application_directory=$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/.." && pwd)
if [[ ! -f "$application_directory/ClyvoCare.API.dll" ]]; then
    echo "Published ClyvoCare.API.dll not found beside the scripts directory." >&2
    exit 1
fi
for executable in dotnet openssl base64; do
    command -v "$executable" >/dev/null || { echo "Required executable not found: $executable" >&2; exit 1; }
done

umask 077
key_directory=$(mktemp -d "${TMPDIR:-/tmp}/clyvocare-jwt.XXXXXX")
trap 'rm -f -- "$key_directory/public_key.pem"; rmdir -- "$key_directory"' EXIT
if ! printf '%s' "$JWT_PUBLIC_KEY_BASE64" | base64 --decode > "$key_directory/public_key.pem" \
    || grep -q 'PRIVATE KEY' "$key_directory/public_key.pem" \
    || ! openssl rsa -pubin -in "$key_directory/public_key.pem" -noout 2>/dev/null; then
    echo "JWT_PUBLIC_KEY_BASE64 must contain a Base64-encoded RSA public PEM." >&2
    exit 1
fi

export Jwt__PublicKeyPath="$key_directory/public_key.pem"
unset JWT_PUBLIC_KEY_BASE64
export ASPNETCORE_ENVIRONMENT=Production
export DOTNET_ENVIRONMENT=Production
export ASPNETCORE_URLS="http://0.0.0.0:${SERVER_PORT:-${PORT:-8080}}"
export ASPNETCORE_HTTPS_PORT=443
export ASPNETCORE_FORWARDEDHEADERS_ENABLED=false
export AppService__Enabled=true
export Logging__FilePath="${Logging__FilePath:-/home/LogFiles/clyvocare-.log}"
mkdir -p -- "$(dirname -- "$Logging__FilePath")"
cd -- "$application_directory"
exec dotnet "$application_directory/ClyvoCare.API.dll"
