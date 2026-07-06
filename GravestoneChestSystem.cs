using System;
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
    /// A temporary tile (stone floor or bubble wall) that the mod added to keep a gravestone safe.
    /// Removed again once the gravestone's items are retrieved or the gravestone is destroyed.
    /// </summary>
    public class TempBlock
    {
        public int X { get; set; }
        public int Y { get; set; }
        public ushort Type { get; set; }

        public TempBlock(int x, int y, ushort type)
        {
            X = x;
            Y = y;
            Type = type;
        }
    }

    /// <summary>
    /// Stores slot metadata for items in a gravestone storage (for restoration).
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
    /// Custom storage for gravestone items. Not limited by vanilla chest's 40-slot limit.
    /// Stores all player items: inventory (58), armor (20), dyes (10), misc equips (5), misc dyes (5) = ~100 slots max.
    /// </summary>
    public class GravestoneStorage
    {
        public List<Item> Items { get; private set; } = new List<Item>();
        public List<ChestSlotData> SlotData { get; private set; } = new List<ChestSlotData>();

        /// <summary>
        /// Temporary tiles the mod placed to keep this gravestone safe (stone floor + bubble walls).
        /// Empty unless the "Guarantee Gravestone Placement" option was active when the player died.
        /// </summary>
        public List<TempBlock> TempBlocks { get; private set; } = new List<TempBlock>();

        public string OwnerName { get; set; } = "";
        public int TileX { get; set; }
        public int TileY { get; set; }

        public GravestoneStorage(int tileX, int tileY)
        {
            TileX = tileX;
            TileY = tileY;
        }

        public int ItemCount => Items.Count;

        public void AddItem(Item item, ChestSlotData slotData)
        {
            Items.Add(item.Clone());
            SlotData.Add(slotData);
        }

        public void Clear()
        {
            Items.Clear();
            SlotData.Clear();
        }
    }

    /// <summary>
    /// System that tracks which vanilla gravestones have associated item storage.
    /// Persists data with world save/load.
    /// Uses custom storage instead of vanilla Chest to support all player items.
    /// </summary>
    public class GravestoneChestSystem : ModSystem
    {
        // Dictionary mapping gravestone tile position to storage
        // Key: Point (x, y) of the top-left corner of the gravestone
        // Value: GravestoneStorage containing all items
        public static Dictionary<Point, GravestoneStorage> GravestoneStorages { get; private set; } = new Dictionary<Point, GravestoneStorage>();

        // Legacy support: still keep these for compatibility during transition
        public static Dictionary<Point, int> GravestoneChests { get; private set; } = new Dictionary<Point, int>();
        public static Dictionary<Point, ChestSlotData[]> GravestoneSlotData { get; private set; } = new Dictionary<Point, ChestSlotData[]>();
        public static Dictionary<Point, string> GravestoneOwners { get; private set; } = new Dictionary<Point, string>();

        public override void OnWorldLoad()
        {
            GravestoneStorages = new Dictionary<Point, GravestoneStorage>();
            GravestoneChests = new Dictionary<Point, int>();
            GravestoneSlotData = new Dictionary<Point, ChestSlotData[]>();
            GravestoneOwners = new Dictionary<Point, string>();
        }

        public override void OnWorldUnload()
        {
            GravestoneStorages.Clear();
            GravestoneChests.Clear();
            GravestoneSlotData.Clear();
            GravestoneOwners.Clear();
        }

        public override void SaveWorldData(TagCompound tag)
        {
            // Save new storage format
            var storageList = new List<TagCompound>();

            foreach (var kvp in GravestoneStorages)
            {
                var storage = kvp.Value;
                if (storage.Items.Count == 0)
                    continue;

                var storageTag = new TagCompound();
                storageTag["x"] = kvp.Key.X;
                storageTag["y"] = kvp.Key.Y;
                storageTag["owner"] = storage.OwnerName ?? "";

                // Save items
                var itemTags = new List<TagCompound>();
                for (int i = 0; i < storage.Items.Count; i++)
                {
                    var item = storage.Items[i];
                    var slotData = i < storage.SlotData.Count ? storage.SlotData[i] : null;

                    var itemTag = new TagCompound();
                    itemTag["item"] = ItemIO.Save(item);
                    if (slotData != null)
                    {
                        itemTag["slotType"] = (int)slotData.SlotType;
                        itemTag["slotIndex"] = slotData.SlotIndex;
                        itemTag["favorited"] = slotData.IsFavorited;
                    }
                    itemTags.Add(itemTag);
                }
                storageTag["items"] = itemTags;

                // Save temporary blocks (stone floor / bubble walls) so they can be cleaned up after a reload.
                if (storage.TempBlocks.Count > 0)
                {
                    var tempTags = new List<TagCompound>();
                    foreach (var tb in storage.TempBlocks)
                    {
                        tempTags.Add(new TagCompound
                        {
                            ["x"] = tb.X,
                            ["y"] = tb.Y,
                            ["type"] = (int)tb.Type
                        });
                    }
                    storageTag["tempBlocks"] = tempTags;
                }

                storageList.Add(storageTag);
            }

            tag["gravestoneStorages"] = storageList;
        }

        public override void LoadWorldData(TagCompound tag)
        {
            GravestoneStorages.Clear();
            GravestoneChests.Clear();
            GravestoneSlotData.Clear();
            GravestoneOwners.Clear();

            // Load new storage format
            if (tag.ContainsKey("gravestoneStorages"))
            {
                var storageList = tag.GetList<TagCompound>("gravestoneStorages");
                foreach (var storageTag in storageList)
                {
                    int x = storageTag.GetInt("x");
                    int y = storageTag.GetInt("y");
                    string owner = storageTag.GetString("owner");
                    Point key = new Point(x, y);

                    var storage = new GravestoneStorage(x, y);
                    storage.OwnerName = owner;

                    var itemTags = storageTag.GetList<TagCompound>("items");
                    foreach (var itemTag in itemTags)
                    {
                        Item item = ItemIO.Load(itemTag.GetCompound("item"));
                        int slotType = itemTag.ContainsKey("slotType") ? itemTag.GetInt("slotType") : -1;
                        int slotIndex = itemTag.ContainsKey("slotIndex") ? itemTag.GetInt("slotIndex") : -1;
                        bool isFavorited = itemTag.ContainsKey("favorited") && itemTag.GetBool("favorited");

                        ChestSlotData slotData = slotType >= 0 
                            ? new ChestSlotData((SlotType)slotType, slotIndex, isFavorited) 
                            : null;

                        storage.Items.Add(item);
                        storage.SlotData.Add(slotData);
                    }

                    // Load temporary blocks (guarded for worlds saved before this feature existed).
                    if (storageTag.ContainsKey("tempBlocks"))
                    {
                        foreach (var tempTag in storageTag.GetList<TagCompound>("tempBlocks"))
                        {
                            storage.TempBlocks.Add(new TempBlock(
                                tempTag.GetInt("x"),
                                tempTag.GetInt("y"),
                                (ushort)tempTag.GetInt("type")));
                        }
                    }

                    if (storage.Items.Count > 0)
                    {
                        GravestoneStorages[key] = storage;
                    }
                }
            }
        }

        /// <summary>
        /// Registers a gravestone position with an associated storage.
        /// </summary>
        public static GravestoneStorage RegisterGravestoneStorage(int tileX, int tileY, string ownerName)
        {
            Point key = new Point(tileX, tileY);
            var storage = new GravestoneStorage(tileX, tileY);
            storage.OwnerName = ownerName;
            GravestoneStorages[key] = storage;
            return storage;
        }

        /// <summary>
        /// Gets the storage for a gravestone at the given position, or null if none.
        /// </summary>
        public static GravestoneStorage GetStorageForGravestone(int tileX, int tileY)
        {
            // Find the top-left corner of the gravestone (they can be multi-tile)
            Point topLeft = FindGravestoneOrigin(tileX, tileY);
            
            if (GravestoneStorages.TryGetValue(topLeft, out GravestoneStorage storage))
            {
                return storage;
            }

            return null;
        }

        /// <summary>
        /// Checks if a gravestone at the given position has an associated storage with items.
        /// </summary>
        public static bool HasStorage(int tileX, int tileY)
        {
            var storage = GetStorageForGravestone(tileX, tileY);
            return storage != null && storage.Items.Count > 0;
        }

        /// <summary>
        /// Legacy: Checks if a gravestone at the given position has an associated chest.
        /// Now checks for storage instead.
        /// </summary>
        public static bool HasChest(int tileX, int tileY)
        {
            return HasStorage(tileX, tileY);
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
        /// Removes the storage when a gravestone is destroyed.
        /// Attempts to restore items to the player who destroyed it.
        /// </summary>
        public static void OnGravestoneKilled(int tileX, int tileY)
        {
            Point origin = FindGravestoneOrigin(tileX, tileY);
            
            if (GravestoneStorages.TryGetValue(origin, out GravestoneStorage storage))
            {
                ModContent.GetInstance<GraveyardStorage>().Logger.Debug(
                    $"[GraveyardStorage] Gravestone with storage killed at {origin}; items={storage.Items.Count}, tempBlocks={storage.TempBlocks.Count}, netMode={Main.netMode}");

                if (storage.Items.Count > 0)
                {
                    // Find the player who is breaking this tile
                    Player breakingPlayer = FindPlayerBreakingTile(origin);
                    
                    if (breakingPlayer != null)
                    {
                        // Restore items to player's original slots (same as "Get Items" button)
                        RestoreItemsToPlayerFromStorage(breakingPlayer, storage, origin, isDestroying: true);
                    }
                    else
                    {
                        // No player found breaking tile - drop items on the ground
                        DropItemsFromStorage(storage, origin);
                    }
                }

                GravestoneStorages.Remove(origin);
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
        /// Drops all items from a gravestone storage on the ground.
        /// </summary>
        private static void DropItemsFromStorage(GravestoneStorage storage, Point origin)
        {
            foreach (var item in storage.Items)
            {
                if (item != null && !item.IsAir)
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
            }
            RemoveTempBlocks(storage);
            storage.Clear();
        }

        /// <summary>
        /// Removes any temporary blocks (stone floor / bubble walls) the mod placed to keep this gravestone safe.
        /// Only tiles that still match the type we placed are removed, so player-built blocks are never touched.
        /// </summary>
        public static void RemoveTempBlocks(GravestoneStorage storage)
        {
            if (storage == null || storage.TempBlocks.Count == 0)
                return;

            foreach (var tb in storage.TempBlocks)
            {
                if (tb.X < 0 || tb.X >= Main.maxTilesX || tb.Y < 0 || tb.Y >= Main.maxTilesY)
                    continue;

                Tile t = Main.tile[tb.X, tb.Y];
                if (!t.HasTile || t.TileType != tb.Type)
                    continue; // player changed this tile - leave it alone

                // Clear the tile directly. We intentionally do NOT call WorldGen.SquareTileFrame here: reframing the
                // stone floor would re-run the gravestone's support check and could synchronously KillTile it,
                // re-entering OnGravestoneKilled while this storage is still processing. SendTileSquare reframes on
                // clients; in single player the neighbor sprites resolve on the next natural tile update.
                t.HasTile = false;
                t.TileFrameX = 0;
                t.TileFrameY = 0;

                if (Main.netMode != NetmodeID.SinglePlayer)
                    NetMessage.SendTileSquare(-1, tb.X, tb.Y, 1, 1);
            }

            storage.TempBlocks.Clear();
        }

        /// <summary>
        /// Clears the 2x2 tombstone tiles (and its sign) at the given origin without dropping a tombstone item.
        /// Used after item retrieval to remove a gravestone that was only standing on temporary support.
        /// </summary>
        private static void ClearGravestoneAt(Point origin)
        {
            for (int dx = 0; dx <= 1; dx++)
            {
                for (int dy = 0; dy <= 1; dy++)
                {
                    int tx = origin.X + dx;
                    int ty = origin.Y + dy;
                    if (tx < 0 || tx >= Main.maxTilesX || ty < 0 || ty >= Main.maxTilesY)
                        continue;

                    Tile t = Main.tile[tx, ty];
                    if (t.HasTile && t.TileType == TileID.Tombstones)
                    {
                        t.HasTile = false;
                        t.TileFrameX = 0;
                        t.TileFrameY = 0;
                    }
                }
            }

            for (int i = 0; i < Main.sign.Length; i++)
            {
                if (Main.sign[i] != null && Main.sign[i].x == origin.X && Main.sign[i].y == origin.Y)
                {
                    Main.sign[i] = null;
                    break;
                }
            }

            if (Main.netMode != NetmodeID.SinglePlayer)
                NetMessage.SendTileSquare(-1, origin.X, origin.Y, 2, 2);
        }

        /// <summary>
        /// Gets the owner name for a gravestone.
        /// </summary>
        public static string GetOwner(Point gravestoneOrigin)
        {
            if (GravestoneStorages.TryGetValue(gravestoneOrigin, out GravestoneStorage storage))
            {
                return storage.OwnerName;
            }
            return null;
        }

        /// <summary>
        /// Checks if the player is the owner of the gravestone items.
        /// </summary>
        public static bool IsOwnerOfStorage(Player player, GravestoneStorage storage)
        {
            if (storage == null)
                return false;
            
            return string.IsNullOrEmpty(storage.OwnerName) || storage.OwnerName == player.name;
        }

        /// <summary>
        /// Gets the number of items stored in a gravestone storage.
        /// </summary>
        public static int GetStorageItemCount(int tileX, int tileY)
        {
            var storage = GetStorageForGravestone(tileX, tileY);
            return storage?.ItemCount ?? 0;
        }

        /// <summary>
        /// Restores all items from a gravestone storage to the player's inventory.
        /// Items are placed in their original slots if possible, otherwise in free slots, otherwise dropped.
        /// Respects the placement mode setting and ownership rules.
        /// </summary>
        public static void RestoreItemsFromStorage(Player player, int tileX, int tileY)
        {
            Point origin = FindGravestoneOrigin(tileX, tileY);
            if (!GravestoneStorages.TryGetValue(origin, out GravestoneStorage storage))
                return;

            RestoreItemsToPlayerFromStorage(player, storage, origin, isDestroying: false);
        }

        /// <summary>
        /// Internal method to restore items from storage to player.
        /// </summary>
        private static void RestoreItemsToPlayerFromStorage(Player player, GravestoneStorage storage, Point origin, bool isDestroying)
        {
            // Determine placement mode
            // If player is not the owner, always use NoReplacement mode
            bool isOwner = IsOwnerOfStorage(player, storage);
            ItemPlacementMode placementMode = isOwner 
                ? GraveyardStorageConfig.Instance?.PlacementMode ?? ItemPlacementMode.ReplaceStarred
                : ItemPlacementMode.NoReplacement;

            List<Item> itemsToProcess = new List<Item>();
            List<ChestSlotData> slotsToProcess = new List<ChestSlotData>();

            // Collect all items from the storage
            for (int i = 0; i < storage.Items.Count; i++)
            {
                var item = storage.Items[i];
                if (item != null && !item.IsAir)
                {
                    itemsToProcess.Add(item.Clone());
                    slotsToProcess.Add(i < storage.SlotData.Count ? storage.SlotData[i] : null);
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
                if (isDestroying)
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
                else
                {
                    Item.NewItem(
                        player.GetSource_Misc("GraveyardStorage"),
                        player.Center, Vector2.Zero,
                        item.type, item.stack,
                        false, item.prefix);
                }
            }

            // Clean up: remove temporary blocks and the gravestone storage since items are now retrieved.
            // If the gravestone was standing on our temporary support, remove the (now empty) gravestone too so it
            // doesn't hang in the air after the support is gone.
            bool hadTempBlocks = storage.TempBlocks.Count > 0;
            RemoveTempBlocks(storage);
            if (!isDestroying && hadTempBlocks)
                ClearGravestoneAt(origin);
            storage.Clear();
            GravestoneStorages.Remove(origin);
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
