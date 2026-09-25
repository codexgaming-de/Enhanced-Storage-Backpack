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

Nächster diskriminierender Schritt: die tatsächlich installierte Manager-DLL
untersuchen (UI-Erzeugung, Metadatenzugriff, Aktualisierung). Neue Diagnose
`ESB_LANGUAGE_APPLIED` protokolliert Metadaten der Sprachauswahl direkt nach Änderung.
Keine globale Textersetzung und kein Eingriff in fremde Mods auf bloßen Verdacht.

## Validierung

0.0.5 kompiliert gegen die bereitgestellten vollständigen Referenzen: 0 Fehler,
0 Warnungen. Bisherige 16.395 Kapazitäts-/Reihenprüfungen bestanden. Das beweist nicht
die Korrektheit der nativen Registrierung oder der UI. Originalschritte wiederholen:

1. Mit leerem kleinem Regal Ziel 5: tatsächlich genau 5 Plätze, keine Ausnahme.
2. 16 → 24 → 16 an derselben Instanz ohne Entfernen, anschließend neu platzieren.
3. Sichtbare Slotflächen zählen, Normalgröße und Quick Move prüfen.
4. Belegten letzten Platz beim Schrumpfen behalten, nach Entnahme verkleinern.
5. Sprache Deutsch → English → Deutsch, geöffnete und neu geöffnete Phone-App
   sowie Hauptmenü prüfen, einschließlich Beschreibungstexte.
6. Erst danach Speichern/Laden mit Gegenständen in einer Spielstandkopie testen.
