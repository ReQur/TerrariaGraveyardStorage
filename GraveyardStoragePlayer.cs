using Microsoft.Xna.Framework;
using System.Collections.Generic;
using Terraria;
using Terraria.ID;
using Terraria.DataStructures;
using Terraria.ModLoader;

namespace GraveyardStorage
{
    /// <summary>
    /// Represents the type of inventory slot an item was stored in.
    /// </summary>
    public enum SlotType
    {
        Inventory,      // Main inventory (0-49)
        Coins,          // Coins (50-53)
        Ammo,           // Ammo (54-57)
        Armor,          // Armor and accessories
        Dye,            // Dye slots
        MiscEquip,      // Misc equipment (hook, mount, pet, etc.)
        MiscDye         // Misc dye slots
    }

    /// <summary>
    /// Stores an item along with its original slot position for restoration.
    /// </summary>
    public class SavedItemData
    {
        public Item Item { get; set; }
        public SlotType SlotType { get; set; }
        public int SlotIndex { get; set; }

        public SavedItemData(Item item, SlotType slotType, int slotIndex)
        {
            Item = item;
            SlotType = slotType;
            SlotIndex = slotIndex;
        }
    }

    public class GraveyardStoragePlayer : ModPlayer
    {
        // Track death state for item transfer
        private bool pendingItemTransfer = false;
        private Point deathPosition;
        private List<SavedItemData> savedItemsWithSlots = new List<SavedItemData>();
        private int transferDelayFrames = 0;

        public override void PreUpdate()
        {
            // Check if we have pending items to transfer to a gravestone
            if (pendingItemTransfer && !Player.dead)
            {
                // Wait a few frames to ensure gravestone is placed
                if (transferDelayFrames < 10)
                {
                    transferDelayFrames++;
                    return;
                }

                // Player has respawned - find the gravestone and attach the chest
                TransferItemsToNearestGravestone();
                pendingItemTransfer = false;
                transferDelayFrames = 0;
            }
        }

        /// <summary>
        /// Called BEFORE the player dies. Saves items before they drop.
        /// </summary>
        public override bool PreKill(double damage, int hitDirection, bool pvp, ref bool playSound, ref bool genGore, ref PlayerDeathReason damageSource)
        {
            // In multiplayer client, let server handle this
            if (Main.netMode == NetmodeID.MultiplayerClient)
                return true;

            // Only for Mediumcore and Hardcore
            // difficulty: 0 = Softcore, 1 = Mediumcore, 2 = Hardcore
            if (Player.difficulty == 0)
                return true;

            // Save death position
            deathPosition = Player.Center.ToTileCoordinates();

            // Save all items BEFORE they drop
            SavePlayerItems();

            // Mark that we need to transfer items when we find the gravestone
            pendingItemTransfer = true;

            // Return true to allow the death to proceed normally (gravestone will be placed)
            return true;
        }

        /// <summary>
        /// Saves all player items to temporary storage with their original slot positions.
        /// </summary>
        private void SavePlayerItems()
        {
            savedItemsWithSlots.Clear();

            // Save armor (includes armor, vanity armor, accessories, vanity accessories)
            for (int i = 0; i < Player.armor.Length; i++)
            {
                if (!Player.armor[i].IsAir)
                {
                    savedItemsWithSlots.Add(new SavedItemData(Player.armor[i].Clone(), SlotType.Armor, i));
                    Player.armor[i].TurnToAir();
                }
            }

            // Save dyes
            for (int i = 0; i < Player.dye.Length; i++)
            {
                if (!Player.dye[i].IsAir)
                {
                    savedItemsWithSlots.Add(new SavedItemData(Player.dye[i].Clone(), SlotType.Dye, i));
                    Player.dye[i].TurnToAir();
                }
            }

            // Save main inventory (slots 0-49)
            for (int i = 0; i < 50; i++)
            {
                if (!Player.inventory[i].IsAir)
                {
                    savedItemsWithSlots.Add(new SavedItemData(Player.inventory[i].Clone(), SlotType.Inventory, i));
                    Player.inventory[i].TurnToAir();
                }
            }

            // Save coins (slots 50-53)
            for (int i = 50; i < 54; i++)
            {
                if (!Player.inventory[i].IsAir)
                {
                    savedItemsWithSlots.Add(new SavedItemData(Player.inventory[i].Clone(), SlotType.Coins, i - 50));
                    Player.inventory[i].TurnToAir();
                }
            }

            // Save ammo (slots 54-57)
            for (int i = 54; i < 58; i++)
            {
                if (!Player.inventory[i].IsAir)
                {
                    savedItemsWithSlots.Add(new SavedItemData(Player.inventory[i].Clone(), SlotType.Ammo, i - 54));
                    Player.inventory[i].TurnToAir();
                }
            }

            // Save misc equipment
            for (int i = 0; i < Player.miscEquips.Length; i++)
            {
                if (!Player.miscEquips[i].IsAir)
                {
                    savedItemsWithSlots.Add(new SavedItemData(Player.miscEquips[i].Clone(), SlotType.MiscEquip, i));
                    Player.miscEquips[i].TurnToAir();
                }
            }

            // Save misc dyes
            for (int i = 0; i < Player.miscDyes.Length; i++)
            {
                if (!Player.miscDyes[i].IsAir)
                {
                    savedItemsWithSlots.Add(new SavedItemData(Player.miscDyes[i].Clone(), SlotType.MiscDye, i));
                    Player.miscDyes[i].TurnToAir();
                }
            }
        }

        /// <summary>
        /// Finds the nearest gravestone to death position and creates a chest for it.
        /// </summary>
        private void TransferItemsToNearestGravestone()
        {
            if (savedItemsWithSlots.Count == 0)
                return;

            // Search for a gravestone near the death position
            int searchRadius = 30;
            Point? foundGravestone = null;
            int closestDistance = int.MaxValue;

            for (int x = deathPosition.X - searchRadius; x <= deathPosition.X + searchRadius; x++)
            {
                for (int y = deathPosition.Y - searchRadius; y <= deathPosition.Y + searchRadius; y++)
                {
                    if (x < 0 || x >= Main.maxTilesX || y < 0 || y >= Main.maxTilesY)
                        continue;

                    Tile tile = Main.tile[x, y];
                    if (tile.HasTile && tile.TileType == TileID.Tombstones)
                    {
                        // Check if this gravestone already has a chest
                        Point origin = GravestoneChestSystem.FindGravestoneOrigin(x, y);
                        if (!GravestoneChestSystem.HasChest(origin.X, origin.Y))
                        {
                            int distance = (x - deathPosition.X) * (x - deathPosition.X) + 
                                          (y - deathPosition.Y) * (y - deathPosition.Y);
                            if (distance < closestDistance)
                            {
                                closestDistance = distance;
                                foundGravestone = origin;
                            }
                        }
                    }
                }
            }

            if (foundGravestone.HasValue)
            {
                // Create a chest for this gravestone
                int chestId = CreateChestForGravestone(foundGravestone.Value.X, foundGravestone.Value.Y);
                if (chestId >= 0)
                {
                    TransferSavedItemsToChest(chestId);

                    // Store the slot data for this chest
                    GravestoneChestSystem.StoreSlotData(foundGravestone.Value, savedItemsWithSlots);

                    // Sync in multiplayer
                    if (Main.netMode == NetmodeID.Server)
                    {
                        for (int slot = 0; slot < 40; slot++)
                        {
                            NetMessage.SendData(MessageID.SyncChestItem, -1, -1, null, chestId, slot);
                        }
                    }
                }
            }
            else
            {
                // No gravestone found - drop items on ground
                DropSavedItems();
            }

            savedItemsWithSlots.Clear();
        }

        /// <summary>
        /// Creates a chest associated with a gravestone tile.
        /// We manually create the chest since gravestones aren't container tiles.
        /// </summary>
        private int CreateChestForGravestone(int tileX, int tileY)
        {
            // Find an empty chest slot manually (Chest.CreateChest validates tile type which fails for gravestones)
            int chestId = -1;
            for (int i = 0; i < Main.maxChests; i++)
            {
                if (Main.chest[i] == null)
                {
                    chestId = i;
                    break;
                }
            }

            if (chestId == -1)
                return -1;

            // Create the chest manually
            Main.chest[chestId] = new Chest();
            Main.chest[chestId].x = tileX;
            Main.chest[chestId].y = tileY;
            Main.chest[chestId].name = "";

            // Initialize all item slots
            for (int i = 0; i < 40; i++)
            {
                Main.chest[chestId].item[i] = new Item();
            }

            GravestoneChestSystem.RegisterGravestoneChest(tileX, tileY, chestId);
            return chestId;
        }

        /// <summary>
        /// Transfers saved items to a chest.
        /// </summary>
        private void TransferSavedItemsToChest(int chestId)
        {
            Chest chest = Main.chest[chestId];
            if (chest == null)
                return;

            int slot = 0;
            foreach (var itemData in savedItemsWithSlots)
            {
                if (slot >= 40)
                    break;

                chest.item[slot] = itemData.Item.Clone();
                slot++;
            }

            // If there are more items than chest slots, drop the rest
            for (int i = slot; i < savedItemsWithSlots.Count; i++)
            {
                var item = savedItemsWithSlots[i].Item;
                Item.NewItem(
                    Player.GetSource_Death(),
                    deathPosition.X * 16, deathPosition.Y * 16, 16, 16,
                    item.type, item.stack,
                    false, item.prefix);
            }
        }

        /// <summary>
        /// Drops all saved items on the ground (fallback if no gravestone found).
        /// </summary>
        private void DropSavedItems()
        {
            foreach (var itemData in savedItemsWithSlots)
            {
                var item = itemData.Item;
                Item.NewItem(
                    Player.GetSource_Death(),
                    deathPosition.X * 16, deathPosition.Y * 16, 16, 16,
                    item.type, item.stack,
                    false, item.prefix);
            }
        }
    }
}
