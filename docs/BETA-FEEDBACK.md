# Community feedback / Community-Rückmeldung

Version: 0.2.0-beta.2

Please include / Bitte angeben:

- Game version and IL2CPP branch / Spielversion und IL2CPP-Zweig:
- ESB version on host and each client / ESB-Version auf Host und Clients:
- MelonLoader and SteamNetworkLib versions:
- Host or client affected / Host oder Client betroffen:
- Number of players / Spieleranzahl:
- Operating system / Betriebssystem:
- Other inventory, storage or networking mods / weitere entsprechende Mods:
- Exact steps / genaue Schritte:
- Expected result / erwartetes Ergebnis:
- Actual result / tatsächliches Ergebnis:
- Saved before the issue? / vor dem Fehler gespeichert?:
- Items and quantities before/after / Gegenstände und Mengen vorher/nachher:
- Does reconnect/reload change it? / Verhalten nach Wiederbeitritt/Neuladen:

Attach `Enhanced-Storage-Backpack-Debug.log` from host AND affected client from the
same session, where possible. Review logs before posting personal information.
Screenshots/video help with slot indices, pages and UI state.

## Suggested beta checks / Vorschläge für Community-Tests

- All players install identical versions; verify connection and host settings.
- Separate personal backpacks; move full stacks, single items, merge and swap.
- Change backpack page; use quick move; test wallet/cash independently.
- Change each storage capacity/row count, including occupied high slots and 128 slots.
- Two players access shared storage in sequence and concurrently.
- Save → main menu → reload; save → complete restart → reload.
- Move items without saving → restart: only the last saved state should return.
- Disconnect/rejoin without saving; ensure transferred items are not restored twice.
- Save while a move or configuration synchronization is pending.
- Client leaves during a transfer; host later saves and both reconnect.
- Verify incompatible/missing dependencies produce blocked access, not writable
  independent backpack state. Record any unclear waiting or failure messages.

Report failures and successes separately. These are test requests, not claims
that the multiplayer behaviors have already passed.
