# Enhanced Storage + Backpack

Singleplayer-Mod für Schedule I von codexgaming-de, entwickelt mit KI-Unterstützung.

## 0.0.5 – Fehlerkorrektur in Prüfung

Die bereitgestellte Manager-DLL wurde untersucht. Nach Sprachänderungen fordert
unser Plugin jetzt über die öffentliche TriggerUIRefresh-API eine Aktualisierung an.
Diese Ergänzung ist noch nicht gebaut oder im Spiel bestätigt: Die lokalen
.NET-Buildwerkzeuge scheitern momentan bereits beim Zugriff auf Prozessinformationen.
Nicht als fertige Korrektur freigegeben.

Neu: Beschreibungstexte für sämtliche Einstellungen in beiden Sprachen, korrigierte
Slot-Registrierung und Entfernung, keine Skalierung mit vor dem Öffnen erfassten
Zellgrößen. Details und verbleibende Unsicherheiten: [Fehleranalyse](docs/BUGS-0.0.4.md).

## Funktionsumfang in Prüfung

- Metadaten für Einstellungen, Kategorien und Beschreibungen auf Deutsch und
  Englisch. Die sichtbare Aktualisierung muss noch im Spiel bestätigt werden.
- Nach einem Sprachwechsel wird die öffentliche Aktualisierungs-API des Managers
  einmal aufgerufen. Die Oberfläche des fremden Mod Managers und
  dessen eigene Schaltflächen werden nicht vom Plugin übersetzt.
- Small Storage Rack / Kleines Lagerregal: 1–128 Plätze, eigene Reihenanzahl.
  0 verwendet jeweils die ursprüngliche Spielvorgabe.
- Reihen werden für die Darstellung auf die tatsächliche Zahl der Plätze begrenzt.
- Einstellungsänderungen werden bei der nächsten Spielaktualisierung verarbeitet.
  Während eines Drag-and-drop-Vorgangs wird bis zum Ablegen gewartet; während
  Speichern/Laden werden angeforderte Größenänderungen ebenfalls zurückgestellt.
- Beim Verkleinern bleiben belegte, gesperrte, gefilterte oder anderweitig gebundene
  Plätze erhalten. Leere Plätze am Ende können entfernt werden. Nach Entleerung
  wird die angeforderte Größe erneut geprüft; spätestens beim nächsten Öffnen.
- Zusätzliche UI-Plätze werden bei Bedarf einmal erzeugt und wiederverwendet.
- Keine dauernden Welt-/Lagersuchen und keine periodischen Datei-Schreibvorgänge.

**Andere Lagertypen und der Backpack haben weiterhin nur Einstellungen.**
Die Regal-Funktionen sind gebaut, aber noch nicht im Spiel bestätigt. Keine
Veröffentlichung als fertige Nexus-Version.

## Aktualisieren und bauen

Spiel beenden. Voraussetzungen: .NET SDK 8, Schedule I IL2CPP und MelonLoader.

```bash
cd /home/codex/Enhanced-Storage-Backpack
git pull --ff-only origin main &&
dotnet build -c Release -p:GameDirectory="/home/codex/Schreibtisch/Schedulue 1 Plugins/" &&
cp "bin/Release/net6.0/EnhancedStorageBackpack.dll" \
   "/home/codex/Schreibtisch/Schedulue 1 Plugins/Mods/"
```

Spiele- und Loader-DLLs werden nur lokal referenziert. Alternativ sind die
MSBuild-Parameter `MelonLoaderDirectory` und `GameAssembliesDirectory` verfügbar.

## Gemeinsamer Test – zunächst mit einer Kopie des Spielstands

1. Diagnoseprotokoll einschalten. Sprache Deutsch wählen, Einstellungsansicht neu
   öffnen: Kategorien und Einträge müssen deutsch sein. Dasselbe mit English prüfen.
2. Für das **kleine Lagerregal** 16 Plätze und 4 Reihen einstellen und ein leeres
   kleines Lagerregal öffnen. Anzahl und Darstellung prüfen.
3. Während derselben Sitzung auf 24 Plätze und 4 Reihen ändern. Bestehende und neu
   aufgestellte kleine Lagerregale prüfen. Andere Lagertypen dürfen sich nicht ändern.
4. Einen Gegenstand auf Platz 24 legen. Auf 8 Plätze / 2 Reihen reduzieren:
   Platz 24 muss mit Inhalt erhalten bleiben. Gegenstand entnehmen: Danach darf
   das Regal auf 8 Plätze schrumpfen. Drag-and-drop und Schnellverschieben prüfen.
5. Auf 128 Plätze / 8 Reihen erweitern und die letzten Plätze prüfen. Danach
   5 Plätze / 10 Reihen mit leerem Regal testen: höchstens 5 angezeigte Reihen.
6. Gegenstände in erweiterten Plätzen ablegen, **Spielstand speichern**, neu laden.
   Inhalte und Mengen müssen identisch sein. Ebenso Beenden ohne Speicherung
   prüfen: Es muss der Inhalt des letzten Spielstands wiederhergestellt werden.
7. Nach dem Speichern mit belegtem erweiterten Platz die konfigurierte Größe im
   Hauptmenü verkleinern und erst dann laden: Der belegte Platz muss erhalten bleiben.
8. Einstellungen Slots/Rows auf 0 zurücksetzen; soweit Plätze leer sind, müssen
   ursprüngliche Größe und Reihenanzahl wiederhergestellt werden.

Bis diese Tests abgeschlossen sind, kein verlässlich geprüfter Ersatz für die
bisherige Lagerverwaltung. Bei Auffälligkeiten den Test stoppen und das Debug-Log
mit Beschreibung bzw. Screenshot senden; nicht über den einzigen Originalstand speichern.

## Diagnose

`UserData/Enhanced-Storage-Backpack-Debug.log`, optional im Mod Manager einschaltbar.
Begrenzt auf ungefähr 1 MiB plus eine Vorgängerdatei. Kein zyklisches Logging.

- `ESB_SETTING_CURRENT` / `ESB_SETTING_CHANGED`: geladene/geänderte Einstellungen.
- `ESB_RACK_FOUND`: erkanntes kleines Lagerregal mit ursprünglicher Kapazität.
- `ESB_RACK_APPLIED`: angeforderte und tatsächliche Kapazität, angewandte Reihen.
- `ESB_RACK_SHRINK_DEFERRED`: geschützte Plätze verhindern vollständiges Verkleinern.
- `ESB_RACK_LOAD`: Kapazität vor Wiederherstellung durch den nativen Loader.
- `ESB_STORAGE_OPEN` / `ESB_RACK_MENU`: Diagnose der Erkennung und Anzeige.

## Speichervertrag

Regalinhalte bleiben in der nativen Speicherverwaltung des Spiels. Vor dem Laden
wird genügend Platz für die eingehenden gespeicherten Slots bereitgestellt;
Verkleinerung erst nach der Wiederherstellung. Es gibt keine zusätzliche Datei
für Regalinhalte und keinen eigenen Inventar-Autosave.

Für den späteren Backpack gilt der vereinbarte Vertrag in [docs/ROADMAP.md](docs/ROADMAP.md):
Inhalte ausschließlich zusammen mit dem Spielstand speichern; niemals durch
Gegenstandsbewegungen, Größenänderungen oder die Save-Taste des Mod Managers.

## Validierung

- 0.0.1: Build und Laden beim Nutzer bestätigt: Spiel 0.4.6f13, MelonLoader 0.7.3,
  Wine/Proton 11.0. Mod Manager & Phone App 2.2.4 geladen.
- 0.0.2/0.0.3: Nutzerlogs belegen Wertänderungen während der Sitzung, Persistenz
  nach Neustart und Ausgabe des eigenen Debug-Logs. Die hohen Reihenwerte im Log
  waren ausschließlich Testeingaben, keine gewünschten Vorgaben.
- 0.0.4: Release-Build gegen die bereitgestellten DLLs: 0 Warnungen, 0 Fehler.
  16.395 Prüfungen der reinen Größen-/Reihenregeln bestanden (einschließlich aller
  Kombinationen aus Zielkapazität 1–128 und einem geschützten Platz).
  Der anschließende Nutzertest zeigte die in der Fehleranalyse beschriebenen Laufzeitfehler.
- 0.0.5 vor Ergänzung der Manager-API: Release-Build mit 0 Warnungen/Fehlern und bisherige Größenregeltests bestanden.
  Die anschließende API-Ergänzung konnte wegen eines lokalen .NET-Werkzeugfehlers noch nicht gebaut werden.
  Die Korrekturen wurden noch nicht im Spiel geprüft.
  Das sind keine Unity-Laufzeittests. Native Methoden liegen hier nur als
  IL2CPP-Interop-Schnittstellen vor. UI, Gegenstandsbewegungen und Speicherverhalten
  müssen mit obigem Testplan im tatsächlichen Spiel geprüft werden.

```bash
dotnet run --project tests/StorageRulesTests.csproj -c Release
```

Die Kategoriekennungen bleiben stabil, damit gespeicherte Einstellungen und die
[Mod-Manager-Erkennung](https://github.com/Prowiler/schedule1-mod-manager-wiki/wiki/Integration-Guide)
bei einem Sprachwechsel erhalten bleiben. Keine Referenz auf die Mod-Manager-DLL.
