# Enhanced Storage + Backpack

Singleplayer-Mod für Schedule I von codexgaming-de, entwickelt mit KI-Unterstützung.

## 0.1.0 – Erster Backpack-Spieltest

Alle neun Lagertypen wurden vom Nutzer einschließlich Umräumen und Speichern/Laden
getestet. 0.1.0 ergänzt den Backpack: 1–128 Plätze, 40 pro Seite, native Oberfläche,
Hotkey B (änderbar), DE/EN und sichere Größenänderung.

Der Rucksack wird gemeinsam mit der Hotbar im Inventar-Datensatz gespeichert.
Es gibt keine unabhängige Rucksack-Speicherdatei und kein Speichern beim Ablegen.
Das Verhalten der nativen Speicherpipeline muss noch im Spiel bestätigt werden.

**Entwicklungsversion, noch kein fertiger Nexus-Release.**
Compilerprüfung erfolgreich mit elf Referenzversionswarnungen, 268 reine
Persistenzprüfungen bestanden. .NET-6-Build und Spieltest auf dem Nutzer-PC stehen aus.
Details und Testablauf: [Backpack 0.1.0](docs/BACKPACK-0.1.0.md).

Aktueller Branch: `feature/0.1.0-backpack`.
Geprüfter Storage-Zwischenstand: `feature/0.0.7-medium-rack` (0.0.13).

## Funktionsumfang in Prüfung

- Metadaten für Einstellungen, Kategorien und Beschreibungen auf Deutsch und
  Englisch. Sprachwechsel in der Phone App vom Nutzer bestätigt.
- Nach einem Sprachwechsel wird die öffentliche Aktualisierungs-API des Managers
  einmal aufgerufen. Die Oberfläche des fremden Mod Managers und
  dessen eigene Schaltflächen werden nicht vom Plugin übersetzt.
- Small Storage Rack / Kleines Lagerregal und Medium Storage Rack / Mittleres
  Lagerregal sowie Large Storage Rack / Großes Lagerregal: jeweils 1–128 Plätze und eigene Reihenanzahl.
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

**Der Backpack befindet sich im ersten Spieltest.**
Das kleine Regal ist in den dokumentierten Fällen im Spiel bestätigt. Keine
Veröffentlichung als fertige Nexus-Version.

## Aktualisieren und bauen

Spiel beenden. Voraussetzungen: .NET SDK 8, Schedule I IL2CPP und MelonLoader.

```bash
cd /home/codex/Enhanced-Storage-Backpack
git fetch origin &&
git switch feature/0.1.0-backpack &&
git pull --ff-only origin feature/0.1.0-backpack &&
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
Eine einzige `Enhanced-Storage-Backpack-Debug.log` mit maximal drei protokollierten
Spielsitzungen, getrennt durch ESB_SESSION_START. Beim ersten aktivierten Eintrag
einer neuen Sitzung werden die zwei jüngsten alten Sitzungen behalten. Beim
Umstieg werden `.log.1` und `.log.2` eingelesen und nach erfolgreicher Übernahme
entfernt. Rein deaktivierte Sitzungen erzeugen keine Einträge. Ein-/Ausschalten
innerhalb derselben Spielsitzung zählt nicht als neue Sitzung.
Ungefähr 1 MiB pro Sitzung; danach endet deren Ausgabe mit ESB_LOG_LIMIT.
Kein zyklisches Logging.

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

## Testplan 0.0.7

1. Ein kleines und ein mittleres Regal platzieren. Für das kleine 10 Slots/1 Reihe,
   für das mittlere 24 Slots/4 Reihen setzen. Größen müssen getrennt bleiben.
2. Mittleres Regal auf 16 Slots/2 Reihen ändern: vorhandenes und neu platziertes
   Regal prüfen, ohne Spielneustart. Einlegen/Entnehmen und Quick Move prüfen.
3. Im mittleren Regal Gegenstand im letzten Slot ablegen, bei freiem vorderen
   Platz auf 10 Slots verkleinern: automatisches Umräumen und Mengenerhalt prüfen.
4. Speichern/Laden: beide Typen behalten ihre Gegenstände und getrennten Größen.
5. Deutsch/English: mittlere Kategorie und Beschreibungen, Regalüberschrift prüfen.

Wenn das mittlere Regal nicht erkannt wird, enthält ESB_STORAGE_OPEN seinen Namen
und supportedRack=False. ESB_RACK_FOUND protokolliert die erkannte Item-ID.

0.0.8: Ein-Datei-Sitzungsverlauf einschließlich Migration isoliert getestet.
Nutzer bestätigt 0.0.7 für getrennte Größen und Speichern/Laden. Zielgröße 5 beim
mittleren Regal noch ungeklärt: ESB_RACK_SLOT_GUARD protokolliert Sperren, Filter
und SiblingSet-Verknüpfungen bei zurückgestellter Verkleinerung. Keine pauschale
Vanilla-Mindestgröße eingeführt und kein bestehender Schutz entfernt.
Direkte Compilerprüfung erfolgreich mit .NET-8-Referenzwarnungen;
regulärer net6-Build und Spieltest von 0.0.8 ausstehend.

0.0.9 korrigiert die zu strenge Sperre für rein regalinterne SiblingSets.
Leere Originalslots dürfen entfernt werden, wobei die Mitgliedschaft in ihrer
Gruppe bereinigt wird. Externe Gruppen und tatsächliche Sperren/Filter bleiben
geschützt. Regulärer net6-Build und Laufzeittest dieser Änderung stehen aus.

## Testablauf 0.0.11

- Großes Regal: 30 Plätze / 3 Reihen, danach 10 Plätze / 2 Reihen.
- Ein Item in Slot 30 ablegen und bei freien vorderen Plätzen verkleinern:
  Item muss erreichbar bleiben und in einen passenden vorderen Platz umziehen.
- Bei mehr als zehn belegten Plätzen darf Verkleinern nichts abschneiden.
- Gegenstände auch aus zusätzlichen Slots im Regal sichtbar, soweit Stellfläche reicht.
- Bestehendes und neu platziertes Regal; Speichern/Laden; DE/EN-Beschreibungen.
- Kleine und mittlere Regale behalten ihre eigenen Einstellungen.
- Debug-Log: ESB_RACK_FOUND muss id=largestoragerack zeigen.

## Testablauf 0.0.12

Jeden der vier Schränke separat prüfen, möglichst bestehend und neu platziert:

1. Unterschiedliche Größen: Small 12/2, Medium 24/3, Large 32/4, Huge 40/4
   (Plätze/Reihen). Änderungen dürfen die anderen Typen nicht beeinflussen.
2. Item in den letzten Slot legen; bei freien vorderen Plätzen auf 5/1 verkleinern.
   Das Item muss in einen passenden vorderen Slot umziehen und entnehmbar bleiben.
3. Mehr als fünf Plätze belegen, dann auf fünf verkleinern: belegte Plätze bleiben
   zugänglich. Nach Entleeren passt sich die Größe an.
4. Speichern/Laden, Sprache DE/EN und vorhandene Regal-Einstellungen prüfen.
5. Eigenes Debug-Log zur Bestätigung der vier Item-IDs bereitstellen.

Die native Darstellung wird nur angebunden, wenn ein passender Visualizer
vorhanden ist. Geschlossene Schränke erhalten keine künstliche Außenanzeige.

## Testablauf 0.0.13

- Tresor: 16 Plätze / 2 Reihen. Aktenschrank: 24 Plätze / 3 Reihen.
- Jeweils ein Item im letzten Slot ablegen, bei freien vorderen Plätzen auf
  5 Plätze / 1 Reihe verkleinern, anschließend Item entnehmen.
- Bei mehr als fünf belegten Plätzen muss der Überhang erreichbar bleiben.
- Bestehende und neu platzierte Objekte, Speichern/Laden und DE/EN prüfen.
- Andere Lagertypen behalten ihre jeweiligen Einstellungen.
- Eigenes Debug-Log zur Bestätigung von safe und filingcabinet bereitstellen.
