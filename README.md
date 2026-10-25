# Essensplan

Eine moderne Blazor Web App (.NET 10, Server-Interaktivität) zum Planen von Menüs,
Rezepten und dem wöchentlichen Essensplan – mit EF Core auf MySQL/MariaDB und
einem selbst geschriebenen, hell/dunkel-fähigen Design-System (kein Bootstrap).

## Funktionen

- **Rezepte**: Anlegen, bearbeiten, mit Zutatenliste, Zubereitung, Zeiten, Portionen, Bild und Kategorie.
- **Rezept-Import**: Rezept-URL einfügen – die App liest die auf den meisten Kochseiten
  eingebettete `schema.org/Recipe`-Struktur (JSON-LD) automatisch aus (Titel, Zutaten,
  Zubereitung, Zeiten, Portionen, Bild) und zeigt sie zur Kontrolle vor dem Speichern an.
- **Kategorien**: Frei definierbare Rezeptkategorien mit Icon (Emoji) und Farbe.
- **Menüs**: Ein Menü kombiniert ein oder mehrere Rezepte (z. B. Hauptgericht + Beilage)
  zu einer planbaren Mahlzeit mit fester Mahlzeit-Zuordnung (Frühstück/Mittag/Abend/Snack).
- **Wochenplan**: 7-Tage-Raster über Frühstück/Mittag/Abend. Einträge lassen sich manuell
  per Klick zuweisen oder per **„Automatisch planen“** befüllen – der Generator vermeidet
  dabei Wiederholungen der letzten ~2 Wochen, solange genug Auswahl vorhanden ist.
- **Dashboard**: Tagesübersicht, Fortschritt der aktuellen Woche, Schnellzugriffe.

## Architektur

```
src/Essensplan.Web/
  Models/          Domain-Modelle (Recipe, Menu, WeekPlan, …)
  Data/            AppDbContext + EF-Core-Migrationen
  Services/        RecipeService, MenuService, WeekPlanService, RecipeImportService, …
  Components/
    Layout/        App-Shell: Sidebar (Desktop), Topbar/BottomNav (Mobile)
    Pages/          Rezepte, Menüs, Kategorien, Wochenplan, Dashboard
    Shared/         Wiederverwendbare Komponenten (ThemeToggle, ConfirmDialog)
  wwwroot/css/      Design-System: tokens.css, base.css, layout.css, components.css
  wwwroot/js/       theme.js (Dark/Light-Umschaltung mit localStorage)
```

- **EF Core + Pomelo.EntityFrameworkCore.MySql** für den Zugriff auf MySQL/MariaDB.
  `IDbContextFactory<AppDbContext>` wird verwendet, damit jeder Service-Aufruf einen
  eigenen, kurzlebigen Kontext bekommt (empfohlenes Muster für Blazor Server).
- **Kein AutoDetect der Serverversion zur Laufzeit** – die Version wird über
  `Database:ServerVersion` konfiguriert (Default `8.0.34`), damit kein zusätzlicher
  Blocking-Call beim Start nötig ist und `dotnet ef`-Tooling ohne laufende DB funktioniert.
- **EF Core/Pomelo bewusst auf 9.0.x gepinnt**, obwohl die App selbst `net10.0` als
  TargetFramework hat: `Pomelo.EntityFrameworkCore.MySql` hat (Stand jetzt) noch keine
  für EF Core 10 passende Version veröffentlicht. 9.0.x-Bibliotheken laufen problemlos
  unter der .NET-10-Runtime (nur die TFM-Version der App selbst zählt für die
  Runtime-Anforderung) – sobald Pomelo nachzieht, können beide Pakete zusammen mit
  `Microsoft.EntityFrameworkCore.Design` auf 10.x angehoben werden.
- **Design-System**: CSS-Variablen (`tokens.css`) für Light/Dark, per `data-theme`-Attribut
  am `<html>`-Element umgeschaltet. Die Wahl wird in `localStorage` gespeichert; ein
  Inline-Script in `App.razor` setzt das Theme vor dem ersten Paint (kein Flackern).

## Voraussetzungen

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- Eine erreichbare MySQL- oder MariaDB-Instanz (lokal, Docker oder gehostet)

## Setup

1. **Connection String setzen** – entweder in `appsettings.Development.json` anpassen
   oder per Umgebungsvariable überschreiben:

   ```bash
   export ConnectionStrings__DefaultConnection="Server=localhost;Port=3306;Database=essensplan;User=essensplan;Password=DEIN_PASSWORT;TreatTinyAsBoolean=true"
   ```

2. **Datenbank anlegen und Migration ausführen:**

   ```bash
   dotnet tool install --global dotnet-ef
   cd src/Essensplan.Web
   dotnet ef database update --connection "Server=localhost;Port=3306;Database=essensplan;User=essensplan;Password=DEIN_PASSWORT;TreatTinyAsBoolean=true"
   ```

   Alternativ kann `Database:ApplyMigrationsOnStartup` auf `true` gesetzt werden
   (in der Development-Konfiguration bereits aktiv), damit die App beim Start
   automatisch migriert.

3. **App starten:**

   ```bash
   cd src/Essensplan.Web
   dotnet run
   ```

   Standardmäßig erreichbar unter `http://localhost:5070`.

## Deployment (Homelab: apps.familie-merz.ch)

Die .NET-App wird über GitHub Actions (`.github/workflows/deploy.yml`) auf den
hauseigenen App-Manager auf `apps.familie-merz.ch` deployt – gleiches Prinzip wie
`flomiapp`, `hallo` und `games`: Build → ZIP → `POST /api/deploy/essensplan` mit
Bearer-Token. Der Migros-Sidecar (`migros-image-server/`, Node.js) läuft separat auf
demselben Server und wird eigenständig eingerichtet/aktualisiert, **nicht** über diesen
Workflow – der App-Manager kennt nur einzelne .NET-DLLs, keine Begleitprozesse.

### Einmaliges Setup im App-Manager (https://apps.familie-merz.ch)

1. Neue App **`essensplan`** anlegen:
   - **DLL**: `Essensplan.Web.dll` (muss exakt so heissen, sonst bricht der Deploy mit
     „`Essensplan.Web.dll` fehlt im ZIP“ ab)
   - **Health-Path**: `/` (Standard genügt – nicht angemeldete Zugriffe liefern einen
     302 auf `/anmelden`, das zählt dem App-Manager als „gesund“, da nur < 500 geprüft wird)
   - **Datenbank**: `mysql`, `ConnectionKey` = `DefaultConnection` (Standardwert –
     **nicht** auf `Default` umbenennen, sonst greift der localdb-Platzhalter aus
     `appsettings.json`, wie es bei `flomiapp` anfangs passiert ist)
   - **Subdomain**: nach Wunsch (z. B. `essensplan.familie-merz.ch`)
2. Deploy-Token für die App generieren und als Repo-Secret `APPMGR_TOKEN` in
   GitHub hinterlegen (Settings → Secrets and variables → Actions).
3. In der App-Detailansicht im **Umgebungsvariablen-Editor** zusätzlich setzen:

   ```
   Database__ApplyMigrationsOnStartup=true
   Database__ServerVersion=8.4.11
   Setup__AdminInitialPassword=<optional, sonst wird eines generiert und geloggt>
   ```

   `ApplyMigrationsOnStartup` ist nötig, weil die Web-LXC nur die ASP.NET-Core-**Runtime**
   hat (kein SDK, kein `dotnet ef`) – die App muss ihre Migrationen beim Start also selbst
   ausführen. `ServerVersion` passend zur dort laufenden MySQL-Version setzen (laut
   Homelab-Notizen aktuell 8.4.11), sonst greift der Default `8.0.34`.

   **Erststart legt automatisch einen SuperAdmin-Benutzer an** (`dani@familie-merz.ch`,
   siehe `SeedAsync` in `Program.cs`) – das initiale Passwort steht danach einmalig im
   Log (`journalctl -u blazor@essensplan`), falls `Setup__AdminInitialPassword` nicht
   gesetzt wurde. Nach dem ersten Login unbedingt ändern.

4. MySQL-Datenbank + eigener DB-User (nicht `root`) auf der DB-VM anlegen, z. B.:

   ```sql
   CREATE DATABASE essensplan CHARACTER SET utf8mb4;
   CREATE USER 'essensplan'@'192.168.1.22' IDENTIFIED BY 'Sicheres!Passwort1';
   GRANT ALL ON essensplan.* TO 'essensplan'@'192.168.1.22';
   ```

   und den Connection-String ebenfalls im Umgebungsvariablen-Editor eintragen:

   ```
   ConnectionStrings__DefaultConnection=Server=<DB_HOST>;Port=3306;Database=essensplan;User=essensplan;Password=...;TreatTinyAsBoolean=true
   ```

   (`APP_DLL` und `ASPNETCORE_URLS` setzt der App-Manager selbst – nicht manuell anfassen.)

5. Migros-Sidecar separat auf dem Server einrichten (eigener systemd-Dienst o. ä.) und
   `MigrosImageServer:Port` setzen, falls er nicht auf dem Standardport `3001` läuft:

   ```
   MigrosImageServer__Port=3001
   ```

### Deploy auslösen

GitHub → Actions → „Deploy essensplan“ → **Run workflow**. Der Workflow baut mit dem
.NET-10-SDK, packt `publish/` als ZIP und lädt es per Bearer-Token hoch. Bei
erfolgreichem Health-Check wird die neue Version aktiv (3 Releases werden vorgehalten,
bei fehlgeschlagenem Health-Check rollt der App-Manager automatisch zurück).

## Hinweise

- Beim allerersten Start legt die Migration sieben Standardkategorien an
  (Hauptgericht, Vorspeise, Beilage, Dessert, Frühstück, Suppe, Snack).
- Der Rezept-Import funktioniert mit Seiten, die strukturierte `Recipe`-Daten nach
  schema.org einbetten (JSON-LD) – das trifft auf die meisten größeren Kochseiten zu.
  Ist nichts auffindbar, wird eine Fehlermeldung angezeigt und das Rezept kann
  manuell erfasst werden.
