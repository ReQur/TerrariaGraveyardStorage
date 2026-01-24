# Graveyard Storage - AI Context File

This file provides context for AI assistants working on this tModLoader mod.

## Project Overview

**Purpose**: Store player items in vanilla gravestones when dying on Mediumcore/Hardcore, with read-only access (can take items, cannot deposit). Includes a "Get Items" button to restore items to their original inventory slots.

**Status**: Working implementation with slot position restoration feature.

## Architecture

### Core Flow

1. **Death Detection** (`GraveyardStoragePlayer.PreKill`)
   - Intercepts death BEFORE items drop
   - Saves all items to `savedItemsWithSlots` list (with `SlotType` and `SlotIndex`)
   - Clears player inventory with `TurnToAir()`
   - Sets `pendingItemTransfer = true`

2. **Gravestone Association** (`GraveyardStoragePlayer.PreUpdate`)
   - After respawn, waits 10 frames
   - Searches for nearest vanilla gravestone (TileID.Tombstones) near death position
   - Creates internal storage chest (for item management)
   - Registers in `GravestoneChestSystem.GravestoneChests` dictionary
   - Stores slot data in `GravestoneChestSystem.GravestoneSlotData`

3. **Tombstone Interaction** (`VanillaGravestoneGlobalTile.RightClick`)
   - Tracks when player opens a gravestone with stored items
   - Lets vanilla sign interface open normally
   - Sets `CurrentGravestonePosition` for UI to detect

4. **UI Overlay** (`GraveyardStorageUI`)
   - Detects when sign interface is open for a gravestone with items
   - Shows item count and "Get Items" button below the sign interface
   - Positioned relative to screen center (where sign UI appears)

5. **Item Restoration** (`GravestoneChestSystem.RestoreItemsToPlayer`)
   - On button click, restores items to original slots (if empty), then free slots, then drops
   - Cleans up internal storage and gravestone association after restoration

### Key Technical Details

- **Vanilla sign interface**: Tombstones use normal sign UI, mod adds overlay button
- **Internal chest for storage**: Items stored in Chest object but not opened as chest UI
- **World persistence**: `GravestoneChestSystem` implements `SaveWorldData`/`LoadWorldData`
- **Slot tracking**: `SlotType` enum (Inventory, Coins, Ammo, Armor, Dye, MiscEquip, MiscDye) + `SlotIndex`
- **Slot data storage**: `GravestoneSlotData` dictionary maps gravestone origin to `ChestSlotData[]`
- **UI detection**: Tracks `CurrentGravestonePosition` when player right-clicks gravestone with items

## File Responsibilities

| File | Key Methods | Purpose |
|------|-------------|---------|
| `GraveyardStoragePlayer.cs` | `PreKill`, `PreUpdate`, `SavePlayerItems`, `TransferItemsToNearestGravestone` | Death handling, item saving with slot positions |
| `GraveyardStorageSystem.cs` | `IsViewingGravestoneWithItems`, `GetCurrentGravestoneChestId` | Tracks when player views gravestone with items |
| `GravestoneChestSystem.cs` | `RegisterGravestoneChest`, `GetChestForGravestone`, `StoreSlotData`, `RestoreItemsToPlayer`, `SaveWorldData` | Item storage, slot data, item restoration, persistence |
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

- Max item slots: 40
- Search radius for gravestone: 30 tiles
- Transfer delay after respawn: 10 frames
- Player difficulty: 0=Softcore, 1=Mediumcore, 2=Hardcore

## Dependencies

- tModLoader 1.4.4+
- No external mod dependencies
