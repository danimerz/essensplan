# Backlog – Essensplan

Ideensammlung für die Weiterentwicklung. Nicht priorisiert im Sinne eines Sprints,
sondern grob nach Themenbereich sortiert. `[x]` = umgesetzt.

## ✅ Bereits umgesetzt

- [x] Login / Benutzerkonten (ASP.NET Core Identity, E-Mail + Passwort)
- [x] Mehrere Haushalte mit getrennten Daten (Mandantentrennung)
- [x] Rollen: Admin, Mitglied, SuperAdmin; wer darf was
- [x] Haushaltsverwaltung für SuperAdmin (`/admin/haushalte`): anlegen, umbenennen, löschen, Mitglieder & Einladung
- [x] Benutzerverwaltung für Admins (`/admin/benutzer`): einladen, Rolle wechseln, deaktivieren
- [x] Rezept-Import via URL (JSON-LD / schema.org)
- [x] Wochenplan mit Auto-Generierung und manuellem Drag-ähnlichem Befüllen
- [x] Kategorien mit Farbe + Emoji
- [x] Globale Rezeptbibliothek: Rezepte werden haushaltsübergreifend geteilt (Junction-Table), Fork beim Bearbeiten wenn mehrere Haushalte dasselbe Rezept nutzen
- [x] Duplikatsprüfung beim Rezept anlegen (Namensabgleich + URL-Abgleich beim Import) mit Vorschlagsanzeige
- [x] „Geteilt"-Badge auf Rezeptkarten (zeigt, wenn das Rezept in mehreren Haushalten verwendet wird)
- [x] Aktiver Haushalt in der Seitenleiste anzeigen
- [x] „Bitte warten"-Spinner auf allen Speichern-Buttons (`LoadingButton`-Komponente, deaktiviert während der Aktion)
- [x] Migros-Integration: Produktbilder, Kategorie (aus Migros-Breadcrumb), aktueller Preis und Aktions-Badge (🏷️) auf Einkaufslisten-Artikeln — Node.js Sidecar (`migros-image-server`) mit täglichem Promotions-Cache

---

## 🛒 Einkaufsliste

- [x] Automatische Einkaufsliste aus dem Wochenplan generieren (Zutaten aller geplanten Menüs zusammenführen, Mengen addieren).
- [x] Zutaten beim Einkaufen abhaken (persistenter Haken in DB, haushaltsübergreifend).
- [x] Manuelle Zusatzeinträge auf der Einkaufsliste ("Küchenpapier", "Katzenfutter") — werden gleich wie Auto-Einträge mit Migros-Daten angereichert.
- [x] Als Text kopieren (📋-Button, WhatsApp-tauglich).
- [x] Artikel nach Kategorie gruppiert (Migros-Kategorie hat Vorrang, Keyword-Fallback wenn Sidecar nicht verfügbar).
- [ ] Mengen-Umrechnung/-Bündelung, wenn dieselbe Zutat in mehreren Rezepten mit
  unterschiedlichen Einheiten vorkommt (g/kg, ml/l).
- [ ] Einkaufsliste als PDF exportieren.

## 👨‍👩‍👧‍👦 Mehrbenutzer & Familie

- [x] Gemeinsame Nutzung: mehrere Personen sehen denselben Wochenplan live (In-Process Pub/Sub via WeekPlanChangeNotifier — kein SignalR nötig in Blazor Server).
- [x] Kommentare/Bewertungen zu Rezepten ("hat allen geschmeckt", 1–5 Sterne) — implementiert in RecipeDetail.

## 🍳 Rezepte

- [ ] Globale Rezeptvorschläge auf der Rezeptliste (Rezepte anderer Haushalte sichtbar
  machen, wenn eigene Liste leer oder Suche leer ausgeht — Option B).
- [ ] Portionsrechner: Zutatenmengen live auf gewünschte Portionszahl umrechnen.
- [ ] Nährwertangaben (Kalorien, Makros) – manuell erfassbar oder über eine
  Nährwert-API automatisch geschätzt.
- [ ] Zubereitungsschritte als einzelne, abhakbare Checkliste statt Fließtext.
- [x] Timer direkt im Rezept (z. B. "20 Min köcheln lassen" → Klick startet Timer) — Regex-Erkennung im Anleitungstext, klickbare Chips, Sticky-Widget mit Countdown, Pause/Reset.
- [ ] Mehrere Bilder pro Rezept / Bild-Upload statt nur externer Bild-URL.
- [ ] Tags zusätzlich zu Kategorien (z. B. "vegetarisch", "glutenfrei", "schnell",
  "kindertauglich") – kombinierbar mit der bestehenden Kategorie-Filterung.
- [ ] Notizen/Variationen am Rezept festhalten ("beim nächsten Mal weniger Salz").
- [ ] Lieblingsrezepte markieren / "zuletzt gekocht"-Übersicht.
- [ ] Rezept-Import robuster machen: Fallback auf Microdata (statt nur JSON-LD),
  Klartext-Import (Copy-Paste von Zutaten/Anleitung mit Heuristik-Parsing),
  Import von Foto/PDF (OCR) für Familienrezepte aus dem Kochbuch.
- [ ] Rezepte duplizieren ("Kopie erstellen") als Basis für Varianten.

## 📅 Wochenplan & Planung

- [ ] Auto-Generierung feiner steuerbar machen: Filter nach Kategorie/Tag
  (z. B. "diese Woche nur vegetarisch"), Ausschluss einzelner Rezepte/Menüs.
- [ ] Vorlagen für Wochenpläne ("Standardwoche") als Startpunkt statt komplett leer.
- [ ] Wochenplan als Ganzes duplizieren (z. B. "wie letzte Woche").
- [ ] Mehrwöchige Planung / Monatsansicht.
- [ ] Drag & Drop von Menüs zwischen Tagen/Slots statt nur Auswahl-Dialog.
- [ ] Snack-Slot optional im Wochenplan-Raster mit anzeigen (aktuell nur
  Frühstück/Mittag/Abend geplant, Snack-MealType existiert im Modell bereits).
- [ ] Statistik: welche Rezepte/Menüs wie oft in den letzten Monaten geplant wurden.
- [ ] Export des Wochenplans (PDF/Kalender-Datei, damit er ausgedruckt oder in
  Google/Apple Kalender importiert werden kann).

## 📱 Mobile & Offline

- [ ] Progressive Web App (PWA): installierbar, Offline-Zugriff auf zuletzt
  geladene Rezepte/Pläne, Home-Screen-Icon.
- [ ] Push-Benachrichtigungen ("Was gibt's heute?", "Wochenplan ist noch leer").
- [ ] Bessere Touch-Bedienung für den Wochenplan-Grid auf kleinen Screens
  (aktuell horizontal scrollbar, könnte durch Swipe-Tage ersetzt werden).

## 🎨 UX-Politur

- [ ] Skeleton-Loader statt einfachem Spinner bei Ladezuständen.
- [ ] Toast-Benachrichtigungen für Aktionen (gespeichert, gelöscht, Fehler) statt
  reiner Inline-Alerts.
- [ ] Undo für Löschaktionen (kurze "Rückgängig"-Leiste statt nur Bestätigungsdialog).
- [ ] Leerer-Zustand-Illustrationen konsistent für alle Listen (Rezepte, Menüs,
  Kategorien) weiter ausbauen.
- [ ] Barrierefreiheit prüfen: Tastaturnavigation im Wochenplan-Grid und den
  Modals, ARIA-Labels für Icon-only-Buttons.
- [ ] Sortierbare Kategorien per Drag & Drop statt nur SortOrder-Feld.

## 🚀 Deployment & Infrastruktur

- [x] Proxmox LXC Deployment-Scripts (`scripts/`): Erstinstallation, App-Setup, Update-Script.
- [x] DNS-Migration zu Cloudflare (`familie-merz.ch`) — E-Mail & Nabu Casa bleiben erhalten.
- [ ] Cloudflare Tunnel einrichten für externen Zugriff (kein Port-Forwarding nötig).
- [ ] HTTPS via Cloudflare Tunnel (automatisch, kein Zertifikat managen).
- [ ] Automatisches Update per Cron-Job im LXC Container (optional, bereits im Setup-Script vorbereitet).

## 🛠 Technik & Qualität

- [ ] Unit-/Integrationstests (aktuell keine automatisierten Tests vorhanden) –
  insbesondere für `WeekPlanService.AutoGenerateAsync` (Wiederholungslogik)
  und `RecipeImportService` (JSON-LD-Parsing verschiedener Website-Formate).
- [ ] CI-Pipeline (GitHub Actions): Build + Tests bei jedem PR.
- [ ] Strukturiertes Logging/Monitoring für Produktion.
- [ ] Rate-Limiting/Timeout-Härtung beim Rezept-Import (Schutz vor Missbrauch als
  offener URL-Fetcher).
- [ ] Soft-Delete statt Hard-Delete für Rezepte/Menüs (Wiederherstellbarkeit).
- [ ] Healthcheck-Endpoint für Deployment-Monitoring.
- [ ] Docker-Compose-Setup (App + MySQL) für einfacheres lokales Onboarding.

## 💡 Ideen für später

- [ ] KI-gestützte Menüvorschläge basierend auf vorhandenen Zutaten im Kühlschrank.
- [ ] Saisonale Rezeptvorschläge (was passt gerade jetzt im Juli).
- [ ] Reste-Verwertung: "Was kann ich aus X, Y, Z kochen?"
- [x] Integration mit Migros (Produktbilder, Preise, Aktionen) — siehe oben.
- [ ] Mehrsprachigkeit (aktuell komplett auf Deutsch hartkodiert).
