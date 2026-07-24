#!/bin/bash
# ============================================================
# Essensplan – App Setup im LXC Container
# Wird automatisch von install-lxc.sh aufgerufen.
# Kann auch manuell für eine Neuinstallation ausgeführt werden.
# ============================================================
set -euo pipefail

APP_DIR="/opt/essensplan"
APP_USER="essensplan"
GITHUB_REPO="https://github.com/danimerz/essensplan.git"

echo "╔═══════════════════════════════════════════╗"
echo "║    Essensplan – App Setup                ║"
echo "╚═══════════════════════════════════════════╝"
echo ""

# ── Eingaben ──────────────────────────────────────────────────────────
echo "GitHub Personal Access Token wird für den Zugriff auf das Repo benötigt."
echo "(GitHub → Settings → Developer Settings → Personal access tokens → Fine-grained)"
echo "(Benötigte Berechtigung: Contents: Read-only)"
echo ""
read -rp "GitHub PAT: " GITHUB_PAT
echo ""

echo "Datenbankverbindung (Enter = Standardwert):"
read -rp "  DB Server   [server17.hostfactory.ch]: " DB_SERVER
DB_SERVER=${DB_SERVER:-server17.hostfactory.ch}
read -rp "  DB Port     [3306]: "                   DB_PORT
DB_PORT=${DB_PORT:-3306}
read -rp "  DB Name     [essensplan]: "             DB_NAME
DB_NAME=${DB_NAME:-essensplan}
read -rp "  DB Benutzer [essensplan_usr]: "         DB_USER
DB_USER=${DB_USER:-essensplan_usr}
read -rsp "  DB Passwort: "                         DB_PASSWORD
echo ""
echo ""

DB_CONN="Server=${DB_SERVER};Port=${DB_PORT};Database=${DB_NAME};User=${DB_USER};Password=${DB_PASSWORD};TreatTinyAsBoolean=true"

# ── System-Pakete ─────────────────────────────────────────────────────
echo "▶ System aktualisieren ..."
export DEBIAN_FRONTEND=noninteractive
apt-get update -qq
apt-get upgrade -y -qq
apt-get install -y -qq curl git nginx

# ── .NET 10 SDK ───────────────────────────────────────────────────────
echo "▶ .NET 10 SDK installieren ..."
curl -fsSL https://dot.net/v1/dotnet-install.sh \
    | bash -s -- --channel 10.0 --install-dir /usr/share/dotnet
ln -sf /usr/share/dotnet/dotnet /usr/local/bin/dotnet
echo "  .NET Version: $(dotnet --version)"

# ── Node.js 22 ────────────────────────────────────────────────────────
echo "▶ Node.js 22 installieren ..."
curl -fsSL https://deb.nodesource.com/setup_22.x | bash - >/dev/null 2>&1
apt-get install -y -qq nodejs
echo "  Node Version: $(node --version)"

# ── App-Benutzer ──────────────────────────────────────────────────────
echo "▶ App-Benutzer '$APP_USER' erstellen ..."
id "$APP_USER" &>/dev/null || useradd --system --no-create-home --shell /bin/false "$APP_USER"

# ── GitHub-Credentials für git pull speichern ─────────────────────────
git config --global credential.helper store
echo "https://danimerz:${GITHUB_PAT}@github.com" > /root/.git-credentials
chmod 600 /root/.git-credentials

# ── Repository klonen ─────────────────────────────────────────────────
echo "▶ Repository klonen ..."
rm -rf "$APP_DIR"
git clone --quiet "$GITHUB_REPO" "$APP_DIR"

# Scripts ins App-Verzeichnis kopieren (falls noch nicht vorhanden)
cp /root/update.sh "$APP_DIR/scripts/update.sh" 2>/dev/null || true
chmod +x "$APP_DIR/scripts/update.sh" 2>/dev/null || true

# ── Node.js Sidecar ───────────────────────────────────────────────────
echo "▶ Node.js Abhängigkeiten installieren ..."
cd "$APP_DIR/migros-image-server"
npm ci --omit=dev --silent

# ── .NET App bauen ────────────────────────────────────────────────────
echo "▶ .NET App bauen (Release) ..."
cd "$APP_DIR"
dotnet publish src/Essensplan.Web/Essensplan.Web.csproj \
    -c Release -o "$APP_DIR/publish" --nologo -q
echo "  Build abgeschlossen."

# ── Konfiguration speichern ───────────────────────────────────────────
echo "▶ Konfiguration speichern ..."
mkdir -p /etc/essensplan
cat > /etc/essensplan/env <<EOF
ASPNETCORE_ENVIRONMENT=Production
ASPNETCORE_URLS=http://localhost:5000
ConnectionStrings__DefaultConnection=${DB_CONN}
EOF
chmod 640 /etc/essensplan/env

# ── Berechtigungen ────────────────────────────────────────────────────
chown -R "$APP_USER:$APP_USER" "$APP_DIR"
chown root:"$APP_USER" /etc/essensplan/env

# ── Systemd: Blazor App ───────────────────────────────────────────────
echo "▶ Systemd-Dienste einrichten ..."
cat > /etc/systemd/system/essensplan-app.service <<EOF
[Unit]
Description=Essensplan Blazor Server App
After=network.target

[Service]
Type=simple
User=${APP_USER}
WorkingDirectory=${APP_DIR}/publish
ExecStart=/usr/local/bin/dotnet ${APP_DIR}/publish/Essensplan.Web.dll
Restart=always
RestartSec=5
EnvironmentFile=/etc/essensplan/env

[Install]
WantedBy=multi-user.target
EOF

# ── Systemd: Node.js Sidecar ─────────────────────────────────────────
cat > /etc/systemd/system/essensplan-sidecar.service <<EOF
[Unit]
Description=Essensplan Migros Sidecar (Node.js)
After=network.target

[Service]
Type=simple
User=${APP_USER}
WorkingDirectory=${APP_DIR}/migros-image-server
ExecStart=/usr/bin/node server.js
Restart=always
RestartSec=5
Environment=PORT=3001

[Install]
WantedBy=multi-user.target
EOF

systemctl daemon-reload
systemctl enable --now essensplan-app essensplan-sidecar

# ── Nginx ─────────────────────────────────────────────────────────────
echo "▶ Nginx konfigurieren ..."
cat > /etc/nginx/sites-available/essensplan <<'NGINX'
server {
    listen 80 default_server;
    server_name _;

    # Blazor Server benötigt WebSocket-Upgrade für SignalR
    location / {
        proxy_pass         http://localhost:5000;
        proxy_http_version 1.1;
        proxy_set_header   Upgrade $http_upgrade;
        proxy_set_header   Connection "upgrade";
        proxy_set_header   Host $host;
        proxy_set_header   X-Real-IP $remote_addr;
        proxy_set_header   X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header   X-Forwarded-Proto $scheme;
        proxy_read_timeout 86400;
    }
}
NGINX

rm -f /etc/nginx/sites-enabled/default
ln -sf /etc/nginx/sites-available/essensplan /etc/nginx/sites-enabled/essensplan
nginx -t -q
systemctl enable --now nginx

# ── Auto-Update Cron (optional) ───────────────────────────────────────
echo ""
read -rp "Automatische Updates einrichten? (alle 10 Min. auf neue Commits prüfen) [j/N]: " SETUP_CRON
if [[ "${SETUP_CRON,,}" == "j" ]]; then
    (crontab -l 2>/dev/null; echo "*/10 * * * * bash /opt/essensplan/scripts/update.sh >> /var/log/essensplan-update.log 2>&1") | crontab -
    echo "  Cron-Job eingerichtet. Logs: /var/log/essensplan-update.log"
fi

# ── Abschluss ─────────────────────────────────────────────────────────
echo ""
sleep 3
echo "Dienststatus:"
systemctl is-active essensplan-app     >/dev/null && echo "  ✅ essensplan-app läuft"     || echo "  ❌ essensplan-app FEHLER – 'journalctl -u essensplan-app' prüfen"
systemctl is-active essensplan-sidecar >/dev/null && echo "  ✅ essensplan-sidecar läuft" || echo "  ❌ essensplan-sidecar FEHLER – 'journalctl -u essensplan-sidecar' prüfen"
systemctl is-active nginx              >/dev/null && echo "  ✅ nginx läuft"              || echo "  ❌ nginx FEHLER"

IP=$(hostname -I | awk '{print $1}')
echo ""
echo "✅  Setup abgeschlossen!"
echo "   Essensplan läuft auf: http://${IP}"
