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
        public bool IsFavorited { get; set; }

        public ChestSlotData(SlotType slotType, int slotIndex, bool isFavorited = false)
        {
            SlotType = slotType;
            SlotIndex = slotIndex;
            IsFavorited = isFavorited;
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

        // Dictionary mapping gravestone position to owner player name
        // Key: Point (gravestone origin), Value: Player name who died
        public static Dictionary<Point, string> GravestoneOwners { get; private set; } = new Dictionary<Point, string>();

        public override void OnWorldLoad()
        {
            GravestoneChests = new Dictionary<Point, int>();
            GravestoneSlotData = new Dictionary<Point, ChestSlotData[]>();
            GravestoneOwners = new Dictionary<Point, string>();
        }

        public override void OnWorldUnload()
        {
            GravestoneChests.Clear();
            GravestoneSlotData.Clear();
            GravestoneOwners.Clear();
        }

        public override void SaveWorldData(TagCompound tag)
        {
            // Save gravestone-chest associations
            var positions = new List<int>();
            var chestIds = new List<int>();
            var slotTypes = new List<int>();
            var slotIndices = new List<int>();
            var slotFavorited = new List<bool>();
            var owners = new List<string>();

            foreach (var kvp in GravestoneChests)
            {
                // Verify the chest still exists
                if (kvp.Value >= 0 && kvp.Value < Main.maxChests && Main.chest[kvp.Value] != null)
                {
                    positions.Add(kvp.Key.X);
                    positions.Add(kvp.Key.Y);
                    chestIds.Add(kvp.Value);

                    // Save owner
                    if (GravestoneOwners.TryGetValue(kvp.Key, out string owner))
                    {
                        owners.Add(owner);
                    }
                    else
                    {
                        owners.Add("");
                    }

                    // Save slot data for this chest
                    if (GravestoneSlotData.TryGetValue(kvp.Key, out ChestSlotData[] slotData))
                    {
                        for (int i = 0; i < 40; i++)
                        {
                            if (slotData[i] != null)
                            {
                                slotTypes.Add((int)slotData[i].SlotType);
                                slotIndices.Add(slotData[i].SlotIndex);
                                slotFavorited.Add(slotData[i].IsFavorited);
                            }
                            else
                            {
                                slotTypes.Add(-1);
                                slotIndices.Add(-1);
                                slotFavorited.Add(false);
                            }
                        }
                    }
                    else
                    {
                        // No slot data - fill with defaults
                        for (int i = 0; i < 40; i++)
                        {
                            slotTypes.Add(-1);
                            slotIndices.Add(-1);
                            slotFavorited.Add(false);
                        }
                    }
                }
            }

            tag["gravestonePositions"] = positions;
            tag["gravestoneChestIds"] = chestIds;
            tag["slotTypes"] = slotTypes;
            tag["slotIndices"] = slotIndices;
            tag["slotFavorited"] = slotFavorited;
            tag["gravestoneOwners"] = owners;
        }

        public override void LoadWorldData(TagCompound tag)
        {
            GravestoneChests.Clear();
            GravestoneSlotData.Clear();
            GravestoneOwners.Clear();

            if (tag.ContainsKey("gravestonePositions") && tag.ContainsKey("gravestoneChestIds"))
            {
                var positions = tag.GetList<int>("gravestonePositions");
                var chestIds = tag.GetList<int>("gravestoneChestIds");
                var slotTypes = tag.ContainsKey("slotTypes") ? tag.GetList<int>("slotTypes") : null;
                var slotIndices = tag.ContainsKey("slotIndices") ? tag.GetList<int>("slotIndices") : null;
                var slotFavorited = tag.ContainsKey("slotFavorited") ? tag.GetList<bool>("slotFavorited") : null;
                var owners = tag.ContainsKey("gravestoneOwners") ? tag.GetList<string>("gravestoneOwners") : null;

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

                        // Load owner if available
                        if (owners != null && i < owners.Count)
                        {
                            GravestoneOwners[key] = owners[i];
                        }

                        // Load slot data if available
                        if (slotTypes != null && slotIndices != null)
                        {
                            ChestSlotData[] slotData = new ChestSlotData[40];
                            int baseIndex = i * 40;
                            for (int s = 0; s < 40 && baseIndex + s < slotTypes.Count; s++)
                            {
                                int slotType = slotTypes[baseIndex + s];
                                int slotIndex = slotIndices[baseIndex + s];
                                bool isFavorited = slotFavorited != null && baseIndex + s < slotFavorited.Count && slotFavorited[baseIndex + s];
                                if (slotType >= 0)
                                {
                                    slotData[s] = new ChestSlotData((SlotType)slotType, slotIndex, isFavorited);
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
        /// Attempts to restore items to the player who destroyed it.
        /// </summary>
        public static void OnGravestoneKilled(int tileX, int tileY)
        {
            Point origin = FindGravestoneOrigin(tileX, tileY);
            
            if (GravestoneChests.TryGetValue(origin, out int chestId))
            {
                if (chestId >= 0 && chestId < Main.maxChests && Main.chest[chestId] != null)
                {
                    // Find the player who is breaking this tile
                    Player breakingPlayer = FindPlayerBreakingTile(origin);
                    
                    if (breakingPlayer != null)
                    {
                        // Restore items to player's original slots (same as "Get Items" button)
                        RestoreItemsToPlayerOnDestroy(breakingPlayer, chestId, origin);
                    }
                    else
                    {
                        // No player found breaking tile - drop items on the ground
                        DropItemsFromChest(chestId, origin);
                    }
                }

                GravestoneChests.Remove(origin);
                GravestoneSlotData.Remove(origin);
                GravestoneOwners.Remove(origin);
            }
        }

        /// <summary>
        /// Finds the player who is currently breaking the gravestone.
        /// </summary>
        private static Player FindPlayerBreakingTile(Point gravestoneOrigin)
        {
            // In single player, the local player is always the one breaking tiles
            if (Main.netMode == NetmodeID.SinglePlayer)
            {
                return Main.LocalPlayer;
            }
            
            // On server, find the player closest to the tile within mining range
            if (Main.netMode == NetmodeID.Server)
            {
                Player closestPlayer = null;
                float closestDistSq = float.MaxValue;
                
                for (int i = 0; i < Main.maxPlayers; i++)
                {
                    Player player = Main.player[i];
                    if (player == null || !player.active)
                        continue;

                    float dx = player.Center.X - (gravestoneOrigin.X * 16 + 16);
                    float dy = player.Center.Y - (gravestoneOrigin.Y * 16 + 16);
                    float distanceSq = dx * dx + dy * dy;
                    
                    // Player mining range is about 5-6 tiles (80-96 pixels), use generous range of 10 tiles
                    if (distanceSq < 160 * 160 && distanceSq < closestDistSq)
                    {
                        closestDistSq = distanceSq;
                        closestPlayer = player;
                    }
                }
                
                return closestPlayer;
            }

            // On client, local player is breaking the tile
            return Main.LocalPlayer;
        }

        /// <summary>
        /// Restores items from a gravestone chest to the player when the gravestone is destroyed.
        /// Items go to original slots if possible, then free slots, then drop on ground.
        /// Respects the placement mode setting and ownership rules.
        /// </summary>
        private static void RestoreItemsToPlayerOnDestroy(Player player, int chestId, Point origin)
        {
            Chest chest = Main.chest[chestId];
            ChestSlotData[] slotData = GetSlotData(chestId);

            // Determine placement mode
            // If player is not the owner, always use NoReplacement mode
            bool isOwner = IsOwner(player, chestId);
            ItemPlacementMode placementMode = isOwner 
                ? GraveyardStorageConfig.Instance?.PlacementMode ?? ItemPlacementMode.ReplaceStarred
                : ItemPlacementMode.NoReplacement;

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
            List<Item> displacedItems = new List<Item>();

            // First pass: try to place items in their original slots
            for (int i = 0; i < itemsToProcess.Count; i++)
            {
                Item item = itemsToProcess[i];
                ChestSlotData slot = slotsToProcess[i];

                if (slot != null)
                {
                    // Determine if we should allow replacement for this item
                    bool allowReplace = false;
                    if (placementMode == ItemPlacementMode.ReplaceAll)
                    {
                        allowReplace = true;
                    }
                    else if (placementMode == ItemPlacementMode.ReplaceStarred && slot.IsFavorited)
                    {
                        allowReplace = true;
                    }
                    // NoReplacement mode: allowReplace stays false

                    if (TryPlaceInOriginalSlot(player, item, slot, allowReplace, displacedItems))
                    {
                        continue; // Item placed successfully
                    }
                }

                // Item couldn't be placed in original slot
                remainingItems.Add(item);
            }

            // Add displaced items to remaining items (they need to find a new home)
            remainingItems.AddRange(displacedItems);

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
                int itemIndex = Item.NewItem(
                    new Terraria.DataStructures.EntitySource_TileBreak(origin.X, origin.Y),
                    origin.X * 16, origin.Y * 16, 32, 32,
                    item.type, item.stack,
                    false, item.prefix);

                if (Main.netMode == NetmodeID.Server && itemIndex >= 0)
                {
                    NetMessage.SendData(MessageID.SyncItem, -1, -1, null, itemIndex);
                }
            }

            // Destroy the internal chest
            Chest.DestroyChest(chest.x, chest.y);
        }

        /// <summary>
        /// Drops all items from a gravestone chest on the ground.
        /// </summary>
        private static void DropItemsFromChest(int chestId, Point origin)
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

        /// <summary>
        /// Stores slot data for a gravestone chest.
        /// </summary>
        public static void StoreSlotData(Point gravestoneOrigin, List<SavedItemData> itemsWithSlots, string ownerName)
        {
            ChestSlotData[] slotData = new ChestSlotData[40];
            for (int i = 0; i < itemsWithSlots.Count && i < 40; i++)
            {
                slotData[i] = new ChestSlotData(itemsWithSlots[i].SlotType, itemsWithSlots[i].SlotIndex, itemsWithSlots[i].IsFavorited);
            }
            GravestoneSlotData[gravestoneOrigin] = slotData;
            GravestoneOwners[gravestoneOrigin] = ownerName;
        }

        /// <summary>
        /// Gets the owner name for a gravestone.
        /// </summary>
        public static string GetOwner(Point gravestoneOrigin)
        {
            if (GravestoneOwners.TryGetValue(gravestoneOrigin, out string owner))
            {
                return owner;
            }
            return null;
        }

        /// <summary>
        /// Checks if the player is the owner of the gravestone items.
        /// </summary>
        public static bool IsOwner(Player player, int chestId)
        {
            Point? origin = GetGravestoneOrigin(chestId);
            if (!origin.HasValue)
                return false;
            
            string owner = GetOwner(origin.Value);
            return string.IsNullOrEmpty(owner) || owner == player.name;
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
        /// Respects the placement mode setting and ownership rules.
        /// </summary>
        public static void RestoreItemsToPlayer(Player player, int chestId)
        {
            if (chestId < 0 || chestId >= Main.maxChests || Main.chest[chestId] == null)
                return;

            Chest chest = Main.chest[chestId];
            ChestSlotData[] slotData = GetSlotData(chestId);
            Point? origin = GetGravestoneOrigin(chestId);

            // Determine placement mode
            // If player is not the owner, always use NoReplacement mode
            bool isOwner = IsOwner(player, chestId);
            ItemPlacementMode placementMode = isOwner 
                ? GraveyardStorageConfig.Instance?.PlacementMode ?? ItemPlacementMode.ReplaceStarred
                : ItemPlacementMode.NoReplacement;

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
            List<Item> displacedItems = new List<Item>();

            // First pass: try to place items in their original slots
            for (int i = 0; i < itemsToProcess.Count; i++)
            {
                Item item = itemsToProcess[i];
                ChestSlotData slot = slotsToProcess[i];

                if (slot != null)
                {
                    // Determine if we should allow replacement for this item
                    bool allowReplace = false;
                    if (placementMode == ItemPlacementMode.ReplaceAll)
                    {
                        allowReplace = true;
                    }
                    else if (placementMode == ItemPlacementMode.ReplaceStarred && slot.IsFavorited)
                    {
                        allowReplace = true;
                    }
                    // NoReplacement mode: allowReplace stays false

                    if (TryPlaceInOriginalSlot(player, item, slot, allowReplace, displacedItems))
                    {
                        continue; // Item placed successfully
                    }
                }

                // Item couldn't be placed in original slot
                remainingItems.Add(item);
            }

            // Add displaced items to remaining items (they need to find a new home)
            remainingItems.AddRange(displacedItems);

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
                GravestoneOwners.Remove(origin.Value);
                
                // Destroy the internal chest (items already retrieved)
                Chest.DestroyChest(chest.x, chest.y);
            }
        }

        /// <summary>
        /// Tries to place an item in its original slot.
        /// If allowReplace is true and the slot is occupied, moves the existing item to a free slot.
        /// </summary>
        /// <param name="player">The player to place the item into</param>
        /// <param name="item">The item to place</param>
        /// <param name="slot">The original slot data</param>
        /// <param name="allowReplace">Whether to replace items in occupied slots</param>
        /// <param name="displacedItems">List to collect items that were displaced</param>
        private static bool TryPlaceInOriginalSlot(Player player, Item item, ChestSlotData slot, bool allowReplace, List<Item> displacedItems)
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

            // Slot is occupied - check if we should replace
            if (allowReplace)
            {
                // Save the existing item to be placed elsewhere
                displacedItems.Add(targetArray[targetIndex].Clone());
                // Place our item in the original slot
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
