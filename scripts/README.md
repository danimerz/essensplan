# Essensplan – Deployment auf Proxmox

Anleitung zur Installation von Essensplan auf einem Proxmox-Server als LXC-Container.

## Voraussetzungen

- Proxmox VE 8.x oder neuer
- Internetzugang vom Proxmox Host
- GitHub Personal Access Token (PAT) mit **Contents: Read-only** Berechtigung
  → [GitHub → Settings → Developer Settings → Personal access tokens → Fine-grained](https://github.com/settings/tokens)
- Datenbankzugangsdaten (MySQL auf `server17.hostfactory.ch`)

---

## Erstinstallation

### 1. Scripts auf den Proxmox Host kopieren

Auf dem **Proxmox Host** (Shell oder SSH):

```bash
apt-get install -y git
git clone https://github.com/danimerz/essensplan.git /tmp/essensplan
```

### 2. Installationsscript ausführen

```bash
bash /tmp/essensplan/scripts/install-lxc.sh
```

Optionale Parameter:

```bash
bash /tmp/essensplan/scripts/install-lxc.sh [CT_ID] [MEMORY_MB] [DISK_GB]

# Beispiel mit Container-ID 201, 2 GB RAM, 10 GB Disk:
bash /tmp/essensplan/scripts/install-lxc.sh 201 2048 10
```

Standardwerte: **CT_ID=200**, Memory=1024 MB, Disk=8 GB

### 3. Interaktive Eingaben

Das Script fragt während der Installation nach:

| Eingabe | Beschreibung |
|---|---|
| GitHub PAT | Personal Access Token für den Repository-Zugriff |
| DB Server | Datenbankserver (Standard: `server17.hostfactory.ch`) |
| DB Port | Datenbankport (Standard: `3306`) |
| DB Name | Datenbankname (Standard: `essensplan`) |
| DB Benutzer | Datenbankbenutzer (Standard: `essensplan_usr`) |
| DB Passwort | Datenbankpasswort |
| Auto-Update? | Optionaler Cron-Job für automatische Updates |

### 4. Nach der Installation

Das Script gibt am Ende die IP-Adresse des Containers aus:

```
✅  Installation abgeschlossen!
   Essensplan läuft auf: http://192.168.1.xxx
```

Die App ist danach unter dieser IP im lokalen Netzwerk erreichbar.

---

## Updates

### Manuell (vom Proxmox Host)

```bash
pct exec 200 -- bash /opt/essensplan/scripts/update.sh
```

Oder direkt im Container:

```bash
pct enter 200
bash /opt/essensplan/scripts/update.sh
```

Das Update-Script:
1. Prüft ob neue Commits auf GitHub vorhanden sind
2. Führt `git pull` aus (nur wenn nötig)
3. Baut die .NET App neu (`dotnet publish`)
4. Aktualisiert Node.js-Abhängigkeiten (`npm ci`)
5. Startet beide Dienste neu

### Automatisch (Cron-Job)

Beim Setup kann ein Cron-Job eingerichtet werden, der alle 10 Minuten prüft ob neue Commits auf GitHub vorhanden sind. Ein Deploy wird nur ausgelöst wenn sich tatsächlich etwas geändert hat.

Logs des automatischen Updates:
```bash
tail -f /var/log/essensplan-update.log
```

Cron-Job nachträglich einrichten (im Container):
```bash
(crontab -l 2>/dev/null; echo "*/10 * * * * bash /opt/essensplan/scripts/update.sh >> /var/log/essensplan-update.log 2>&1") | crontab -
```

---

## Dienste verwalten

Alle Befehle werden **im LXC Container** ausgeführt (`pct enter 200`):

```bash
# Status beider Dienste anzeigen
systemctl status essensplan-app
systemctl status essensplan-sidecar

# Logs anzeigen
journalctl -u essensplan-app -f
journalctl -u essensplan-sidecar -f

# Dienste neustarten
systemctl restart essensplan-app
systemctl restart essensplan-sidecar

# Nginx neustarten
systemctl restart nginx
```

---

## Technische Details

### Was installiert wird

| Komponente | Version | Zweck |
|---|---|---|
| Debian | 12 (Bookworm) | Betriebssystem |
| .NET SDK | 10.x | Blazor Server App bauen & ausführen |
| Node.js | 22.x | Migros Sidecar (Produktbilder, Preise) |
| Nginx | aktuell | Reverse Proxy (Port 80 → App) |

### Ports

| Port | Dienst | Beschreibung |
|---|---|---|
| 80 | Nginx | Öffentlicher Zugriff (HTTP) |
| 5000 | essensplan-app | Blazor App (intern, nur localhost) |
| 3001 | essensplan-sidecar | Migros Node.js Sidecar (intern, nur localhost) |

### Verzeichnisstruktur im Container

```
/opt/essensplan/          ← Repository (git clone)
    publish/              ← .NET Release Build
    migros-image-server/  ← Node.js Sidecar
    scripts/              ← Diese Scripts

/etc/essensplan/env       ← Konfiguration & DB-Zugangsdaten (nicht im Repo)
/etc/nginx/sites-available/essensplan  ← Nginx Konfiguration
/var/log/essensplan-update.log         ← Auto-Update Logs (falls aktiviert)
```

### Systemd Dienste

| Dienst | Beschreibung |
|---|---|
| `essensplan-app` | Blazor Server App (.NET 10) |
| `essensplan-sidecar` | Migros API Sidecar (Node.js) |

Beide Dienste starten automatisch beim Booten (`systemctl enable`).

---

## Fehlerbehebung

### App startet nicht

```bash
journalctl -u essensplan-app -n 50 --no-pager
```

Häufige Ursachen:
- DB-Verbindung schlägt fehl → Passwort in `/etc/essensplan/env` prüfen
- Port 5000 belegt → `ss -tlnp | grep 5000`

### Migros Sidecar funktioniert nicht

```bash
journalctl -u essensplan-sidecar -n 50 --no-pager
```

Test:
```bash
curl http://localhost:3001/health
curl "http://localhost:3001/image?q=Butter"
```

### Nginx Fehler

```bash
nginx -t
journalctl -u nginx -n 20 --no-pager
```

### GitHub PAT abgelaufen (git pull schlägt fehl)

Neuen PAT auf GitHub erstellen und im Container hinterlegen:

```bash
pct enter 200
git config --global credential.helper store
echo "https://danimerz:NEUER_PAT@github.com" > /root/.git-credentials
chmod 600 /root/.git-credentials
```
