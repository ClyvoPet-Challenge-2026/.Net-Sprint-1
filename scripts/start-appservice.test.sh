#!/usr/bin/env bash
set -euo pipefail

script_directory=$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)
test_directory=$(mktemp -d)
export TMPDIR="$test_directory/runtime"
export PUBLISH_DIRECTORY="$test_directory/publish with spaces"
mkdir -p -- "$TMPDIR" "$test_directory/bin" "$PUBLISH_DIRECTORY/scripts"
cleanup() {
    local directory
    for directory in "$TMPDIR"/clyvocare-jwt.* "$PUBLISH_DIRECTORY/scripts" "$PUBLISH_DIRECTORY" "$test_directory/bin" "$test_directory/logs"; do
        if [[ -d "$directory" ]]; then
            rm -f -- "$directory"/*
            rmdir -- "$directory"
        fi
    done
    rm -f -- "$test_directory/private.pem" "$test_directory/public.pem" "$test_directory/output"
    rmdir -- "$TMPDIR" "$test_directory"
}
trap cleanup EXIT
trap 'echo "App Service startup test failed at line $LINENO." >&2; cat "$test_directory/output" >&2' ERR

cp -- "$script_directory/start-appservice.sh" "$PUBLISH_DIRECTORY/scripts/"
touch -- "$PUBLISH_DIRECTORY/ClyvoCare.API.dll"
openssl genpkey -algorithm RSA -pkeyopt rsa_keygen_bits:2048 -out "$test_directory/private.pem" 2>/dev/null
openssl pkey -in "$test_directory/private.pem" -pubout -out "$test_directory/public.pem"
export EXPECTED_PUBLIC_KEY="$test_directory/public.pem"
export JWT_PUBLIC_KEY_BASE64
JWT_PUBLIC_KEY_BASE64=$(base64 < "$EXPECTED_PUBLIC_KEY" | tr -d '\r\n')
export ConnectionStrings__ClyvoCareOracle='Data Source=example.invalid:1521/ORCL;User Id=test;Password=test;'
export Mongo__ConnectionString='mongodb://example.invalid:27017'
export Mongo__DatabaseName=test
export Logging__FilePath="$test_directory/logs/clyvocare-.log"
export SERVER_PORT=8181
export PORT=9191
export EXPECTED_PORT=8181

cat > "$test_directory/bin/dotnet" <<'DOTNET'
#!/usr/bin/env bash
set -euo pipefail
trap 'echo "Dotnet launch assertion failed at line $LINENO." >&2' ERR
[[ "$#" == 1 && "$1" == "$PUBLISH_DIRECTORY/ClyvoCare.API.dll" ]]
[[ "$PWD" == "$PUBLISH_DIRECTORY" ]]
[[ "$ASPNETCORE_ENVIRONMENT" == Production && "$DOTNET_ENVIRONMENT" == Production ]]
[[ "$ASPNETCORE_URLS" == "http://0.0.0.0:$EXPECTED_PORT" && "$ASPNETCORE_HTTPS_PORT" == 443 ]]
[[ "$AppService__Enabled" == true && "$ASPNETCORE_FORWARDEDHEADERS_ENABLED" == false ]]
[[ -z "${JWT_PUBLIC_KEY_BASE64+x}" ]]
[[ -d "$(dirname -- "$Logging__FilePath")" ]]
cmp -- "$Jwt__PublicKeyPath" "$EXPECTED_PUBLIC_KEY"
if [[ "$(uname -s)" == Linux ]]; then
    [[ "$(stat -c %a "$Jwt__PublicKeyPath")" == 600 ]]
    [[ "$(stat -c %a "$(dirname -- "$Jwt__PublicKeyPath")")" == 700 ]]
fi
echo 'Dotnet launch assertions passed.'
exit "${DOTNET_TEST_EXIT_CODE:-0}"
DOTNET
chmod +x "$test_directory/bin/dotnet"
export PATH="$test_directory/bin:$PATH"

expect_failure() {
    if "$@" > "$test_directory/output" 2>&1; then
        echo 'Invalid startup unexpectedly succeeded.' >&2
        exit 1
    fi
    if compgen -G "$TMPDIR/clyvocare-jwt.*" > /dev/null; then
        echo 'Failed startup left a temporary key behind.' >&2
        cat "$test_directory/output" >&2
        exit 1
    fi
}

startup_script="$PUBLISH_DIRECTORY/scripts/start-appservice.sh"
for variable in ConnectionStrings__ClyvoCareOracle Mongo__ConnectionString Mongo__DatabaseName JWT_PUBLIC_KEY_BASE64; do
    expect_failure env -u "$variable" bash "$startup_script"
done
expect_failure env JWT_PUBLIC_KEY_BASE64='invalid!' bash "$startup_script"
expect_failure env JWT_PUBLIC_KEY_BASE64="$(printf 'not a PEM' | base64)" bash "$startup_script"
expect_failure env JWT_PUBLIC_KEY_BASE64="$(base64 < "$test_directory/private.pem" | tr -d '\r\n')" bash "$startup_script"
rm -- "$PUBLISH_DIRECTORY/ClyvoCare.API.dll"
expect_failure bash "$startup_script"
touch -- "$PUBLISH_DIRECTORY/ClyvoCare.API.dll"

for attempt in 1 2; do
    bash "$startup_script" > "$test_directory/output" 2>&1
    [[ "$(cat "$test_directory/output")" == 'Dotnet launch assertions passed.' ]]
done
env -u SERVER_PORT EXPECTED_PORT=9191 bash "$startup_script" > "$test_directory/output" 2>&1
env -u SERVER_PORT -u PORT EXPECTED_PORT=8080 bash "$startup_script" > "$test_directory/output" 2>&1
if env DOTNET_TEST_EXIT_CODE=23 bash "$startup_script" > "$test_directory/output" 2>&1; then
    echo 'Dotnet failure was not propagated.' >&2
    exit 1
else
    [[ "$?" == 23 ]]
fi

echo 'App Service startup: assertions passed.'
