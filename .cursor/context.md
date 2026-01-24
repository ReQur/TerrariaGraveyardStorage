# Graveyard Storage - AI Context File

This file provides context for AI assistants working on this tModLoader mod.

## Project Overview

**Purpose**: Store player items in vanilla gravestones when dying on Mediumcore/Hardcore, with read-only access (can take items, cannot deposit). Includes a "Get Items" button to restore items to their original inventory slots.

**Status**: Working implementation with slot position restoration, configurable item placement modes, and owner tracking for multiplayer.

## Architecture

### Core Flow

1. **Death Detection** (`GraveyardStoragePlayer.PreKill`)
   - Intercepts death BEFORE items drop
   - Saves all items to `savedItemsWithSlots` list (with `SlotType`, `SlotIndex`, and `IsFavorited`)
   - Clears player inventory with `TurnToAir()`
   - Sets `pendingItemTransfer = true`

2. **Gravestone Association** (`GraveyardStoragePlayer.PreUpdate`)
   - After respawn, waits 10 frames
   - Searches for nearest vanilla gravestone (TileID.Tombstones) near death position
   - Creates `GravestoneStorage` object (custom storage, not limited by vanilla 40-slot chest)
   - Registers in `GravestoneChestSystem.GravestoneStorages` dictionary
   - Stores all items with their slot data (SlotType, SlotIndex, IsFavorited)

3. **Tombstone Interaction** (`VanillaGravestoneGlobalTile.RightClick`)
   - Tracks when player opens a gravestone with stored items
   - Lets vanilla sign interface open normally
   - Sets `CurrentGravestonePosition` for UI to detect

4. **UI Overlay** (`GraveyardStorageUI`)
   - Detects when sign interface is open for a gravestone with items
   - Shows item count and "Get Items" button below the sign interface
   - Positioned relative to screen center (where sign UI appears)

5. **Item Restoration** (`GravestoneChestSystem.RestoreItemsToPlayer`)
   - On button click, restores items based on placement mode setting
   - **ReplaceStarred** (default): Only favorited items replace occupied slots, others go to free slots
   - **ReplaceAll**: All items can replace occupied slots (displaced items go to free slots)
   - **NoReplacement**: Items only go to empty slots (used when another player retrieves items)
   - Cleans up internal storage, slot data, and owner after restoration

6. **Tombstone Destruction** (`GravestoneChestSystem.OnGravestoneKilled`)
   - When tombstone is destroyed, finds the player who broke it
   - Restores items to that player's original slots (same logic as "Get Items" button)
   - Uses `FindPlayerBreakingTile()` to identify the breaking player
   - Falls back to dropping items if no player found nearby

### Key Technical Details

- **Vanilla sign interface**: Tombstones use normal sign UI, mod adds overlay button
- **Custom storage system**: Uses `GravestoneStorage` class instead of vanilla Chest (no 40-slot limit)
- **World persistence**: `GravestoneChestSystem` implements `SaveWorldData`/`LoadWorldData` with ItemIO
- **Slot tracking**: `SlotType` enum (Inventory, Coins, Ammo, Armor, Dye, MiscEquip, MiscDye) + `SlotIndex` + `IsFavorited`
- **Storage structure**: `GravestoneStorages` dictionary maps gravestone origin to `GravestoneStorage` object
- **Owner tracking**: `GravestoneStorage.OwnerName` stores player name who died
- **Item placement modes**: `GraveyardStorageConfig` with `ItemPlacementMode` enum (ReplaceStarred, ReplaceAll, NoReplacement)
- **UI detection**: Tracks `CurrentGravestonePosition` when player right-clicks gravestone with items
- **Tombstone destruction**: Items restore to breaking player (single player: LocalPlayer, server: nearest player within 10 tiles)
- **Multiplayer ownership**: Non-owners always use NoReplacement mode to avoid disrupting their inventory

## File Responsibilities

| File | Key Methods | Purpose |
|------|-------------|---------|
| `GraveyardStorageConfig.cs` | `PlacementMode` property | Mod configuration (ItemPlacementMode enum) |
| `GraveyardStoragePlayer.cs` | `PreKill`, `PreUpdate`, `SavePlayerItems`, `TransferItemsToNearestGravestone` | Death handling, item saving with slot positions and favorite status |
| `GraveyardStorageSystem.cs` | `IsViewingGravestoneWithItems`, `GetCurrentGravestoneChestId` | Tracks when player views gravestone with items |
| `GravestoneChestSystem.cs` | `RegisterGravestoneChest`, `GetChestForGravestone`, `StoreSlotData`, `RestoreItemsToPlayer`, `IsOwner`, `OnGravestoneKilled`, `SaveWorldData` | Item storage, slot data, owner tracking, item restoration with placement modes, persistence |
| `GraveyardStorageUI.cs` | `OnGetItemsClicked`, `Update`, `ModifyInterfaceLayers` | "Get Items" button overlay on sign interface |
| `VanillaGravestoneGlobalTile.cs` | `RightClick`, `KillTile` | Tracks gravestone interaction, handles gravestone destruction |

## Common Issues & Solutions

### Items still dropping
- Ensure `PreKill` is used (not `Kill`)
- Items must be cloned THEN cleared with `TurnToAir()`

### Gravestone not found
- Search radius is 30 tiles from death position
- Wait 10 frames after respawn before searching
- Check `TileID.Tombstones` tile type

### Get Items button not appearing
- Check `IsViewingGravestoneWithItems()` returns true
- Verify `CurrentGravestonePosition` is set in `VanillaGravestoneGlobalTile.RightClick`
- Ensure gravestone has items stored (chest exists with items)

## Important Constants

- Max item slots: Unlimited (stores all player items - inventory, armor, accessories, dyes, etc.)
- Search radius for gravestone: 30 tiles
- Transfer delay after respawn: 10 frames
- Player difficulty: 0=Softcore, 1=Mediumcore, 2=Hardcore

## Dependencies

- tModLoader 1.4.4+
- No external mod dependencies
