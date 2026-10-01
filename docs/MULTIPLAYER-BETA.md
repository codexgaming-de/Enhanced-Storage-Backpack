# Enhanced Storage with Backpack — 0.2.0-beta.2

By CoDeX-Gaming. Experimental community beta, 1 October 2026.
Developed with OpenAI Codex assistance. Core multiplayer gameplay was tested by the project owner with two Steam accounts
on one Nobara PC (main client and Wolf/Moonlight client). Automated tests do not establish compatibility
with every game version, mod combination or disconnect timing.

## Deutsch

Die stabile Singleplayer-Version **0.1.6** bleibt separat erhältlich. Diese Beta
ersetzt sie nicht. Für die Beta verwenden **Host und alle Mitspieler dieselbe
0.2.0-beta.2** und **SteamNetworkLib 1.6.0 IL2CPP**. Eine Mischung mit der stabilen
Version oder unmodifizierten Clients wird nicht unterstützt. Nur IL2CPP, nicht Mono.

### Installation

1. Spiel beenden. Einen separaten Testspielstand oder eine Kopie verwenden.
2. `Mods/EnhancedStorageBackpack.dll` aus diesem ZIP in den Spielordner kopieren.
   Es darf nur eine ESB-DLL vorhanden sein. Die stabile DLL nicht zusätzlich laden.
3. SteamNetworkLib separat von Nexus 1396 beziehen und dessen IL2CPP-DLL unter
   `UserLibs/SteamNetworkLib.dll` installieren. Die Abhängigkeit ist nicht im ZIP.
4. MelonLoader ist erforderlich. Referenzen: MelonLoader 0.7.3,
   Mod Manager & Phone App 2.2.4. Andere Versionen sind nicht zugesichert.
5. Für Rückmeldungen das eigene Debug-Logging aktivieren. Die Datei heißt
   `UserData/Enhanced-Storage-Backpack-Debug.log`; sie enthält die letzten drei
   protokollierten Sitzungen in einer Datei.

### Implementiertes Verhalten

- Der Host bestimmt Rucksackplätze sowie Plätze/Reihen aller neun Lagertypen.
  Sprache, Öffnungstaste und Logging bleiben lokal.
- Clients öffnen den Rucksack über dieselbe native Menüoberfläche. Gegenstände
  werden erst nach Host-Prüfung und geordneter Bestätigung übertragen.
- Verschieben, Teilen, Zusammenführen, Tauschen und schrittweises Quick-Move
  gewöhnlicher Gegenstände sind verbunden. Geld verwendet native Cash-Daten,
  einschließlich separatem Wallet; extreme, nicht genau darstellbare Beträge
  werden abgelehnt. Cash-Quick-Move bedient einen passenden Zielplatz pro Klick.
- Seiten mit bis zu 40 Rucksackplätzen bleiben verfügbar. Gewöhnliche Quick-Moves
  verwenden wie das Menü die aktuell sichtbaren Zielplätze.
- Bei Lagergrößenänderungen werden Clients kurz angehalten; dann folgen die
  Host-Anordnung, Inhalte, Filter und Sperren über den nativen Spielkanal.
- Belegte Plätze werden beim Verkleinern erhalten, wenn sie nicht sicher in
  vordere freie Plätze passen.
- Gegenstandsbewegungen aktualisieren nur den Host-Arbeitsspeicher. Ein regulärer
  Spielsave, einschließlich Spiel-Autosave, speichert den gemeinsamen Stand.
  Individuelle Spieler-Abmeldungen erzeugen keinen zusätzlichen ESB-Inventarsave.
- Wiederbeitritt innerhalb derselben Host-Sitzung verwendet den aktuellen
  Sitzungsstand. Ein kompletter Neustart verwendet den letzten Spielsave.
- Vor einem Save wartet der Host auf offene Übertragungen und Client-Bestätigungen.
  Fehlende Bestätigungen verhindern den Start dieses Saves; sie gelten nicht als
  erfolgreiches Speichern. Diagnose: `ESB_SAVE_BARRIER_FAILED`.

### Grenzen dieser ersten Beta

Zwei-Client-Kerntests mit gewöhnlichen Items, Speichern/Laden, Wiederbeitritt,
sicherer Größenänderung und gemeinsamer Lagerbedienung wurden vom Projektbetreiber
bestätigt. Cash-Sonderfälle, getrennte PCs/WAN, 3–4 Spieler und erzwungene Abbrüche
sind noch nicht validiert. Der native Welt-Speichervorgang besteht aus mehreren Dateien;
der Mod macht ihn nicht atomar gegen Prozessabbruch oder Stromausfall.

Andere Mods, die Inventare, RPCs oder Speichervorgänge verändern, können Konflikte
verursachen. Bei nicht sicher auflösbarer Synchronisierung bleibt die Bedienung
gesperrt und das Log verlangt ein Neuladen. Das ist keine Wiederherstellungsgarantie.
Gegenstände zum Wegwerfen zuerst in die Hotbar verschieben; direktes Verwerfen
aus dem Client-Rucksack ist gesperrt. Es gibt keinen Host-Wechsel, keinen Offline-Client-Save und keine Anti-Cheat-Zusage.
Nach Aktualisierung einer DLL muss das Spiel neu gestartet werden; Einstellungen
innerhalb einer laufenden, verbundenen Sitzung benötigen keinen Neustart.

### Rückkehr zur stabilen Version

Beta-Spielstand separat aufbewahren. Spiel beenden, Beta-DLL entfernen und nur die
stabile DLL installieren; für einen verlässlichen Rückweg den vorherigen
Singleplayer-Spielstand wiederherstellen. Ein Downgrade eines veränderten
Beta-Spielstands ist nicht validiert. Nicht beide DLLs gleichzeitig installieren.

## English

Stable singleplayer **0.1.6** remains a separate download. This is an additional,
experimental multiplayer beta, not a replacement for the stable release.
The host and every client need the **same beta build**, MelonLoader and
**SteamNetworkLib 1.6.0 IL2CPP**. Install the mod DLL in `Mods` and the separately
downloaded dependency in `UserLibs/SteamNetworkLib.dll`. Do not install two ESB
versions together. Mono and mixed stable/beta/unmodded parties are unsupported.

The host controls all capacities and storage rows; language, hotkey and logging
remain local. Backpack moves are prepared by the host and committed after native
owner acknowledgements. Normal-item quick moves proceed across suitable visible
slots; cash quick moves use one suitable destination per click. Money uses native
cash data and its wallet, with rejection of unsafe floating-point amounts.

Storage changes briefly freeze client interaction while the host replicates the
layout, contents, filters and locks. Backpack updates remain in host session
memory until a regular world save, including the game's autosave. Individual
player departures do not independently persist ESB inventory changes. Rejoining
uses the live host session; restarting uses the last world save.

World saves wait for pending transfers and client acknowledgements. A timeout is
a failed/deferred save request, not a successful save. Check the dedicated debug
log. A synchronization fault can keep interaction blocked until the save is
reloaded. The game's multi-file save is not made crash-atomic by this mod.

**Core two-client gameplay passed on the baseline used for this release.**
Cash-specific cases, separate-PC/WAN play, 3–4 players and forced crashes remain
unverified. The exact packaged DLL still requires its final smoke test. Use a
separate test save/copy. Game versions and inventory/network/save mods can affect
compatibility. No host migration or anti-cheat guarantees are provided. Keep the
stable DLL and your pre-beta singleplayer save for rollback. Downgrading a modified
beta save is unverified. See BETA-FEEDBACK.md for the community report template.

## Diagnostic notes / Diagnosehinweise

The optional connection and transfer traces remain enabled only with DebugLogging.
The log may show `ESB_MP_host-offer-rejected` for an empty native inventory during
join; this legacy diagnostic is separate from the Steam handshake. Include the
full session log rather than interpreting that line alone.

Die optionale Verbindungs- und Transferdiagnose bleibt für die Beta erhalten.
`ESB_MP_host-offer-rejected` kann beim Beitritt für ein leeres natives Inventar
erscheinen; diese alte Diagnose ist nicht das Ergebnis des Steam-Verbindungsaufbaus.
Bitte bei Problemen den vollständigen Sitzungslog beilegen.
