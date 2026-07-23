# Backlog – Essensplan

Ideensammlung für die Weiterentwicklung. Nicht priorisiert im Sinne eines Sprints,
sondern grob nach Themenbereich sortiert. Häkchen sind bewusst weggelassen — das
hier ist ein Ideenspeicher, kein Statustracker.

## 🛒 Einkaufsliste

- Automatische Einkaufsliste aus dem Wochenplan generieren (Zutaten aller geplanten
  Menüs zusammenführen, Mengen addieren, nach Kategorie/Gang gruppieren).
- Zutaten beim Einkaufen abhaken (persistenter Haken, nicht nur clientseitig).
- Manuelle Zusatzeinträge auf der Einkaufsliste ("Küchenpapier", "Katzenfutter").
- Mengen-Umrechnung/-Bündelung, wenn dieselbe Zutat in mehreren Rezepten mit
  unterschiedlichen Einheiten vorkommt (g/kg, ml/l).
- Einkaufsliste teilen/exportieren (PDF, Text zum Kopieren, evtl. WhatsApp-Link).

## 👨‍👩‍👧‍👦 Mehrbenutzer & Familie

- Login/Benutzerkonten – aktuell ist die App komplett ohne Auth (Single-Tenant).
- Mehrere Haushalte/Familien mit getrennten Daten (Mandantentrennung).
- Rollen: wer darf Rezepte anlegen, wer darf den Wochenplan ändern.
- Gemeinsame Nutzung: mehrere Personen sehen denselben Wochenplan live (z. B. via
  SignalR-Broadcast statt nur lokalem State).
- Kommentare/Bewertungen zu Rezepten ("hat allen geschmeckt", 1–5 Sterne).

## 🍳 Rezepte

- Portionsrechner: Zutatenmengen live auf gewünschte Portionszahl umrechnen.
- Nährwertangaben (Kalorien, Makros) – manuell erfassbar oder über eine
  Nährwert-API automatisch geschätzt.
- Zubereitungsschritte als einzelne, abhakbare Checkliste statt Fließtext.
- Timer direkt im Rezept (z. B. "20 Min köcheln lassen" → Klick startet Timer).
- Mehrere Bilder pro Rezept / Bild-Upload statt nur externer Bild-URL.
- Tags zusätzlich zu Kategorien (z. B. "vegetarisch", "glutenfrei", "schnell",
  "kindertauglich") – kombinierbar mit der bestehenden Kategorie-Filterung.
- Notizen/Variationen am Rezept festhalten ("beim nächsten Mal weniger Salz").
- Lieblingsrezepte markieren / "zuletzt gekocht"-Übersicht.
- Rezept-Import robuster machen: Fallback auf Microdata (statt nur JSON-LD),
  Klartext-Import (Copy-Paste von Zutaten/Anleitung mit Heuristik-Parsing),
  Import von Foto/PDF (OCR) für Familienrezepte aus dem Kochbuch.
- Rezepte duplizieren ("Kopie erstellen") als Basis für Varianten.

## 📅 Wochenplan & Planung

- Auto-Generierung feiner steuerbar machen: Filter nach Kategorie/Tag
  (z. B. "diese Woche nur vegetarisch"), Ausschluss einzelner Rezepte/Menüs.
- Vorlagen für Wochenpläne ("Standardwoche") als Startpunkt statt komplett leer.
- Wochenplan als Ganzes duplizieren (z. B. "wie letzte Woche").
- Mehrwöchige Planung / Monatsansicht.
- Drag & Drop von Menüs zwischen Tagen/Slots statt nur Auswahl-Dialog.
- Snack-Slot optional im Wochenplan-Raster mit anzeigen (aktuell nur
  Frühstück/Mittag/Abend geplant, Snack-MealType existiert im Modell bereits).
- Statistik: welche Rezepte/Menüs wie oft in den letzten Monaten geplant wurden.
- Export des Wochenplans (PDF/Kalender-Datei, damit er ausgedruckt oder in
  Google/Apple Kalender importiert werden kann).

## 📱 Mobile & Offline

- Progressive Web App (PWA): installierbar, Offline-Zugriff auf zuletzt
  geladene Rezepte/Pläne, Home-Screen-Icon.
- Push-Benachrichtigungen ("Was gibt's heute?", "Wochenplan ist noch leer").
- Bessere Touch-Bedienung für den Wochenplan-Grid auf kleinen Screens
  (aktuell horizontal scrollbar, könnte durch Swipe-Tage ersetzt werden).

## 🎨 UX-Politur

- Skeleton-Loader statt einfachem Spinner bei Ladezuständen.
- Toast-Benachrichtigungen für Aktionen (gespeichert, gelöscht, Fehler) statt
  reiner Inline-Alerts.
- Undo für Löschaktionen (kurze "Rückgängig"-Leiste statt nur Bestätigungsdialog).
- Leerer-Zustand-Illustrationen konsistent für alle Listen (Rezepte, Menüs,
  Kategorien) weiter ausbauen.
- Barrierefreiheit prüfen: Tastaturnavigation im Wochenplan-Grid und den
  Modals, ARIA-Labels für Icon-only-Buttons.
- Sortierbare Kategorien per Drag & Drop statt nur SortOrder-Feld.

## 🛠 Technik & Qualität

- Unit-/Integrationstests (aktuell keine automatisierten Tests vorhanden) –
  insbesondere für `WeekPlanService.AutoGenerateAsync` (Wiederholungslogik)
  und `RecipeImportService` (JSON-LD-Parsing verschiedener Website-Formate).
- CI-Pipeline (GitHub Actions): Build + Tests bei jedem PR.
- Strukturiertes Logging/Monitoring für Produktion.
- Rate-Limiting/Timeout-Härtung beim Rezept-Import (Schutz vor Missbrauch als
  offener URL-Fetcher).
- Soft-Delete statt Hard-Delete für Rezepte/Menüs (Wiederherstellbarkeit).
- Healthcheck-Endpoint für Deployment-Monitoring.
- Docker-Compose-Setup (App + MySQL) für einfacheres lokales Onboarding.

## 💡 Ideen für später

- KI-gestützte Menüvorschläge basierend auf vorhandenen Zutaten im Kühlschrank.
- Saisonale Rezeptvorschläge (was passt gerade jetzt im Juli).
- Reste-Verwertung: "Was kann ich aus X, Y, Z kochen?"
- Integration mit Lieferdiensten/Online-Supermärkten für die Einkaufsliste.
- Mehrsprachigkeit (aktuell komplett auf Deutsch hartkodiert).
