#!/usr/bin/env bash
set -euo pipefail

REPO_URL="${REPO_URL:-https://github.com/TomachiGachiAnti/ModSynchronizer.git}"
REPO_REF="${REPO_REF:-main}"
PROFILE_NAME="${PROFILE_NAME:-industrial-1.21.1}"
SERVER_ROOT="${SERVER_ROOT:-$PWD}"
WORK_DIR="${WORK_DIR:-$SERVER_ROOT/.modsetup-sync}"
REPO_DIR="${REPO_DIR:-$WORK_DIR/repo}"
PROFILE_PATH="$REPO_DIR/profiles/$PROFILE_NAME.json"
EXCLUDE_FILE_DEFAULT="$REPO_DIR/profiles/$PROFILE_NAME.server-excludes.txt"
EXCLUDE_FILE="${SERVER_EXCLUDE_FILE:-$EXCLUDE_FILE_DEFAULT}"
MODS_DIR="$SERVER_ROOT/mods"
CONFIG_DIR="$SERVER_ROOT/config"
STATE_DIR="$SERVER_ROOT/.modsetup"
MANAGED_MODS_FILE="$STATE_DIR/managed-mods.txt"
MANAGED_CONFIGS_FILE="$STATE_DIR/managed-configs.txt"

require_command() {
    if ! command -v "$1" >/dev/null 2>&1; then
        echo "必要なコマンドが見つかりません: $1" >&2
        exit 1
    fi
}

json_value() {
    local line="$1"
    sed -n 's/^[[:space:]]*"[^"]*":[[:space:]]*"\(.*\)"[[:space:]]*,\{0,1\}[[:space:]]*$/\1/p' <<<"$line"
}

json_bool() {
    local line="$1"
    sed -n 's/^[[:space:]]*"[^"]*":[[:space:]]*\(true\|false\)[[:space:]]*,\{0,1\}[[:space:]]*$/\1/p' <<<"$line"
}

load_excludes() {
    local file_path="$1"
    if [[ ! -f "$file_path" ]]; then
        return
    fi

    while IFS= read -r line; do
        line="${line%%#*}"
        line="$(printf '%s' "$line" | sed 's/^[[:space:]]*//;s/[[:space:]]*$//')"
        if [[ -n "$line" ]]; then
            EXCLUDED_FILES["$line"]=1
        fi
    done <"$file_path"
}

is_excluded() {
    local file_name="$1"
    [[ -n "${EXCLUDED_FILES[$file_name]:-}" ]]
}

sha256_matches() {
    local file_path="$1"
    local expected="$2"
    if [[ -z "$expected" || ! -f "$file_path" ]]; then
        return 1
    fi

    local actual
    actual="$(sha256sum "$file_path" | awk '{print $1}')"
    [[ "$actual" == "$expected" ]]
}

download_mod() {
    local url="$1"
    local destination="$2"
    local expected_sha256="$3"
    local temp_file

    temp_file="$(mktemp)"
    curl -L --fail --show-error --silent "$url" -o "$temp_file"

    if [[ -n "$expected_sha256" ]]; then
        local actual_sha256
        actual_sha256="$(sha256sum "$temp_file" | awk '{print $1}')"
        if [[ "$actual_sha256" != "$expected_sha256" ]]; then
            rm -f "$temp_file"
            echo "SHA-256 不一致: $destination" >&2
            exit 1
        fi
    fi

    mv "$temp_file" "$destination"
}

read_mod_entries() {
    awk '
        BEGIN {
            in_mods = 0
            brace_depth = 0
            filename = ""
            url = ""
            sha256 = ""
            required = "false"
            deprecated = "false"
        }
        /"mods"[[:space:]]*:[[:space:]]*\[/ {
            in_mods = 1
            next
        }
        in_mods == 1 {
            if ($0 ~ /^[[:space:]]*$/) {
                next
            }
            if ($0 ~ /^[[:space:]]*\][[:space:]]*,?[[:space:]]*$/) {
                exit
            }
            if ($0 ~ /^[[:space:]]*{[[:space:]]*$/) {
                brace_depth = 1
                filename = ""
                url = ""
                sha256 = ""
                required = "false"
                deprecated = "false"
                next
            }
            if (brace_depth > 0) {
                if ($0 ~ /"filename"[[:space:]]*:/) {
                    line = $0
                    sub(/^[[:space:]]*"filename"[[:space:]]*:[[:space:]]*"/, "", line)
                    sub(/"[[:space:]]*,?[[:space:]]*$/, "", line)
                    filename = line
                }
                else if ($0 ~ /"url"[[:space:]]*:/) {
                    line = $0
                    sub(/^[[:space:]]*"url"[[:space:]]*:[[:space:]]*"/, "", line)
                    sub(/"[[:space:]]*,?[[:space:]]*$/, "", line)
                    url = line
                }
                else if ($0 ~ /"sha256"[[:space:]]*:/) {
                    line = $0
                    sub(/^[[:space:]]*"sha256"[[:space:]]*:[[:space:]]*"/, "", line)
                    sub(/"[[:space:]]*,?[[:space:]]*$/, "", line)
                    sha256 = line
                }
                else if ($0 ~ /"required"[[:space:]]*:/) {
                    line = $0
                    sub(/^[[:space:]]*"required"[[:space:]]*:[[:space:]]*/, "", line)
                    sub(/[[:space:]]*,?[[:space:]]*$/, "", line)
                    required = line
                }
                else if ($0 ~ /"deprecated"[[:space:]]*:/) {
                    line = $0
                    sub(/^[[:space:]]*"deprecated"[[:space:]]*:[[:space:]]*/, "", line)
                    sub(/[[:space:]]*,?[[:space:]]*$/, "", line)
                    deprecated = line
                }
                else if ($0 ~ /^[[:space:]]*}[[:space:]]*,?[[:space:]]*$/) {
                    if (required == "true" && deprecated != "true" && filename != "") {
                        printf "%s\t%s\t%s\n", filename, url, sha256
                    }
                    brace_depth = 0
                }
            }
        }
    ' "$PROFILE_PATH"
}

read_server_setup_value() {
    local key="$1"
    awk '
        BEGIN {
            in_server_setup = 0
            brace_depth = 0
        }
        /"server_setup"[[:space:]]*:[[:space:]]*{/ {
            in_server_setup = 1
            brace_depth = 1
            next
        }
        in_server_setup == 1 {
            if ($0 ~ /^[[:space:]]*}[[:space:]]*,?[[:space:]]*$/) {
                exit
            }
            pattern = "\"KEY\"[[:space:]]*:"
            gsub("KEY", key, pattern)
            if ($0 ~ pattern) {
                line = $0
                sub(/^[[:space:]]*"[^"]*"[[:space:]]*:[[:space:]]*"/, "", line)
                sub(/"[[:space:]]*,?[[:space:]]*$/, "", line)
                print line
                exit
            }
        }
    ' key="$key" "$PROFILE_PATH"
}

sync_mods() {
    mapfile -t previous_managed_mods < <(sort -u "$MANAGED_MODS_FILE" 2>/dev/null || true)
    mapfile -t current_mod_entries < <(read_mod_entries)

    : >"$MANAGED_MODS_FILE"

    for entry in "${current_mod_entries[@]}"; do
        IFS=$'\t' read -r file_name url sha256 <<<"$entry"

        if is_excluded "$file_name"; then
            echo "除外: $file_name"
            continue
        fi

        if [[ -z "$url" ]]; then
            echo "URL 未設定のためスキップ: $file_name" >&2
            continue
        fi

        local destination="$MODS_DIR/$file_name"
        if sha256_matches "$destination" "$sha256"; then
            echo "維持: $file_name"
        else
            echo "同期: $file_name"
            download_mod "$url" "$destination" "$sha256"
        fi

        printf '%s\n' "$file_name" >>"$MANAGED_MODS_FILE"
    done

    mapfile -t current_managed_mods < <(sort -u "$MANAGED_MODS_FILE" 2>/dev/null || true)
    for old_file in "${previous_managed_mods[@]}"; do
        if [[ -z "$old_file" ]]; then
            continue
        fi

        if ! printf '%s\n' "${current_managed_mods[@]}" | grep -Fxq "$old_file"; then
            if [[ -f "$MODS_DIR/$old_file" ]]; then
                echo "削除: $old_file"
                rm -f "$MODS_DIR/$old_file"
            fi
        fi
    done
}

sync_configs() {
    mapfile -t previous_managed_configs < <(sort -u "$MANAGED_CONFIGS_FILE" 2>/dev/null || true)
    : >"$MANAGED_CONFIGS_FILE"

    local bundle_path
    bundle_path="$(read_server_setup_value "config_bundle_path")"

    if [[ -z "$bundle_path" ]]; then
        return
    fi

    local source_dir="$REPO_DIR/$bundle_path"
    if [[ ! -d "$source_dir" ]]; then
        return
    fi

    while IFS= read -r source_file; do
        local relative_path destination_file destination_dir
        relative_path="${source_file#"$source_dir"/}"
        destination_file="$CONFIG_DIR/$relative_path"
        destination_dir="$(dirname "$destination_file")"
        mkdir -p "$destination_dir"

        if [[ ! -f "$destination_file" ]] || ! cmp -s "$source_file" "$destination_file"; then
            echo "config同期: $relative_path"
            cp "$source_file" "$destination_file"
        fi

        printf '%s\n' "$relative_path" >>"$MANAGED_CONFIGS_FILE"
    done < <(find "$source_dir" -type f | sort)

    mapfile -t current_managed_configs < <(sort -u "$MANAGED_CONFIGS_FILE" 2>/dev/null || true)
    for old_file in "${previous_managed_configs[@]}"; do
        if [[ -z "$old_file" ]]; then
            continue
        fi

        if ! printf '%s\n' "${current_managed_configs[@]}" | grep -Fxq "$old_file"; then
            if [[ -f "$CONFIG_DIR/$old_file" ]]; then
                echo "config削除: $old_file"
                rm -f "$CONFIG_DIR/$old_file"
            fi
        fi
    done
}

prepare_repo() {
    rm -rf "$REPO_DIR"
    mkdir -p "$WORK_DIR"
    git clone --depth 1 --branch "$REPO_REF" "$REPO_URL" "$REPO_DIR" >/dev/null
}

main() {
    require_command git
    require_command curl
    require_command sha256sum
    require_command find
    require_command cmp
    require_command awk
    require_command sed
    require_command grep

    mkdir -p "$MODS_DIR" "$CONFIG_DIR" "$STATE_DIR"
    declare -gA EXCLUDED_FILES=()

    if [[ "${SKIP_REPO_PREPARE:-0}" != "1" ]]; then
        prepare_repo
    fi

    if [[ ! -f "$PROFILE_PATH" ]]; then
        echo "profile が見つかりません: $PROFILE_PATH" >&2
        exit 1
    fi

    load_excludes "$EXCLUDE_FILE"
    sync_mods
    sync_configs

    echo "同期完了: $PROFILE_NAME"
}

main "$@"
