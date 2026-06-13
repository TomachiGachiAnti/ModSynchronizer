#!/usr/bin/env bash
set -euo pipefail

REPO_URL="${REPO_URL:-https://github.com/TomachiGachiAnti/ModSynchronizer.git}"
REPO_REF="${REPO_REF:-main}"
PROFILE_NAME="${PROFILE_NAME:-industrial-1.21.1}"
BOOTSTRAP_ROOT="${BOOTSTRAP_ROOT:-/tmp/modsetup-server-bootstrap}"
REPO_DIR="${REPO_DIR:-$BOOTSTRAP_ROOT/repo}"
SERVER_BASE_DIR="${SERVER_BASE_DIR:-/opt/minecraft}"
SERVER_ROOT="${SERVER_ROOT:-$SERVER_BASE_DIR/$PROFILE_NAME}"
MINECRAFT_USER="${MINECRAFT_USER:-minecraft}"
MINECRAFT_GROUP="${MINECRAFT_GROUP:-minecraft}"
TMUX_SESSION_NAME="${TMUX_SESSION_NAME:-$PROFILE_NAME}"
SYSTEMD_SERVICE_NAME="${SYSTEMD_SERVICE_NAME:-minecraft-$PROFILE_NAME.service}"
PROFILE_PATH="$REPO_DIR/profiles/$PROFILE_NAME.json"
SYNC_SCRIPT_PATH="$REPO_DIR/tools/sync-server-profile.sh"
DOWNLOADS_DIR="$BOOTSTRAP_ROOT/downloads"
SERVER_JVM_ARGS_FILE="$SERVER_ROOT/user_jvm_args.txt"
BACKUP_ROOT_BASE="${BACKUP_ROOT_BASE:-/mnt/hdd/backup}"
BACKUP_ROOT="${BACKUP_ROOT:-$BACKUP_ROOT_BASE/$PROFILE_NAME}"
BACKUP_SCRIPT_PATH="${BACKUP_SCRIPT_PATH:-/usr/local/bin/minecraft-backup-$PROFILE_NAME.sh}"
BACKUP_RETENTION_DAYS="${BACKUP_RETENTION_DAYS:-14}"
BACKUP_SERVICE_NAME="${BACKUP_SERVICE_NAME:-minecraft-backup-$PROFILE_NAME.service}"
BACKUP_TIMER_NAME="${BACKUP_TIMER_NAME:-minecraft-backup-$PROFILE_NAME.timer}"
SAVEALL_SCRIPT_PATH="${SAVEALL_SCRIPT_PATH:-/usr/local/bin/minecraft-saveall-$PROFILE_NAME.sh}"
SAVEALL_SERVICE_NAME="${SAVEALL_SERVICE_NAME:-minecraft-saveall-$PROFILE_NAME.service}"
SAVEALL_TIMER_NAME="${SAVEALL_TIMER_NAME:-minecraft-saveall-$PROFILE_NAME.timer}"

log() {
    echo "[setup-server] $*"
}

fail() {
    echo "[setup-server] $*" >&2
    exit 1
}

require_root() {
    if [[ "$(id -u)" -ne 0 ]]; then
        fail "root 権限で実行してください。例: sudo bash setup-server.sh"
    fi
}

require_command() {
    if ! command -v "$1" >/dev/null 2>&1; then
        fail "必要なコマンドが見つかりません: $1"
    fi
}

ensure_backup_mount() {
    if [[ ! -d "/mnt/hdd" ]]; then
        fail "/mnt/hdd が見つかりません。バックアップ先を用意してから再実行してください。"
    fi

    if command -v mountpoint >/dev/null 2>&1; then
        if ! mountpoint -q /mnt/hdd; then
            fail "/mnt/hdd はマウントポイントではありません。誤った場所へのバックアップを防ぐため中断しました。"
        fi
    fi
}

install_packages() {
    export DEBIAN_FRONTEND=noninteractive
    log "必要パッケージを確認します。"
    apt-get update
    apt-get install -y ca-certificates curl git tmux openjdk-21-jre-headless rsync
}

prepare_repo() {
    log "リポジトリを取得します。"
    rm -rf "$REPO_DIR"
    mkdir -p "$BOOTSTRAP_ROOT"
    git clone --depth 1 --branch "$REPO_REF" "$REPO_URL" "$REPO_DIR"
}

read_section_string() {
    local section="$1"
    local key="$2"
    awk '
        BEGIN {
            in_section = 0
        }
        $0 ~ "^[[:space:]]*\"" section "\"[[:space:]]*:[[:space:]]*\\{" {
            in_section = 1
            next
        }
        in_section == 1 {
            if ($0 ~ /^[[:space:]]*}[[:space:]]*,?[[:space:]]*$/) {
                exit
            }
            if ($0 ~ "^[[:space:]]*\"" key "\"[[:space:]]*:") {
                line = $0
                sub(/^[[:space:]]*"[^"]*"[[:space:]]*:[[:space:]]*"/, "", line)
                sub(/"[[:space:]]*,?[[:space:]]*$/, "", line)
                print line
                exit
            }
        }
    ' section="$section" key="$key" "$PROFILE_PATH"
}

read_loader_installer_url() {
    read_section_string "loader" "installer_url"
}

read_loader_version() {
    read_section_string "loader" "version"
}

read_server_jar_url() {
    read_section_string "server_setup" "server_jar_url"
}

read_server_jar_sha1() {
    read_section_string "server_setup" "server_jar_sha1"
}

ensure_profile_exists() {
    if [[ ! -f "$PROFILE_PATH" ]]; then
        fail "profile が見つかりません: $PROFILE_PATH"
    fi
}

ensure_minecraft_user() {
    if id -u "$MINECRAFT_USER" >/dev/null 2>&1; then
        return
    fi

    log "minecraft 実行ユーザーを作成します。"
    useradd \
        --system \
        --home-dir "$SERVER_BASE_DIR" \
        --create-home \
        --shell /usr/sbin/nologin \
        "$MINECRAFT_USER"
}

prepare_directories() {
    mkdir -p "$SERVER_BASE_DIR" "$SERVER_ROOT" "$DOWNLOADS_DIR"
    mkdir -p "$SERVER_ROOT/mods" "$SERVER_ROOT/config"
    mkdir -p "$BACKUP_ROOT"
    chown -R "$MINECRAFT_USER:$MINECRAFT_GROUP" "$SERVER_BASE_DIR"
    chown -R "$MINECRAFT_USER:$MINECRAFT_GROUP" "$BACKUP_ROOT"
}

download_file() {
    local url="$1"
    local destination="$2"
    local temp_file

    temp_file="$(mktemp)"
    curl -L --fail --show-error --silent "$url" -o "$temp_file"
    mv "$temp_file" "$destination"
}

sha1_matches() {
    local file_path="$1"
    local expected_sha1="$2"

    if [[ -z "$expected_sha1" || ! -f "$file_path" ]]; then
        return 1
    fi

    local actual_sha1
    actual_sha1="$(sha1sum "$file_path" | awk '{print $1}')"
    [[ "$actual_sha1" == "$expected_sha1" ]]
}

ensure_server_jar() {
    local server_jar_url
    local server_jar_sha1
    local destination

    server_jar_url="$(read_server_jar_url)"
    server_jar_sha1="$(read_server_jar_sha1)"
    destination="$SERVER_ROOT/server.jar"

    if [[ -z "$server_jar_url" ]]; then
        fail "server_setup.server_jar_url が未設定です。"
    fi

    if sha1_matches "$destination" "$server_jar_sha1"; then
        log "server.jar は最新です。"
        return
    fi

    log "server.jar を取得します。"
    download_file "$server_jar_url" "$destination"

    if [[ -n "$server_jar_sha1" ]] && ! sha1_matches "$destination" "$server_jar_sha1"; then
        fail "server.jar の SHA-1 検証に失敗しました。"
    fi

    chown "$MINECRAFT_USER:$MINECRAFT_GROUP" "$destination"
}

ensure_neoforge_server() {
    local installer_url
    local loader_version
    local installer_path

    installer_url="$(read_loader_installer_url)"
    loader_version="$(read_loader_version)"
    installer_path="$DOWNLOADS_DIR/neoforge-installer.jar"

    if [[ -z "$installer_url" ]]; then
        fail "loader.installer_url が未設定です。"
    fi

    if [[ -z "$loader_version" ]]; then
        fail "loader.version が未設定です。"
    fi

    if [[ -x "$SERVER_ROOT/run.sh" && -d "$SERVER_ROOT/libraries/net/neoforged/neoforge/$loader_version" ]]; then
        log "NeoForge サーバーは導入済みです。"
        return
    fi

    log "NeoForge installer を取得します。"
    download_file "$installer_url" "$installer_path"
    chown "$MINECRAFT_USER:$MINECRAFT_GROUP" "$installer_path"

    log "NeoForge サーバーを導入します。"
    runuser -u "$MINECRAFT_USER" -- bash -lc "cd '$SERVER_ROOT' && java -jar '$installer_path' --installServer"

    if [[ ! -x "$SERVER_ROOT/run.sh" ]]; then
        fail "NeoForge サーバーの導入後に run.sh が見つかりませんでした。"
    fi
}

ensure_jvm_args() {
    log "サーバー JVM 引数を設定します。"
    cat >"$SERVER_JVM_ARGS_FILE" <<EOF
-Xms4G
-Xmx8G
EOF
    chown "$MINECRAFT_USER:$MINECRAFT_GROUP" "$SERVER_JVM_ARGS_FILE"
}

write_backup_script() {
    log "バックアップスクリプトを作成します。"
    cat >"$BACKUP_SCRIPT_PATH" <<EOF
#!/usr/bin/env bash
set -euo pipefail

PROFILE_NAME="$PROFILE_NAME"
SERVER_ROOT="$SERVER_ROOT"
BACKUP_ROOT="$BACKUP_ROOT"
LATEST_LINK="\$BACKUP_ROOT/latest"
RETENTION_DAYS="$BACKUP_RETENTION_DAYS"
TMUX_SESSION_NAME="$TMUX_SESSION_NAME"
TIMESTAMP="\$(date +%Y%m%d%H%M)"
BACKUP_DIR="\$BACKUP_ROOT/backup_\$TIMESTAMP"

log() {
    echo "[minecraft-backup:\$PROFILE_NAME] \$*"
}

require_command() {
    if ! command -v "\$1" >/dev/null 2>&1; then
        echo "必要なコマンドが見つかりません: \$1" >&2
        exit 1
    fi
}

flush_server_save() {
    if ! /usr/bin/tmux has-session -t "\$TMUX_SESSION_NAME" 2>/dev/null; then
        return
    fi

    /usr/bin/tmux send-keys -t "\$TMUX_SESSION_NAME" save-all C-m
    sleep 2
    /usr/bin/tmux send-keys -t "\$TMUX_SESSION_NAME" save-off C-m
    sleep 2
}

resume_server_save() {
    if ! /usr/bin/tmux has-session -t "\$TMUX_SESSION_NAME" 2>/dev/null; then
        return
    fi

    /usr/bin/tmux send-keys -t "\$TMUX_SESSION_NAME" save-on C-m
}

main() {
    require_command rsync
    require_command find

    mkdir -p "\$BACKUP_ROOT"

    trap resume_server_save EXIT
    flush_server_save

    log "バックアップを開始します: \$BACKUP_DIR"
    if [[ -L "\$LATEST_LINK" ]] || [[ -d "\$LATEST_LINK" ]]; then
        rsync -a --delete --link-dest="\$LATEST_LINK" "\$SERVER_ROOT/" "\$BACKUP_DIR/"
    else
        rsync -a --delete "\$SERVER_ROOT/" "\$BACKUP_DIR/"
    fi

    ln -sfn "\$BACKUP_DIR" "\$LATEST_LINK"
    find "\$BACKUP_ROOT" -maxdepth 1 -mindepth 1 -type d -name 'backup_*' -mtime "+\$RETENTION_DAYS" -exec rm -rf {} +
    log "バックアップが完了しました。"
}

main "\$@"
EOF

    chmod 755 "$BACKUP_SCRIPT_PATH"
}

write_saveall_script() {
    log "save-all スクリプトを作成します。"
    cat >"$SAVEALL_SCRIPT_PATH" <<EOF
#!/usr/bin/env bash
set -euo pipefail

TMUX_SESSION_NAME="$TMUX_SESSION_NAME"

if ! /usr/bin/tmux has-session -t "\$TMUX_SESSION_NAME" 2>/dev/null; then
    exit 0
fi

/usr/bin/tmux send-keys -t "\$TMUX_SESSION_NAME" save-all C-m
EOF

    chmod 755 "$SAVEALL_SCRIPT_PATH"
}

sync_profile_contents() {
    log "mods と config を同期します。"
    REPO_URL="$REPO_URL" \
    REPO_REF="$REPO_REF" \
    PROFILE_NAME="$PROFILE_NAME" \
    SERVER_ROOT="$SERVER_ROOT" \
    WORK_DIR="$BOOTSTRAP_ROOT" \
    REPO_DIR="$REPO_DIR" \
    SKIP_REPO_PREPARE=1 \
    bash "$SYNC_SCRIPT_PATH"

    chown -R "$MINECRAFT_USER:$MINECRAFT_GROUP" "$SERVER_ROOT"
}

confirm_eula() {
    local eula_path="$SERVER_ROOT/eula.txt"
    if [[ -f "$eula_path" ]] && grep -Eq '^eula=true$' "$eula_path"; then
        log "EULA はすでに承諾済みです。"
        return
    fi

    echo
    echo "Minecraft EULA への同意が必要です。"
    echo "EULA: https://aka.ms/MinecraftEULA"
    read -r -p "EULA に同意して続行しますか? [yes/no]: " answer
    if [[ "$answer" != "yes" ]]; then
        fail "EULA に同意しないため処理を中断しました。"
    fi

    printf 'eula=true\n' >"$eula_path"
    chown "$MINECRAFT_USER:$MINECRAFT_GROUP" "$eula_path"
}

write_systemd_service() {
    local service_path="/etc/systemd/system/$SYSTEMD_SERVICE_NAME"

    log "systemd サービスを作成します: $SYSTEMD_SERVICE_NAME"
    cat >"$service_path" <<EOF
[Unit]
Description=Minecraft Server ($PROFILE_NAME)
After=network-online.target
Wants=network-online.target

[Service]
Type=oneshot
RemainAfterExit=yes
User=$MINECRAFT_USER
Group=$MINECRAFT_GROUP
WorkingDirectory=$SERVER_ROOT
ExecStartPre=-/usr/bin/tmux kill-session -t $TMUX_SESSION_NAME
ExecStart=/usr/bin/tmux new-session -d -s $TMUX_SESSION_NAME /bin/bash -lc './run.sh nogui'
ExecStop=/usr/bin/tmux send-keys -t $TMUX_SESSION_NAME stop C-m
ExecStop=/bin/bash -lc 'for i in \$(seq 1 120); do if ! /usr/bin/tmux has-session -t $TMUX_SESSION_NAME 2>/dev/null; then exit 0; fi; sleep 1; done; /usr/bin/tmux kill-session -t $TMUX_SESSION_NAME'
TimeoutStopSec=130

[Install]
WantedBy=multi-user.target
EOF

    systemctl daemon-reload
    systemctl enable "$SYSTEMD_SERVICE_NAME"
}

write_backup_systemd_units() {
    local service_path="/etc/systemd/system/$BACKUP_SERVICE_NAME"
    local timer_path="/etc/systemd/system/$BACKUP_TIMER_NAME"

    log "バックアップ用 systemd service を作成します: $BACKUP_SERVICE_NAME"
    cat >"$service_path" <<EOF
[Unit]
Description=Minecraft Backup ($PROFILE_NAME)
After=network-online.target
Wants=network-online.target

[Service]
Type=oneshot
User=root
ExecStart=$BACKUP_SCRIPT_PATH
EOF

    log "バックアップ用 systemd timer を作成します: $BACKUP_TIMER_NAME"
    cat >"$timer_path" <<EOF
[Unit]
Description=Minecraft Backup Timer ($PROFILE_NAME)

[Timer]
OnCalendar=hourly
Persistent=true
RandomizedDelaySec=15m

[Install]
WantedBy=timers.target
EOF

    systemctl daemon-reload
    systemctl enable --now "$BACKUP_TIMER_NAME"
}

write_saveall_systemd_units() {
    local service_path="/etc/systemd/system/$SAVEALL_SERVICE_NAME"
    local timer_path="/etc/systemd/system/$SAVEALL_TIMER_NAME"

    log "save-all 用 systemd service を作成します: $SAVEALL_SERVICE_NAME"
    cat >"$service_path" <<EOF
[Unit]
Description=Minecraft save-all ($PROFILE_NAME)

[Service]
Type=oneshot
User=root
ExecStart=$SAVEALL_SCRIPT_PATH
EOF

    log "save-all 用 systemd timer を作成します: $SAVEALL_TIMER_NAME"
    cat >"$timer_path" <<EOF
[Unit]
Description=Minecraft save-all Timer ($PROFILE_NAME)

[Timer]
OnBootSec=3min
OnUnitActiveSec=3min
Persistent=true

[Install]
WantedBy=timers.target
EOF

    systemctl daemon-reload
    systemctl enable --now "$SAVEALL_TIMER_NAME"
}

start_service() {
    log "Minecraft サーバーを起動します。"
    systemctl restart "$SYSTEMD_SERVICE_NAME"
    sleep 5
    systemctl --no-pager --full status "$SYSTEMD_SERVICE_NAME" || true
}

show_summary() {
    cat <<EOF

セットアップが完了しました。

- profile: $PROFILE_NAME
- 配置先: $SERVER_ROOT
- systemd: $SYSTEMD_SERVICE_NAME
- tmux: $TMUX_SESSION_NAME
- backup: $BACKUP_ROOT
- backup script: $BACKUP_SCRIPT_PATH
- backup timer: $BACKUP_TIMER_NAME
- save-all script: $SAVEALL_SCRIPT_PATH
- save-all timer: $SAVEALL_TIMER_NAME

確認コマンド:
- systemctl status $SYSTEMD_SERVICE_NAME
- tmux attach -t $TMUX_SESSION_NAME
- systemctl status $BACKUP_TIMER_NAME
- $BACKUP_SCRIPT_PATH
- systemctl status $SAVEALL_TIMER_NAME
- $SAVEALL_SCRIPT_PATH

EOF
}

main() {
    require_root
    install_packages
    require_command git
    require_command curl
    require_command tmux
    require_command java
    require_command runuser
    require_command rsync
    require_command systemctl
    require_command sha1sum

    prepare_repo
    ensure_profile_exists
    ensure_minecraft_user
    ensure_backup_mount
    prepare_directories
    ensure_server_jar
    ensure_neoforge_server
    ensure_jvm_args
    write_backup_script
    write_saveall_script
    sync_profile_contents
    confirm_eula
    write_systemd_service
    write_backup_systemd_units
    write_saveall_systemd_units
    start_service
    show_summary
}

main "$@"
