# Graveyard Storage — Project Context

tModLoader mod. On **Mediumcore/Hardcore** death, all of the player's items are stored inside the vanilla
gravestone that spawns at the death location instead of dropping on the ground. Returning to the gravestone and
right-clicking it opens the normal tombstone (sign) interface, with an added **"Get Items"** button that restores
every item to its original inventory slot.

- Built for **tModLoader 1.4.4+** / Terraria 1.4.4+, .NET 8.
- No effect on Softcore/Journey (items don't drop there anyway).

## Architecture / Core Flow

1. **Death** — `GraveyardStoragePlayer.PreKill`
   - Runs before items drop. Skips Softcore (`Player.difficulty == 0`) and multiplayer *clients* (server handles it).
   - `SavePlayerItems()` clones every item with its `SlotType` + `SlotIndex` + `IsFavorited`, then `TurnToAir()`s the originals.
   - Sets `pendingItemTransfer = true`. Vanilla still spawns the gravestone as normal.

2. **Attach to gravestone** — `GraveyardStoragePlayer.PreUpdate` → `TransferItemsToNearestGravestone`
   - After respawn + 10 frames, searches a 30-tile radius around the death position for a `TileID.Tombstones` tile
     that has no storage yet.
   - **Guaranteed placement (optional, see below):** reinforces / relocates the found gravestone, or builds a new
     one if none was found.
   - Registers a `GravestoneStorage` in `GravestoneChestSystem.GravestoneStorages` (keyed by the 2x2 origin) and
     copies all saved items into it. If nothing usable is found and the option is off, items drop on the ground.

3. **Interaction** — `VanillaGravestoneGlobalTile.RightClick`
   - Lets the vanilla sign UI open; records `CurrentGravestonePosition` so the overlay can detect it.

4. **UI overlay** — `GraveyardStorageUI`
   - When a gravestone with items is open, draws item count + a "Get Items" button under the sign UI.

5. **Restore** — `GravestoneChestSystem.RestoreItemsFromStorage`
   - Placement respects `ItemPlacementMode` (`ReplaceStarred` default / `ReplaceAll` / `NoReplacement`).
   - Non-owners in multiplayer are forced to `NoReplacement`.
   - Removes temporary blocks + the storage entry afterwards.

6. **Destruction** — `VanillaGravestoneGlobalTile.KillTile` → `GravestoneChestSystem.OnGravestoneKilled`
   - Restores items to the breaking player (SP: local player; server: nearest player within ~10 tiles), else drops them.
   - Removes temporary blocks.

## Guaranteed Gravestone Placement (gravestone-safety feature)

Optional, **off by default**. Solves the "gravestone breaks / items lost, especially in or near lava" problem.
Controlled by `GraveyardStorageServerConfig` (ServerSide — the server is authoritative over world tiles in MP):
- `GuaranteeGravestonePlacement` (bool)
- `UseBubbleBlocksInLiquid` (bool)

**Approach: the mod owns the gravestone.** We do NOT reuse Terraria's gravestone when the option is on. The vanilla
gravestone starts as a projectile that rolls, flips and sticks near obstacles (trees, slopes) and frequently never
becomes a usable tile — reusing it caused duplicate gravestones and lost items. Instead the death flow
(`GraveyardStoragePlayer.UpdateGuaranteedPlacement`):
1. **Suppresses the vanilla gravestone** — each frame after death, deactivates nearby tombstone **projectiles**
   (`aiStyle == 17`) within `GravestoneSearchRadius` of the death spot (`KillNearbyVanillaGravestoneProjectiles`),
   and removes any stray empty vanilla tombstone tiles that already settled (`RemoveStrayVanillaGravestonesNearDeath`,
   never touches gravestones that already hold storage).
2. **Places our own** via `GravestoneReinforcer.PlaceNew`: `FindSafeSpotNear` searches **sideways** column by column
   (so a tree trunk directly above the death spot doesn't shoot the grave up into the canopy), `SettleDown` drops each
   candidate onto the ground — descending through air and *cuttable foliage* (grass/plants) but stopping on solid
   ground / liquid / obstacles — and `IsCleanSpot` rejects any spot whose support row is a non-solid tile
   (furniture/chest). A temporary **stone** floor is placed only into empty space (never overwriting a tile), and
   liquid still touching the grave is walled off with temporary **bubble blocks**.
3. **Last resort** `GravestoneReinforcer.ForcePlace`: if no open spot exists anywhere nearby (player entombed in solid
   blocks), forcibly carve the 2x2 at the death spot and place the grave there. Guarantees items are never dropped.
4. **Moves the on-map death marker** (`Player.lastDeathPostion`) onto the gravestone so it guides the player to their
   items, not the exact death spot. (Single-player; the marker is client-side so it is not moved for MP clients.)

Tiles are placed by setting the block layer only (`Tile.ClearTile`, not `ClearEverything`), so **background walls are
preserved** — the grave doesn't punch a hole in cave walls. `PlaceSupport`/`WallIfLiquid` only ever fill empty tiles.

When the option is **off**, behavior is the original: attach items to the vanilla gravestone once it settles
(`TryAttachToVanillaGravestone`, windowed search up to `TransferMaxWaitFrames`), or drop them on timeout. No tiles or
projectiles are touched.

**Temporary blocks**: every stone/bubble tile added is recorded in `GravestoneStorage.TempBlocks`, persisted in world
save data, and removed on item retrieval / gravestone destruction (`GravestoneChestSystem.RemoveTempBlocks`). Removal
only touches tiles that still match the type we placed, so player-built blocks are never destroyed. On retrieval, if
the grave was standing on temporary support it is removed too (`ClearGravestoneAt`) so it doesn't hang in the air.

**Diagnostics**: the death flow logs to `Mod.Logger` (search `[GraveyardStorage]` in `client.log`) at every decision
point — death, projectile/tile suppression, placement, drop, and gravestone destruction.

## Files

| File | Purpose |
|------|---------|
| `GraveyardStorage.cs` | Mod entry class (empty). |
| `GraveyardStorageConfig.cs` | `GraveyardStorageConfig` (ClientSide, item placement) + `GraveyardStorageServerConfig` (ServerSide, gravestone safety). |
| `GraveyardStoragePlayer.cs` | Death handling, item saving with slot data, transfer to gravestone. |
| `GraveyardStorageSystem.cs` | Tracks whether the player is viewing a gravestone with items. |
| `GravestoneChestSystem.cs` | `GravestoneStorage`, `TempBlock`, persistence, restoration, temp-block cleanup, owner tracking. |
| `GravestoneReinforcer.cs` | Guaranteed placement: `PlaceNew` — find dry pocket, settle to ground, stone floor, bubble walls. |
| `GraveyardStorageUI.cs` | "Get Items" button overlay. |
| `VanillaGravestoneGlobalTile.cs` | Right-click tracking + destruction handling on vanilla tombstones. |

## Key constants / facts

- Gravestone search radius: 50 tiles (`GravestoneSearchRadius`). Initial delay after death: 10 frames
  (`TransferInitialDelay`). Off-mode windowed search timeout: 600 frames / ~10s (`TransferMaxWaitFrames`).
- Guaranteed placement upward search cap: 80 tiles (`GravestoneReinforcer.MaxUpwardSearch`); settle-down drop cap: 40.
- Vanilla tombstone projectiles use `aiStyle == 17` (used to suppress them in guaranteed mode).
- Tombstones are `TileID.Tombstones`, 2x2. Frames: `frameX = style*36 + dx*18`, `frameY = dy*18`; origin math in
  `GravestoneChestSystem.FindGravestoneOrigin`.
- Difficulty: 0 = Softcore, 1 = Mediumcore, 2 = Hardcore.

## Build & test

- Build: `dotnet build` (uses `..\tModLoader.targets` → the local tModLoader install's `tMLMod.targets`).
- The `WARN: Image loading failed: unknown image type` line from the packager is about the icon asset and is harmless.
- **In-game testing is required for tile behavior** (relocation, support, bubble walls, sign) — it cannot be verified
  from a headless build. Test on Mediumcore: die on solid ground, over a pit, in water, and in/near lava, with the
  option on and off; verify the gravestone survives, items retrieve, and temp blocks disappear afterward.

## Docs to keep in sync

When adding significant features, update `readme.md`, `description.txt`, `description_workshop.txt`, and this file.
Localization lives in `Localization/*_Mods.GraveyardStorage.hjson` (en-US is the source of truth; other locales fall
back to it).
