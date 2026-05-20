#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "${ROOT_DIR}"

if (( $# == 0 )); then
  set -- "src/rclsharp"
fi

status=0

for target in "$@"; do
  if [[ ! -e "${target}" ]]; then
    echo "Unity meta check target not found: ${target}" >&2
    status=1
    continue
  fi

  while IFS= read -r asset; do
    if [[ ! -e "${asset}.meta" ]]; then
      echo "missing Unity .meta: ${asset}.meta" >&2
      status=1
    fi
  done < <(find "${target}" \( -path '*/bin' -o -path '*/obj' \) -prune -o \( -type d -o \( -type f ! -name '*.meta' \) \) -print | sort)

  while IFS= read -r meta; do
    asset="${meta%.meta}"
    if [[ ! -e "${asset}" ]]; then
      echo "orphan Unity .meta: ${meta}" >&2
      status=1
    fi
  done < <(find "${target}" \( -path '*/bin' -o -path '*/obj' \) -prune -o -type f -name '*.meta' -print | sort)
done

if (( status == 0 )); then
  echo "Unity meta check passed: $*"
fi

exit "${status}"
