using System.Collections.Generic;
using System.IO;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace GraveyardStorage
{
    /// <summary>
    /// Stores slot metadata for items in a gravestone chest (for restoration).
    /// </summary>
    public class ChestSlotData
    {
        public SlotType SlotType { get; set; }
        public int SlotIndex { get; set; }

        public ChestSlotData(SlotType slotType, int slotIndex)
        {
            SlotType = slotType;
            SlotIndex = slotIndex;
        }
    }

    /// <summary>
    /// System that tracks which vanilla gravestones have associated chests.
    /// Persists data with world save/load.
    /// </summary>
    public class GravestoneChestSystem : ModSystem
    {
        // Dictionary mapping gravestone tile position to chest ID
        // Key: Point (x, y) of the top-left corner of the gravestone
        // Value: Chest ID
        public static Dictionary<Point, int> GravestoneChests { get; private set; } = new Dictionary<Point, int>();
        
        // Dictionary mapping gravestone position to slot data for each chest slot
        // Key: Point (gravestone origin), Value: array of ChestSlotData (40 slots)
        public static Dictionary<Point, ChestSlotData[]> GravestoneSlotData { get; private set; } = new Dictionary<Point, ChestSlotData[]>();

        public override void OnWorldLoad()
        {
            GravestoneChests = new Dictionary<Point, int>();
            GravestoneSlotData = new Dictionary<Point, ChestSlotData[]>();
        }

        public override void OnWorldUnload()
        {
            GravestoneChests.Clear();
            GravestoneSlotData.Clear();
        }

        public override void SaveWorldData(TagCompound tag)
        {
            // Save gravestone-chest associations
            var positions = new List<int>();
            var chestIds = new List<int>();
            var slotTypes = new List<int>();
            var slotIndices = new List<int>();

            foreach (var kvp in GravestoneChests)
            {
                // Verify the chest still exists
                if (kvp.Value >= 0 && kvp.Value < Main.maxChests && Main.chest[kvp.Value] != null)
                {
                    positions.Add(kvp.Key.X);
                    positions.Add(kvp.Key.Y);
                    chestIds.Add(kvp.Value);

                    // Save slot data for this chest
                    if (GravestoneSlotData.TryGetValue(kvp.Key, out ChestSlotData[] slotData))
                    {
                        for (int i = 0; i < 40; i++)
                        {
                            if (slotData[i] != null)
                            {
                                slotTypes.Add((int)slotData[i].SlotType);
                                slotIndices.Add(slotData[i].SlotIndex);
                            }
                            else
                            {
                                slotTypes.Add(-1);
                                slotIndices.Add(-1);
                            }
                        }
                    }
                    else
                    {
                        // No slot data - fill with -1
                        for (int i = 0; i < 40; i++)
                        {
                            slotTypes.Add(-1);
                            slotIndices.Add(-1);
                        }
                    }
                }
            }

            tag["gravestonePositions"] = positions;
            tag["gravestoneChestIds"] = chestIds;
            tag["slotTypes"] = slotTypes;
            tag["slotIndices"] = slotIndices;
        }

        public override void LoadWorldData(TagCompound tag)
        {
            GravestoneChests.Clear();
            GravestoneSlotData.Clear();

            if (tag.ContainsKey("gravestonePositions") && tag.ContainsKey("gravestoneChestIds"))
            {
                var positions = tag.GetList<int>("gravestonePositions");
                var chestIds = tag.GetList<int>("gravestoneChestIds");
                var slotTypes = tag.ContainsKey("slotTypes") ? tag.GetList<int>("slotTypes") : null;
                var slotIndices = tag.ContainsKey("slotIndices") ? tag.GetList<int>("slotIndices") : null;

                for (int i = 0; i < chestIds.Count && i * 2 + 1 < positions.Count; i++)
                {
                    int x = positions[i * 2];
                    int y = positions[i * 2 + 1];
                    int chestId = chestIds[i];
                    Point key = new Point(x, y);

                    // Verify the chest exists
                    if (chestId >= 0 && chestId < Main.maxChests && Main.chest[chestId] != null)
                    {
                        GravestoneChests[key] = chestId;

                        // Load slot data if available
                        if (slotTypes != null && slotIndices != null)
                        {
                            ChestSlotData[] slotData = new ChestSlotData[40];
                            int baseIndex = i * 40;
                            for (int s = 0; s < 40 && baseIndex + s < slotTypes.Count; s++)
                            {
                                int slotType = slotTypes[baseIndex + s];
                                int slotIndex = slotIndices[baseIndex + s];
                                if (slotType >= 0)
                                {
                                    slotData[s] = new ChestSlotData((SlotType)slotType, slotIndex);
                                }
                            }
                            GravestoneSlotData[key] = slotData;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Registers a gravestone position with an associated chest.
        /// </summary>
        public static void RegisterGravestoneChest(int tileX, int tileY, int chestId)
        {
            Point key = new Point(tileX, tileY);
            GravestoneChests[key] = chestId;
        }

        /// <summary>
        /// Gets the chest ID for a gravestone at the given position, or -1 if none.
        /// </summary>
        public static int GetChestForGravestone(int tileX, int tileY)
        {
            // Find the top-left corner of the gravestone (they can be multi-tile)
            Point topLeft = FindGravestoneOrigin(tileX, tileY);
            
            if (GravestoneChests.TryGetValue(topLeft, out int chestId))
            {
                // Verify chest still exists
                if (chestId >= 0 && chestId < Main.maxChests && Main.chest[chestId] != null)
                {
                    return chestId;
                }
                else
                {
                    // Chest was destroyed, remove the association
                    GravestoneChests.Remove(topLeft);
                }
            }

            return -1;
        }

        /// <summary>
        /// Checks if a gravestone at the given position has an associated chest.
        /// </summary>
        public static bool HasChest(int tileX, int tileY)
        {
            return GetChestForGravestone(tileX, tileY) >= 0;
        }

        /// <summary>
        /// Finds the origin (top-left) position of a gravestone tile.
        /// Vanilla gravestones are 2x2 tiles.
        /// </summary>
        public static Point FindGravestoneOrigin(int i, int j)
        {
            Tile tile = Main.tile[i, j];
            if (!tile.HasTile || tile.TileType != TileID.Tombstones)
                return new Point(i, j);

            // Vanilla tombstones are 2x2, with frame coordinates indicating position
            // Each sub-tile is 18 pixels
            int frameX = tile.TileFrameX;
            int frameY = tile.TileFrameY;

            // Style width for tombstones (each style is 2 tiles * 18 pixels = 36)
            int styleWidth = 36;
            int localFrameX = frameX % styleWidth;

            // Calculate offset from origin
            int offsetX = localFrameX / 18;
            int offsetY = frameY / 18;

            return new Point(i - offsetX, j - offsetY);
        }

        /// <summary>
        /// Removes the chest association when a gravestone is destroyed.
        /// </summary>
        public static void OnGravestoneKilled(int tileX, int tileY)
        {
            Point origin = FindGravestoneOrigin(tileX, tileY);
            
            if (GravestoneChests.TryGetValue(origin, out int chestId))
            {
                // Drop items from the chest
                if (chestId >= 0 && chestId < Main.maxChests && Main.chest[chestId] != null)
                {
                    Chest chest = Main.chest[chestId];
                    for (int i = 0; i < 40; i++)
                    {
                        if (chest.item[i] != null && !chest.item[i].IsAir)
                        {
                            int itemIndex = Item.NewItem(
                                new Terraria.DataStructures.EntitySource_TileBreak(origin.X, origin.Y),
                                origin.X * 16, origin.Y * 16, 32, 32,
                                chest.item[i].type, chest.item[i].stack,
                                false, chest.item[i].prefix);
                            
                            if (Main.netMode == NetmodeID.Server && itemIndex >= 0)
                            {
                                NetMessage.SendData(MessageID.SyncItem, -1, -1, null, itemIndex);
                            }
                        }
                    }

                    // Destroy the chest
                    Chest.DestroyChest(chest.x, chest.y);
                }

                GravestoneChests.Remove(origin);
                GravestoneSlotData.Remove(origin);
            }
        }

        /// <summary>
        /// Stores slot data for a gravestone chest.
        /// </summary>
        public static void StoreSlotData(Point gravestoneOrigin, List<SavedItemData> itemsWithSlots)
        {
            ChestSlotData[] slotData = new ChestSlotData[40];
            for (int i = 0; i < itemsWithSlots.Count && i < 40; i++)
            {
                slotData[i] = new ChestSlotData(itemsWithSlots[i].SlotType, itemsWithSlots[i].SlotIndex);
            }
            GravestoneSlotData[gravestoneOrigin] = slotData;
        }

        /// <summary>
        /// Gets the slot data for a gravestone chest.
        /// </summary>
        public static ChestSlotData[] GetSlotData(int chestId)
        {
            foreach (var kvp in GravestoneChests)
            {
                if (kvp.Value == chestId)
                {
                    if (GravestoneSlotData.TryGetValue(kvp.Key, out ChestSlotData[] slotData))
                    {
                        return slotData;
                    }
                    break;
                }
            }
            return null;
        }

        /// <summary>
        /// Gets the gravestone origin point for a given chest ID.
        /// </summary>
        public static Point? GetGravestoneOrigin(int chestId)
        {
            foreach (var kvp in GravestoneChests)
            {
                if (kvp.Value == chestId)
                {
                    return kvp.Key;
                }
            }
            return null;
        }

        /// <summary>
        /// Gets the number of items stored in a gravestone chest.
        /// </summary>
        public static int GetItemCount(int chestId)
        {
            if (chestId < 0 || chestId >= Main.maxChests || Main.chest[chestId] == null)
                return 0;

            Chest chest = Main.chest[chestId];
            int count = 0;
            for (int i = 0; i < 40; i++)
            {
                if (chest.item[i] != null && !chest.item[i].IsAir)
                {
                    count++;
                }
            }
            return count;
        }

        /// <summary>
        /// Restores all items from a gravestone chest to the player's inventory.
        /// Items are placed in their original slots if possible, otherwise in free slots, otherwise dropped.
        /// </summary>
        public static void RestoreItemsToPlayer(Player player, int chestId)
        {
            if (chestId < 0 || chestId >= Main.maxChests || Main.chest[chestId] == null)
                return;

            Chest chest = Main.chest[chestId];
            ChestSlotData[] slotData = GetSlotData(chestId);
            Point? origin = GetGravestoneOrigin(chestId);

            List<Item> itemsToProcess = new List<Item>();
            List<ChestSlotData> slotsToProcess = new List<ChestSlotData>();

            // Collect all items from the chest
            for (int i = 0; i < 40; i++)
            {
                if (chest.item[i] != null && !chest.item[i].IsAir)
                {
                    itemsToProcess.Add(chest.item[i].Clone());
                    slotsToProcess.Add(slotData != null && slotData[i] != null ? slotData[i] : null);
                    chest.item[i].TurnToAir();
                }
            }

            List<Item> remainingItems = new List<Item>();

            // First pass: try to place items in their original slots
            for (int i = 0; i < itemsToProcess.Count; i++)
            {
                Item item = itemsToProcess[i];
                ChestSlotData slot = slotsToProcess[i];

                if (slot != null && TryPlaceInOriginalSlot(player, item, slot))
                {
                    continue; // Item placed successfully
                }

                // Item couldn't be placed in original slot
                remainingItems.Add(item);
            }

            // Second pass: try to place remaining items in any free slot
            List<Item> droppedItems = new List<Item>();
            foreach (var item in remainingItems)
            {
                if (!TryPlaceInFreeSlot(player, item))
                {
                    droppedItems.Add(item);
                }
            }

            // Third pass: drop items that couldn't be placed
            foreach (var item in droppedItems)
            {
                Item.NewItem(
                    player.GetSource_Misc("GraveyardStorage"),
                    player.Center, Vector2.Zero,
                    item.type, item.stack,
                    false, item.prefix);
            }

            // Clean up: remove the gravestone chest association since items are now retrieved
            if (origin.HasValue)
            {
                GravestoneChests.Remove(origin.Value);
                GravestoneSlotData.Remove(origin.Value);
                
                // Destroy the internal chest (items already retrieved)
                Chest.DestroyChest(chest.x, chest.y);
            }
        }

        /// <summary>
        /// Tries to place an item in its original slot.
        /// </summary>
        private static bool TryPlaceInOriginalSlot(Player player, Item item, ChestSlotData slot)
        {
            Item[] targetArray = null;
            int targetIndex = slot.SlotIndex;

            switch (slot.SlotType)
            {
                case SlotType.Inventory:
                    if (targetIndex >= 0 && targetIndex < 50)
                        targetArray = player.inventory;
                    break;
                case SlotType.Coins:
                    targetIndex += 50; // Coins are at slots 50-53
                    if (targetIndex >= 50 && targetIndex < 54)
                        targetArray = player.inventory;
                    break;
                case SlotType.Ammo:
                    targetIndex += 54; // Ammo are at slots 54-57
                    if (targetIndex >= 54 && targetIndex < 58)
                        targetArray = player.inventory;
                    break;
                case SlotType.Armor:
                    if (targetIndex >= 0 && targetIndex < player.armor.Length)
                        targetArray = player.armor;
                    break;
                case SlotType.Dye:
                    if (targetIndex >= 0 && targetIndex < player.dye.Length)
                        targetArray = player.dye;
                    break;
                case SlotType.MiscEquip:
                    if (targetIndex >= 0 && targetIndex < player.miscEquips.Length)
                        targetArray = player.miscEquips;
                    break;
                case SlotType.MiscDye:
                    if (targetIndex >= 0 && targetIndex < player.miscDyes.Length)
                        targetArray = player.miscDyes;
                    break;
            }

            if (targetArray == null)
                return false;

            // Check if the slot is empty
            if (targetArray[targetIndex].IsAir)
            {
                targetArray[targetIndex] = item.Clone();
                return true;
            }

            return false;
        }

        /// <summary>
        /// Tries to place an item in any free inventory slot.
        /// </summary>
        private static bool TryPlaceInFreeSlot(Player player, Item item)
        {
            // Try main inventory first (slots 0-49)
            for (int i = 0; i < 50; i++)
            {
                if (player.inventory[i].IsAir)
                {
                    player.inventory[i] = item.Clone();
                    return true;
                }
            }

            // Try coins (slots 50-53) if item is a coin
            if (item.IsACoin)
            {
                for (int i = 50; i < 54; i++)
                {
                    if (player.inventory[i].IsAir)
                    {
                        player.inventory[i] = item.Clone();
                        return true;
                    }
                }
            }

            // Try ammo (slots 54-57) if item is ammo
            if (item.ammo > 0)
            {
                for (int i = 54; i < 58; i++)
                {
                    if (player.inventory[i].IsAir)
                    {
                        player.inventory[i] = item.Clone();
                        return true;
                    }
                }
            }

            return false;
        }
    }
}
