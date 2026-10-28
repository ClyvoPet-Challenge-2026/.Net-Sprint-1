#!/usr/bin/env bash
set -euo pipefail

repo_dir="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$repo_dir/ClyvoCare"

if ! command -v dotnet >/dev/null 2>&1; then
    echo 'SDK .NET 9 não encontrado no PATH. Consulte docs/validacao.md.' >&2
    exit 2
fi
if ! sdk_version="$(dotnet --version 2>/dev/null)" || [[ "$sdk_version" != 9.* ]]; then
    echo 'É necessário SDK .NET 9 compatível com ClyvoCare/global.json.' >&2
    echo 'Instale o SDK e execute novamente. O target net9.0 será preservado.' >&2
    exit 2
fi
runtime_list="$(dotnet --list-runtimes)"
for framework in Microsoft.NETCore.App Microsoft.AspNetCore.App; do
    if ! awk -v framework="$framework" '$1 == framework && $2 ~ /^9\./ { found=1 } END { exit !found }' <<< "$runtime_list"; then
        echo "Runtime $framework 9 ausente. Instale o SDK completo e execute novamente." >&2
        exit 2
    fi
done

mkdir -p "$repo_dir/artifacts"
results_dir="$(mktemp -d "$repo_dir/artifacts/validation.XXXXXX")"
echo "SDK: $sdk_version"
echo "Resultados: $results_dir"

dotnet restore ClyvoCare.sln
dotnet build ClyvoCare.sln --configuration Release --no-restore

test_status=0
for test_project in tests/*/*.csproj; do
    project_name="$(basename -- "$test_project" .csproj)"
    if ! dotnet test "$test_project" --configuration Release --no-build --no-restore \
        --results-directory "$results_dir/$project_name" --logger trx; then
        test_status=1
    fi
done

if [[ "$test_status" -ne 0 ]]; then
    echo "Há testes com falha. Consulte os relatórios em $results_dir." >&2
    exit 1
fi
echo "Build e suíte concluídos. Relatórios: $results_dir"
echo 'Oracle, MongoDB real, migrations e OTLP exigem validação integrada separada.'
