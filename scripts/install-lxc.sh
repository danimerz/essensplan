#!/bin/bash
# ============================================================
# Essensplan – LXC Container erstellen
# Dieses Script auf dem PROXMOX HOST ausführen.
#
# Verwendung:
#   bash scripts/install-lxc.sh [CT_ID] [MEMORY_MB] [DISK_GB]
#
# Beispiel:
#   bash scripts/install-lxc.sh 200 1024 8
# ============================================================
set -euo pipefail

CT_ID=${1:-200}
CT_MEMORY=${2:-1024}
CT_DISK=${3:-8}
CT_CORES=2
STORAGE="local-lvm"
BRIDGE="vmbr0"

echo "╔═══════════════════════════════════════════╗"
echo "║    Essensplan – LXC Container Setup      ║"
echo "╚═══════════════════════════════════════════╝"
echo ""
echo "  Container ID : $CT_ID"
echo "  Memory       : ${CT_MEMORY} MB"
echo "  Disk         : ${CT_DISK} GB"
echo "  Storage      : $STORAGE"
echo "  Bridge       : $BRIDGE"
echo ""

# Prüfen ob Container bereits existiert
if pct status "$CT_ID" &>/dev/null; then
    echo "FEHLER: Container $CT_ID existiert bereits."
    echo "Anderen CT_ID wählen, z.B.: bash scripts/install-lxc.sh 201"
    exit 1
fi

# Debian 12 Template herunterladen falls nötig
TEMPLATE=$(pveam available --section system 2>/dev/null | awk '/debian-12-standard/{print $2}' | tail -1)
if [ -z "$TEMPLATE" ]; then
    echo "FEHLER: Kein debian-12-standard Template verfügbar."
    echo "Bitte in Proxmox unter Storage > CT Templates > 'Vorlagen aktualisieren' klicken."
    exit 1
fi

if ! pveam list local 2>/dev/null | grep -q "debian-12-standard"; then
    echo "Lade Debian 12 Template herunter: $TEMPLATE ..."
    pveam download local "$TEMPLATE"
fi

LOCAL_TEMPLATE=$(pveam list local | awk '/debian-12-standard/{print $1}' | tail -1)
echo "Template: $LOCAL_TEMPLATE"
echo ""

# Container erstellen
echo "Erstelle Container $CT_ID ..."
pct create "$CT_ID" "$LOCAL_TEMPLATE" \
    --hostname essensplan \
    --memory "$CT_MEMORY" \
    --cores "$CT_CORES" \
    --rootfs "${STORAGE}:${CT_DISK}" \
    --net0 name=eth0,bridge="${BRIDGE}",ip=dhcp \
    --unprivileged 1 \
    --features nesting=1 \
    --ostype debian \
    --start 1 \
    --onboot 1

echo "Warte auf Container-Start ..."
sleep 10

IP=$(pct exec "$CT_ID" -- hostname -I 2>/dev/null | awk '{print $1}')
echo "Container IP: $IP"
echo ""

# Setup- und Update-Script in den Container kopieren
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
pct push "$CT_ID" "$SCRIPT_DIR/setup.sh"  /root/setup.sh
pct push "$CT_ID" "$SCRIPT_DIR/update.sh" /root/update.sh
pct exec "$CT_ID" -- chmod +x /root/setup.sh /root/update.sh

echo "════════════════════════════════════════════"
echo "Starte App-Setup im Container ..."
echo "════════════════════════════════════════════"
echo ""
pct exec "$CT_ID" -- bash /root/setup.sh

echo ""
echo "╔═══════════════════════════════════════════╗"
echo "║    ✅  Installation abgeschlossen!       ║"
echo "╚═══════════════════════════════════════════╝"
echo ""
IP=$(pct exec "$CT_ID" -- hostname -I 2>/dev/null | awk '{print $1}')
echo "  Essensplan läuft auf:  http://$IP"
echo ""
echo "  App manuell updaten:"
echo "    pct exec $CT_ID -- bash /opt/essensplan/scripts/update.sh"
echo ""
echo "  Oder direkt im Container:"
echo "    pct enter $CT_ID"
echo "    bash /opt/essensplan/scripts/update.sh"
echo ""
