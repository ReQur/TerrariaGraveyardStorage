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
  - Configurable item placement behavior (see Settings below)
  - If inventory is full, remaining items drop on the ground
- **Configurable Item Placement**: Choose how items are restored to your inventory via mod settings
- **Multiplayer Ownership**: Tombstones track who died, affecting item restoration behavior
- **Slot Position Memory**: The mod remembers exactly where each item was (armor slots, accessory slots, inventory position, etc.)
- **Difficulty-Aware**: Only works on Mediumcore and Hardcore difficulties
  - On Softcore/Journey mode, items don't drop anyway, so the mod doesn't interfere
- **Secure Storage**: Items are safely stored in the gravestone until you retrieve them
- **World Save Support**: Gravestone contents and slot positions persist when saving/loading the world
- **Guaranteed Gravestone Placement** (optional): Prevents the gravestone from breaking on its own, especially when you die in or near lava (see Settings)

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

Every item is stored — the gravestone storage has no slot limit.

## Settings

Access mod settings via: Main Menu → Workshop → Manage Mods → Graveyard Storage → Config

### Item Placement Mode

Controls how items are placed in your inventory when restored from a tombstone:

| Mode | Description |
|------|-------------|
| **Replace Starred Items** (Default) | Only favorited (starred) items will replace items in their original slots. Other items go to empty slots or are dropped. |
| **Replace All Items** | All items will try to replace items in their original slots. Displaced items are moved to empty slots or dropped. |
| **No Replacement** | Items only go to empty slots or are dropped. No items will be displaced from their current positions. |

**Note**: When another player picks up items from your tombstone (multiplayer), "No Replacement" mode is always used regardless of their settings. This prevents your death from reorganizing another player's inventory.

### Guaranteed Gravestone Placement (Server settings)

By default the mod relies on Terraria's own gravestone, which can break on its own in some situations — most notably when you die in or near lava, where the gravestone may never appear and your items are lost.

Enable **Guarantee Gravestone Placement** (off by default) to make the gravestone reliable:

| Option | Description |
|--------|-------------|
| **Guarantee Gravestone Placement** | Places a temporary stone floor under the gravestone so it always has something to sit on, moves it up out of any liquid onto the nearest safe spot, and (if still in liquid) walls it off with bubble blocks. |
| **Use Bubble Blocks In Liquid** (Default: on) | Whether to wall off nearby liquid with temporary, walk-through bubble blocks. Disable to keep only the stone floor + relocation behavior. |

All temporary blocks are removed automatically once you retrieve your items or the gravestone is destroyed, and only tiles the mod itself placed are ever removed — your own builds are never touched. These are **server-side** settings (the server is authoritative over world tiles in multiplayer).

## How It Works

1. Player dies on Mediumcore or Hardcore difficulty
2. Mod intercepts items BEFORE they drop and saves them (including their original slot positions and favorite status)
3. Vanilla Terraria places a gravestone as normal
4. After player respawns, mod finds the nearest new gravestone
5. Stores all saved items internally linked to that gravestone (with owner name)
6. Player can return and right-click the gravestone to open the normal tombstone interface
7. A "Get Items" button appears showing the number of stored items
8. Click the button to restore all items to their original inventory positions (respecting placement mode settings)

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
| `GraveyardStorageConfig.cs` | Mod configuration (client-side item placement mode + server-side gravestone safety) |
| `GraveyardStoragePlayer.cs` | Handles death, saves items with slot positions and favorite status, transfers to gravestones |
| `GraveyardStorageSystem.cs` | Read-only logic, hooks for blocking deposits |
| `GravestoneChestSystem.cs` | Tracks gravestone associations, slot data, temporary blocks, owner tracking, item restoration, world save/load |
| `GravestoneReinforcer.cs` | Guaranteed placement: relocation, temporary stone floor and bubble walls, sign moving |
| `GraveyardStorageUI.cs` | "Get Items" button UI in the gravestone chest interface |
| `VanillaGravestoneGlobalTile.cs` | Adds right-click chest functionality to vanilla gravestones |

## Known Limitations

- Only works in single-player or as server (multiplayer client defers to server)

## License

This mod is free to use and modify.

## Credits

Created for the Terraria modding community.

---

**Note**: This mod does not affect Softcore/Journey mode gameplay, as items already don't drop in that difficulty mode.
