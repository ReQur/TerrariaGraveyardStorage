# Graveyard Storage

A Terraria tModLoader mod that stores your items in vanilla gravestones when you die, instead of dropping them on the ground.

## Description

When playing on Mediumcore or Hardcore difficulty, dying in Terraria means your items scatter across the ground, making recovery difficult and sometimes impossible. This mod solves that problem by automatically storing all your items in the vanilla gravestone that spawns at your death location.

## Features

- **Automatic Item Storage**: When you die, all items are automatically stored in the vanilla gravestone
- **Uses Vanilla Gravestones**: No custom tiles - works with the standard Terraria tombstones
- **Vanilla Sign Interface**: Right-click the gravestone to open the normal tombstone interface with your items info
- **"Get Items" Button**: One-click button in the tombstone interface to restore all items to their original inventory slots
  - Items are placed in their original positions (armor, accessories, hotbar, etc.)
  - If a slot is occupied, items go to a free inventory slot instead
  - If inventory is full, remaining items drop on the ground
- **Slot Position Memory**: The mod remembers exactly where each item was (armor slots, accessory slots, inventory position, etc.)
- **Difficulty-Aware**: Only works on Mediumcore and Hardcore difficulties
  - On Softcore/Journey mode, items don't drop anyway, so the mod doesn't interfere
- **40 Item Slots**: Each gravestone can hold up to 40 items
- **Secure Storage**: Items are safely stored in the gravestone until you retrieve them
- **World Save Support**: Gravestone contents and slot positions persist when saving/loading the world

## What Gets Stored

The mod stores (in priority order):
- Armor (head, body, legs)
- Accessories
- Vanity armor and accessories
- Dyes
- Main inventory items (slots 0-49)
- Coins (4 slots)
- Ammunition (4 slots)
- Misc equipment (hook, mount, pet, light pet, minecart)
- Misc dyes

If you have more than 40 items when you die, excess items will drop normally on the ground.

## How It Works

1. Player dies on Mediumcore or Hardcore difficulty
2. Mod intercepts items BEFORE they drop and saves them (including their original slot positions)
3. Vanilla Terraria places a gravestone as normal
4. After player respawns, mod finds the nearest new gravestone
5. Stores all saved items internally linked to that gravestone
6. Player can return and right-click the gravestone to open the normal tombstone interface
7. A "Get Items" button appears showing the number of stored items
8. Click the button to restore all items to their original inventory positions

## Installation

1. Subscribe to this mod in the tModLoader Workshop, or
2. Download and place in your tModLoader Mods folder
3. Enable the mod in-game
4. Reload mods

## Technical Details

- Built for tModLoader 1.4.4+
- Compatible with Terraria 1.4.4+
- Uses vanilla TileID.Tombstones with custom chest association
- Gravestone-chest associations saved with world data
- Uses MonoMod hooks to intercept item slot interactions for read-only functionality

## File Structure

| File | Purpose |
|------|---------|
| `GraveyardStorage.cs` | Main mod class |
| `GraveyardStoragePlayer.cs` | Handles death, saves items with slot positions, transfers to gravestones |
| `GraveyardStorageSystem.cs` | Read-only logic, hooks for blocking deposits |
| `GravestoneChestSystem.cs` | Tracks gravestone-chest associations, slot data, item restoration, world save/load |
| `GraveyardStorageUI.cs` | "Get Items" button UI in the gravestone chest interface |
| `VanillaGravestoneGlobalTile.cs` | Adds right-click chest functionality to vanilla gravestones |

## Known Limitations

- Maximum 40 items per gravestone (excess drops on ground)
- Only works in single-player or as server (multiplayer client defers to server)

## License

This mod is free to use and modify.

## Credits

Created for the Terraria modding community.

---

**Note**: This mod does not affect Softcore/Journey mode gameplay, as items already don't drop in that difficulty mode.
