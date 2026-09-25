# Vereinbarter Funktionsumfang

Die folgende Liste beschreibt Ziele, keine bereits implementierten Funktionen.
0.0.1: Build und Laden im Spiel bestätigt.
0.0.2/0.0.3: Einstellungsänderungen, Persistenz und eigenes Debug-Log durch Nutzerlogs bestätigt.
Aktueller Schritt: 0.0.4 Sprachwahl und Small Storage Rack im Spiel testen.

## Backpack

- 1 bis 128 konfigurierbare Slots.
- Integration in das native StorageMenu.
- 40 Slots pro Seite mit eigener Navigation `<` und `>`.
- Konfigurierbarer Hotkey, Vorgabe B.
- Deutsch und Englisch.
- MelonPreferences, kompatibel mit Mod Manager & Phone App.
- Sicheres Verkleinern: belegte Slots nicht abschneiden.
- Polizeidurchsuchungen ignorieren den Rucksackinhalt.
- Optionales Debug-Logging mit eigenem Diagnose-Log.

## Storage

Slots (maximal 128) und Reihen pro Lagertyp separat konfigurierbar:

- Small / Medium / Large Storage Rack.
- Small / Medium / Large / Huge Storage Closet.
- Safe.
- Filing Cabinet.

Alle Einstellungen ueber den Mod Manager im Spiel; Aenderungen sofort ohne
Spielneustart wirksam. Belegte Slots bei Groessenaenderungen schuetzen.

## Entwicklung

Neuer Code im Repository `codexgaming-de/Enhanced-Storage-Backpack`.
Lokaler Ordner des Nutzers: `/home/codex/Enhanced-Storage-Backpack/`.
GitHub-Zugriff auf dem Nutzer-PC per vorhandenem SSH-Schluessel.
Keine automatische Uebernahme des alten Implementierungsstands.
Nach jedem abgegrenzten Schritt Build pruefen und gemeinsam im Spiel testen.
Ausschließlich Singleplayer; Multiplayer ist nicht Teil des Projekts.
Ressourcenschonend: ereignisbasierte Änderungen, keine dauernden vollständigen
Lagersuchen, keine unnötigen UI-Neuaufbauten oder Dateioperationen.
Angegebener Spielordner: `/home/codex/Schreibtisch/Schedulue 1 Plugins/`.


## Verbindlicher Speichervertrag für den späteren Backpack

- Gegenstandsbewegungen ändern nur den laufenden Zustand im Arbeitsspeicher.
- Dauerhaft speichern nur zusammen mit dem zugehörigen Spielstand, einschließlich
  eines regulären Spiel-Autosaves. Kein eigener Rucksack-Autosave.
- Mod-Manager-Speichern und Größenänderungen dürfen keine Gegenstände speichern.
- Beenden ohne Spielstandspeicherung verwirft die Änderungen seit dem letzten Save.
- Laden muss Inventar und Rucksack aus demselben Speicherzeitpunkt herstellen.
- Fehlgeschlagene oder unterbrochene Speicherung darf keinen neueren Rucksack mit
  einem älteren Inventar kombinieren. Das tatsächliche Save-Verfahren des Spiels
  muss vor der Implementierung untersucht werden; ein nachträglich geschriebener
  separater Inhalt allein garantiert diese Konsistenz nicht.
- Pflichtfälle: Hotbar → Backpack und Backpack → Hotbar, jeweils mit Speichern,
  ohne Speichern, nach Neustart, bei Save-Fehler/Abbruch und Spielstandwechsel.

## Noch vorläufig

Startwerte: Backpack 40, Lager 0 = Original. Storage-Reihen haben im
Einstellungstest die technische Grenze 128. Beim kleinen Lagerregal werden die tatsächlich angezeigten Reihen auf die
vorhandenen Plätze begrenzt. Hohe Reihenwerte aus den Nutzerlogs waren reine Tests. Kein UI- oder Speicherverhalten gilt durch die
reine Registrierung einer Einstellung als fertig.


Die Beschriftungen unserer Einstellungen und Kategorien müssen vollständig der
gewählten Sprache entsprechen, keine gemischten deutschen/englischen Beschriftungen.
Fremde Mod-Manager-Schaltflächen behalten dessen eigene Sprache.
