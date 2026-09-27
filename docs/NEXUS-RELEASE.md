# Nexus-Veröffentlichung – Beschreibung für 0.1.6

Noch nicht veröffentlicht. Normale Spieltests bestanden; Paketprüfung noch offen.
Keine Behauptung einer vollständigen Fehlerfreiheit oder garantierten FPS.

## KI-Kennzeichnung

Offizielle Quelle, geprüft am 27.09.2026; Seite aktualisiert am 04.09.2026:
https://help.nexusmods.com/article/28-file-submission-guidelines
Abschnitte „Associated Content and Categorisation“ und
„Generative AI Tagging & Categorisation“.

Für dieses Projekt ist nach unserer Einordnung **AI-Generated Content** passend:
Codex hat wesentliche Teile des Codes erzeugt. **AI Media** kommt hinzu, wenn der
hier KI-erstellte Beschreibungstext auf der Modseite verwendet wird.
**AI Assisted** allein wäre keine passende Beschreibung dieses Arbeitsablaufs.
Git-Commits und manuelle Tests machen KI-generierten Code nicht zu rein menschlich
geschriebenem Code. Vor dem tatsächlichen Upload die Richtlinien erneut prüfen.

Anforderungen, Tests und tatsächlichen Funktionsumfang nachvollziehbar beschreiben.
Fremde Spiel- oder Mod-Dateien nicht ohne Erlaubnis mitliefern. Screenshots sollen
die echte Mod zeigen. Nexus-Tags und Rechteangaben beim Upload entsprechend setzen.

## Deutscher Beschreibungstext

Enhanced Storage + Backpack erweitert die Lagerverwaltung von Schedule I um
individuelle Lagergrößen und einen zusätzlichen Rucksack. Für Singleplayer und
die IL2CPP-Version des Spiels.

- Rucksack mit 1–128 Plätzen; Taste B als anpassbarer Standard.
- Neun Lagertypen mit getrennt einstellbaren Plätzen und Reihen.
- Seitennavigation im nativen Lagermenü, bis zu 40 Plätze je Seite.
- Beim Verkleinern werden Gegenstände in passende freie Plätze umgeräumt.
  Reicht der Platz nicht aus, bleiben benötigte Plätze erhalten.
- Deutsch und Englisch; Einstellungen über MelonPreferences und den
  Mod Manager & Phone App während des Spiels anpassbar.
- Rucksackinhalt wird mit dem regulären Spielstand gespeichert. Auch reguläre
  Spiel-Autosaves zählen dazu. Kein gesondertes Speichern bei Gegenstandsbewegungen.
- Polizeidurchsuchungen ignorieren den Rucksackinhalt.
- Optionales eigenes Diagnoseprotokoll mit den letzten drei protokollierten Sitzungen.

Referenzumgebung: Schedule I 0.4.6f13, MelonLoader 0.7.3 und
Mod Manager & Phone App 2.2.4. Andere Versionen sind nicht bestätigt.
DLL bei beendetem Spiel in Mods kopieren. Vor Deinstallation Rucksack und
zusätzliche Lagerplätze leeren, Lager auf Original zurückstellen und speichern.
Multiplayer und Mono werden nicht unterstützt.

Transparenz: Dieses Projekt entstand mit OpenAI Codex. Wesentliche Teile des
Codes sowie dieser Beschreibung wurden mit generativer KI erstellt. Planung,
Anforderungen und manuelle Spieltests stammen von codexgaming-de. Quellcode und
Entwicklungsschritte sind im verlinkten GitHub-Repository nachvollziehbar.

## English description

Enhanced Storage + Backpack adds configurable storage sizes and an additional
backpack to Schedule I. Designed for singleplayer and the IL2CPP game version.

- Backpack with 1–128 slots and a configurable hotkey, B by default.
- Independent slot and row settings for nine storage types.
- Page navigation in the native storage menu, with up to 40 slots per page.
- Shrinking moves items into suitable empty slots. Required extra slots remain
  available when items cannot safely fit into the requested size.
- German and English; live settings through MelonPreferences and
  Mod Manager & Phone App.
- Backpack contents are saved with the regular game save, including game
  autosaves. Moving items does not trigger an independent inventory save.
- Police searches ignore backpack contents.
- Optional diagnostic log containing the last three logged sessions.

Reference environment: Schedule I 0.4.6f13, MelonLoader 0.7.3 and
Mod Manager & Phone App 2.2.4. Other versions have not been confirmed.
With the game closed, copy the DLL into Mods. Before uninstalling, empty the
backpack and extra storage slots, restore original storage sizes and save.
Multiplayer and Mono are not supported.

Transparency: This project was developed with OpenAI Codex. Substantial portions
of the code and this description were generated using AI. Project direction,
requirements and manual gameplay testing are provided by codexgaming-de.
Source code and development history are available in the linked GitHub repository.

## Unterstützte Lager / Supported storage

Small Storage Rack, Medium Storage Rack, Large Storage Rack,
Small Storage Closet, Medium Storage Closet, Large Storage Closet,
Huge Storage Closet, Safe, Filing Cabinet.

## Links

Quellcode / Source: https://github.com/codexgaming-de/Enhanced-Storage-Backpack
Mod Manager & Phone App: https://www.nexusmods.com/schedule1/mods/397

## Release notes

0.1.6: Backpack-Daten werden am nativen Inventory-Schreibaufruf ergänzt.
Speichern/Laden über Hauptmenü und Neustart, Duplizierungsprüfung und
Spielstandwechsel vom Projektbetreiber erfolgreich geprüft.

0.1.6: Backpack data is attached at the native Inventory write boundary.
Main-menu reload, full restart, unsaved moves without duplication and save-slot
isolation have passed the project owner's gameplay tests.

## Upload

Dateiversion 0.1.6. Nur das geprüfte Paket aus dist/ verwenden.
Checkliste: RELEASE-CHECKLIST.md. Keine generelle Fehlerfreiheit behaupten.
