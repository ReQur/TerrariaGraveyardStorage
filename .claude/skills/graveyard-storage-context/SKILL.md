---
name: graveyard-storage-context
description: Full project context for the Graveyard Storage tModLoader mod. Use when making changes, debugging, adding features, or answering questions about this mod.
---

# Graveyard Storage Mod Context

A tModLoader mod that stores a player's items in the vanilla gravestone on Mediumcore/Hardcore death, with a
"Get Items" button that restores items to their original inventory slots.

## Read these first

1. **`CLAUDE.md`** (project root) — architecture, core flow, guaranteed-placement feature, file map, constants,
   and build/test notes. This is the primary context file.
2. **`readme.md`** — user-facing feature overview and settings.
3. Browse the root directory for the source files listed in `CLAUDE.md`.

## When making changes

1. Read `CLAUDE.md` for the flow and which file owns the behavior you are changing.
2. Follow the established patterns: slot tracking (`SlotType` + `SlotIndex` + `IsFavorited`), position-keyed
   storage in `GravestoneChestSystem.GravestoneStorages`, ServerSide config for anything that edits world tiles.
3. Build with `dotnet build`. Tile behavior (placement, relocation, bubble walls, signs) must be verified in-game.
4. Keep `readme.md`, `description.txt`, `description_workshop.txt`, `CLAUDE.md`, and `Localization/*.hjson` in sync
   for significant features.

## tModLoader references

- Docs: https://docs.tmodloader.net/docs/stable/
- Wiki: https://github.com/tModLoader/tModLoader/wiki
