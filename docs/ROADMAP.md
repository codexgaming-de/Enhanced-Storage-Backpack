# Project status — 0.1.6

The agreed singleplayer feature set is implemented and the normal gameplay tests
are confirmed by the project owner. This includes nine storage types, backpack,
live settings, DE/EN, native UI pagination, safe shrinking, police-search isolation,
save/load across restarts, unsaved moves without duplication, and save-slot isolation.

Storage contract: only regular game saves (including game autosaves) persist items.
No independent inventory save on moves, preference changes or menu closing.
Backpack and hotbar share the saved Inventory JSON.

## Release preparation

- Current README, installation/removal instructions and validation report prepared.
- German/English Nexus description and AI disclosure prepared.
- Script for a fresh net6.0 Release build, allowlisted ZIP and SHA-256 prepared.
- Pending: build and inspect real package on the user's PC; test extracted DLL.
- Pending: current screenshots and uploader's redistribution/permission choices.
- Pending: final Nexus upload. Nothing published yet.

Additional limits: no simulated write failures/interruptions, no performance
benchmark, no multiplayer support. Historical version notes remain in docs;
RELEASE-VALIDATION.md is the current acceptance record.
