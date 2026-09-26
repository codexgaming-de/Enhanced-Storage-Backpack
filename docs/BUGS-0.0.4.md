# Untersuchung der Rückmeldung vom 25.09.2026

Basis: Commit c74e195 (0.0.4), Schedule I 0.4.6f13 IL2CPP, MelonLoader 0.7.3,
Mod Manager & Phone App 2.2.4. Nutzerbilder und Debug-Log eingesehen, kein eigener
laufender Spielprozess. Die bisherigen .NET-Regeltests erfassen keine nativen API-Nebenwirkungen.

## 1. Ausfall der Größenänderung

Belegt: `ItemSlot.SetSlotOwner` wirft in `SmallRackStorage.Apply` eine native
NullReferenceException. Dort wurde beim Entfernen `SetSlotOwner(null)` aufgerufen.
Danach blockierte `rack.Failed` weitere Änderungen. Das erklärt, weshalb spätere
Einstellungen auf 16 bei diesen Instanzen wirkungslos blieben.

Stark gestützt: `SetSlotOwner(owner)` registriert neue Slots bereits in der Owner-Liste.
Der bisherige zusätzliche Add-Aufruf erzeugte doppelte Referenzen: Ziel 5, beobachtete
Liste 6, danach erfolgloser Versuch, die überzählige Referenz zu entfernen.
Native Methodenbodies sind in den Interop-DLLs nicht enthalten; die genaue
Registrierungssemantik ist nicht separat im Spiel reproduziert worden.

Korrektur: Nach SetSlotOwner prüfen wir die Liste und hängen nur bei unverändertem
Count an. Genau ein zusätzlicher Slot und seine Identität werden geprüft. Entfernte
leere Slots werden aus der Liste genommen und über das verfügbare native Backing-Feld
vom Owner gelöst, ohne den nicht nullfähigen Registrierungsaufruf.
Leere Standardfilter gelten nicht mehr als schützenswerter individuell gesetzter Filter.
Platzierungsvorschauen ohne ItemDefinition werden nicht mehr als Regal registriert.
Keine automatische Reparatur alter gespeicherter Inhalte: Das wäre ohne Originaldaten
mehrdeutig. Test mit einem Stand vor 0.0.4 beziehungsweise einem neuen leeren Regal.

## 2. Unsichtbare Plätze

Belegt: Das Log meldet 24 Slots / 4 Reihen, während im Bild nur ein Filter-Symbol
und keine normal großen Slotflächen sichtbar sind. Es handelt sich mindestens um
einen Darstellungsfehler; aus dem Bild lässt sich kein Gegenstandsverlust ableiten.

Wahrscheinlich: Zellgrößen wurden vor dem nativen Open aus dem inaktiven Grid
zwischengespeichert und danach für eine eigene Skalierung verwendet. Null- oder
noch nicht berechnete Maße konnten die Felder zusammenschieben.

Korrekturversuch: Die native Zellgröße beibehalten; eigene Skalierung entfernt.
Gezielte Diagnose `ESB_RACK_LAYOUT` enthält die Maße nach nativer Öffnung.
Die ursprüngliche Darstellung muss im Spiel erneut geprüft werden. Kein globaler
Canvas-Rebuild und keine Wartezeit als vermeintliche Reparatur.

## 3. Fehlende Beschreibungen

Belegt durch Code und Screenshot: Description wurde bei CreateEntry nicht gesetzt.
Nun erhalten alle 22 Einträge eine deutsche und englische Beschreibung; beide
Metadaten werden zusammen mit den Bezeichnungen bei Sprachwahl aktualisiert.
Die sichtbare Übernahme durch den Manager ist noch zu prüfen.

## 4. Sichtbarer Sprachwechsel

Belegt: Im Log wechselt Language Deutsch → English → Deutsch. Die Einstellung
selbst wurde also übernommen. Die Metadatenänderung allein genügte nicht für die
beobachtete Oberfläche. Offene Erklärungen: bestehende UI-Texte werden nicht erneut
eingelesen, oder AutoTranslator übersetzt englische Texte zurück ins Deutsche.

Die bereitgestellte ModManager&PhoneApp(3).dll wurde am 26.09.2026 untersucht.
Der Manager liest DisplayName und Description beim Erzeugen der Phone-Einstellungen.
Die öffentliche Instanzmethode TriggerUIRefresh() baut die aktive Einstellungsseite
im Hauptmenü bzw. Telefon neu auf. Unser Plugin ruft diese API jetzt einmal nach
Sprachänderung im Update auf, außerhalb des Dropdown-Callbacks. Optionale Reflection
vermeidet eine feste DLL-Abhängigkeit. Fehler dieser Integration deaktivieren nicht
unsere Lagerverwaltung. Keine regelmäßige Suche, nur nach Sprachänderungen.
ESB_MANAGER_REFRESH protokolliert den Aufruf. Ein möglicher zusätzlicher Einfluss
von AutoTranslator ist noch nicht im Spiel ausgeschlossen.

## Validierung

Der Stand vor der Manager-API-Ergänzung kompiliert gegen die bereitgestellten vollständigen Referenzen: 0 Fehler,
0 Warnungen. Bisherige 16.395 Kapazitäts-/Reihenprüfungen bestanden. Das beweist nicht
die Korrektheit der nativen Registrierung oder der UI. Originalschritte wiederholen:

1. Mit leerem kleinem Regal Ziel 5: tatsächlich genau 5 Plätze, keine Ausnahme.
2. 16 → 24 → 16 an derselben Instanz ohne Entfernen, anschließend neu platzieren.
3. Sichtbare Slotflächen zählen, Normalgröße und Quick Move prüfen.
4. Belegten letzten Platz beim Schrumpfen behalten, nach Entnahme verkleinern.
5. Sprache Deutsch → English → Deutsch, geöffnete und neu geöffnete Phone-App
   sowie Hauptmenü prüfen, einschließlich Beschreibungstexte.
6. Erst danach Speichern/Laden mit Gegenständen in einer Spielstandkopie testen.

Die Manager-API-Ergänzung ist noch nicht kompiliert: dotnet CLI und MSBuild
scheitern in dieser Umgebung an System.Diagnostics.Process, bevor Projektcode
gebaut wird. Kein erfolgreicher Build oder Laufzeittest dieser Ergänzung behauptet.

## Nutzertest 27.09.2026

Nutzerbuild des API-Stands erfolgreich mit 0 Warnungen/Fehlern. Slots, Reihen und
Einlegen/Entnehmen bestätigt. Sichtbarer Sprachwechsel weiterhin defekt, auch
nach Deaktivieren von AutoTranslator. Log bestätigt englische Metadaten und
fehlerfrei zurückkehrenden API-Aufruf. Die API kann wegen interner Szene-/Panel-
Bedingungen ohne UI-Aufbau zurückkehren. Neue Diagnose ESB_MANAGER_STATE erfasst
diese Bedingungen und die ausgewählte Mod; keine Änderung der Regalverwaltung.
Diese Diagnose-Ergänzung ist noch nicht gebaut/getestet.

## Ursache der übersprungenen Phone-Aktualisierung

Diagnoselog (4) bestätigt gameScene=True, aktive Phone/App-Panels und unser Plugin
als Auswahl. Manager 2.2.4 hält JsonEditor_Template als inaktives Kind des
rightPanelContent vor. TriggerUIRefresh prüft nur StartsWith("JsonEditor_"),
keinen Aktivitätszustand, und überspringt dadurch den Neuaufbau. Unser Fallback
ruft PopulateModSettings(MelonBase) nur für unsere ausgewählte, aktive Phone-Seite
und ohne aktiven JSON-Editor auf. Keine Änderung der Manager-DLL. Hauptmenü nutzt
weiter die öffentliche API. Reflection-Signatur gegen bereitgestellte DLL geprüft;
Build und Spieltest dieser Korrektur stehen noch aus.

## Geschützte Slots außerhalb des Bildschirms

Nutzertest: Sprache funktioniert. Speichern/Laden vom Nutzer als erfolgreich
bestätigt. Reproduktion: 26 Slots/4 Reihen, Gegenstand in Slot 26; anschließend
10 Slots/1 Reihe. SafeSize erhält 26 Slots, aber die sofort angewandte eine Reihe
läuft aus dem Bildschirm. Bei Gegenstand in Slot 1 schrumpft die Kapazität normal.
Korrektur: Bei zurückgestellter Verkleinerung die Spaltenzahl des gewünschten
Layouts nicht überschreiten; zusätzliche Reihen aus tatsächlicher Kapazität
berechnen. 26 geschützte Slots bei Ziel 10/1 ergeben 3 Reihen. Nach Entnahme gelten
10/1. Keine Gegenstandsverschiebung, keine Änderung des Speicherverhaltens.
Build und Spieltest dieser Ergänzung ausstehend. Extrem breite reguläre Layouts
(z.B. explizit 128 Slots/1 Reihe) sind damit noch nicht allgemein gelöst.
