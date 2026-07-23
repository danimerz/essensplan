# Essensplan

Eine moderne Blazor Web App (.NET 8, Server-Interaktivität) zum Planen von Menüs,
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
- **Design-System**: CSS-Variablen (`tokens.css`) für Light/Dark, per `data-theme`-Attribut
  am `<html>`-Element umgeschaltet. Die Wahl wird in `localStorage` gespeichert; ein
  Inline-Script in `App.razor` setzt das Theme vor dem ersten Paint (kein Flackern).

## Voraussetzungen

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
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

## Hinweise

- Beim allerersten Start legt die Migration sieben Standardkategorien an
  (Hauptgericht, Vorspeise, Beilage, Dessert, Frühstück, Suppe, Snack).
- Der Rezept-Import funktioniert mit Seiten, die strukturierte `Recipe`-Daten nach
  schema.org einbetten (JSON-LD) – das trifft auf die meisten größeren Kochseiten zu.
  Ist nichts auffindbar, wird eine Fehlermeldung angezeigt und das Rezept kann
  manuell erfasst werden.
