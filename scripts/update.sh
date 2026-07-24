#!/bin/bash
# ============================================================
# Essensplan – App updaten
#
# Im LXC Container ausführen:
#   bash /opt/essensplan/scripts/update.sh
#
# Oder vom Proxmox Host:
#   pct exec 200 -- bash /opt/essensplan/scripts/update.sh
# ============================================================
set -euo pipefail

APP_DIR="/opt/essensplan"
APP_USER="essensplan"

echo "╔═══════════════════════════════════════════╗"
echo "║    Essensplan – Update                   ║"
echo "╚═══════════════════════════════════════════╝"
echo ""

cd "$APP_DIR"

CURRENT=$(git rev-parse HEAD)
echo "  Aktueller Commit: ${CURRENT:0:8}"

# Neue Commits holen
echo "▶ GitHub prüfen ..."
if ! git fetch --quiet origin main 2>/dev/null; then
    echo "  Git fetch fehlgeschlagen – GitHub PAT eingeben:"
    read -rp "  PAT: " PAT
    git config --global credential.helper store
    echo "https://danimerz:${PAT}@github.com" > /root/.git-credentials
    chmod 600 /root/.git-credentials
    git fetch --quiet origin main
fi

NEW=$(git rev-parse origin/main)

if [ "$CURRENT" = "$NEW" ]; then
    echo "  Bereits aktuell. Kein Update nötig."
    exit 0
fi

echo "  Neuer Commit:     ${NEW:0:8}"
echo ""

# Repository aktualisieren
echo "▶ Code aktualisieren ..."
git pull --ff-only --quiet

# Node.js Sidecar
echo "▶ Node.js Abhängigkeiten ..."
cd "$APP_DIR/migros-image-server"
npm ci --omit=dev --silent

# .NET App neu bauen
echo "▶ .NET App bauen ..."
cd "$APP_DIR"
dotnet publish src/Essensplan.Web/Essensplan.Web.csproj \
    -c Release -o "$APP_DIR/publish" --nologo -q

# Berechtigungen setzen
chown -R "$APP_USER:$APP_USER" "$APP_DIR/publish"
chown -R "$APP_USER:$APP_USER" "$APP_DIR/migros-image-server/node_modules"

# Dienste neustarten
echo "▶ Dienste neustarten ..."
systemctl restart essensplan-app essensplan-sidecar

sleep 3
systemctl is-active essensplan-app     >/dev/null && echo "  ✅ essensplan-app läuft"     || echo "  ❌ essensplan-app FEHLER"
systemctl is-active essensplan-sidecar >/dev/null && echo "  ✅ essensplan-sidecar läuft" || echo "  ❌ essensplan-sidecar FEHLER"

echo ""
echo "✅  Update auf ${NEW:0:8} abgeschlossen!"
