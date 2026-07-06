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
        public bool IsFavorited { get; set; }

        public SavedItemData(Item item, SlotType slotType, int slotIndex, bool isFavorited = false)
        {
            Item = item;
            SlotType = slotType;
            SlotIndex = slotIndex;
            IsFavorited = isFavorited;
        }
    }

    public class GraveyardStoragePlayer : ModPlayer
    {
        // Track death state for item transfer
        private bool pendingItemTransfer = false;
        private Point deathPosition;
        private List<SavedItemData> savedItemsWithSlots = new List<SavedItemData>();
        private int transferWaitFrames = 0;

        // Let the vanilla gravestone projectile spawn before we start looking.
        private const int TransferInitialDelay = 10;
        // Keep looking this long for the vanilla gravestone before we give up and place / drop.
        // The vanilla gravestone starts as a projectile that flies and rolls, and only becomes a findable tile once
        // it settles - which can be well after death. Deciding too early is what caused duplicate gravestones.
        private const int TransferMaxWaitFrames = 600; // ~10 seconds
        // Rolled / thrown gravestones can land well away from the death spot.
        private const int GravestoneSearchRadius = 50;

        public override void PreUpdate()
        {
            if (!pendingItemTransfer)
                return;

            transferWaitFrames++;

            bool guarantee = GraveyardStorageServerConfig.Instance?.GuaranteeGravestonePlacement ?? false;

            if (guarantee)
            {
                // Guaranteed placement: don't rely on vanilla's gravestone at all. Its projectile rolls, flips and
                // sticks near obstacles (trees, slopes) and often never becomes a proper tile, leaving a stray
                // duplicate next to ours. Instead we suppress it and place our own at the death spot.
                UpdateGuaranteedPlacement();
                return;
            }

            // --- Guaranteed placement off: original behavior - attach to the vanilla gravestone, or drop on timeout. ---

            // Give the vanilla gravestone projectile a moment to spawn.
            if (transferWaitFrames < TransferInitialDelay)
                return;

            // Look for the settled vanilla gravestone every frame (even while still on the death screen).
            if (TryAttachToVanillaGravestone())
            {
                pendingItemTransfer = false;
                transferWaitFrames = 0;
                return;
            }

            // Not found yet - keep waiting before concluding the vanilla gravestone will never appear.
            if (transferWaitFrames < TransferMaxWaitFrames)
                return;

            // Timed out: no vanilla gravestone ever appeared.
            FinishWithoutVanillaGravestone();
            pendingItemTransfer = false;
            transferWaitFrames = 0;
        }

        /// <summary>
        /// Guaranteed-placement flow: kill vanilla's rolling gravestone projectile(s), remove any stray vanilla
        /// gravestone tiles that settled near the death spot, then place our own gravestone and store the items.
        /// </summary>
        private void UpdateGuaranteedPlacement()
        {
            // Stop vanilla's gravestone from rolling away / duplicating. Runs every frame so we catch the projectile
            // right after it spawns, before it can travel far.
            int killedProjectiles = KillNearbyVanillaGravestoneProjectiles();
            if (killedProjectiles > 0)
                Mod.Logger.Debug($"[GraveyardStorage] Removed {killedProjectiles} vanilla gravestone projectile(s) near {deathPosition}");

            // Give the vanilla projectile a moment to spawn so the kill above can catch it.
            if (transferWaitFrames < TransferInitialDelay)
                return;

            // Remove any vanilla gravestone that already settled near the death spot (leftover / duplicate).
            int removedTiles = RemoveStrayVanillaGravestonesNearDeath();
            if (removedTiles > 0)
                Mod.Logger.Debug($"[GraveyardStorage] Removed {removedTiles} stray vanilla gravestone tile(s) near {deathPosition}");

            PlaceOwnGravestoneAndStore();

            pendingItemTransfer = false;
            transferWaitFrames = 0;
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
            transferWaitFrames = 0;

            Mod.Logger.Debug($"[GraveyardStorage] Death at {deathPosition}; saved {savedItemsWithSlots.Count} items (difficulty={Player.difficulty}, netMode={Main.netMode})");

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
                    savedItemsWithSlots.Add(new SavedItemData(Player.armor[i].Clone(), SlotType.Armor, i, Player.armor[i].favorited));
                    Player.armor[i].TurnToAir();
                }
            }

            // Save dyes
            for (int i = 0; i < Player.dye.Length; i++)
            {
                if (!Player.dye[i].IsAir)
                {
                    savedItemsWithSlots.Add(new SavedItemData(Player.dye[i].Clone(), SlotType.Dye, i, Player.dye[i].favorited));
                    Player.dye[i].TurnToAir();
                }
            }

            // Save main inventory (slots 0-49)
            for (int i = 0; i < 50; i++)
            {
                if (!Player.inventory[i].IsAir)
                {
                    savedItemsWithSlots.Add(new SavedItemData(Player.inventory[i].Clone(), SlotType.Inventory, i, Player.inventory[i].favorited));
                    Player.inventory[i].TurnToAir();
                }
            }

            // Save coins (slots 50-53)
            for (int i = 50; i < 54; i++)
            {
                if (!Player.inventory[i].IsAir)
                {
                    savedItemsWithSlots.Add(new SavedItemData(Player.inventory[i].Clone(), SlotType.Coins, i - 50, Player.inventory[i].favorited));
                    Player.inventory[i].TurnToAir();
                }
            }

            // Save ammo (slots 54-57)
            for (int i = 54; i < 58; i++)
            {
                if (!Player.inventory[i].IsAir)
                {
                    savedItemsWithSlots.Add(new SavedItemData(Player.inventory[i].Clone(), SlotType.Ammo, i - 54, Player.inventory[i].favorited));
                    Player.inventory[i].TurnToAir();
                }
            }

            // Save misc equipment
            for (int i = 0; i < Player.miscEquips.Length; i++)
            {
                if (!Player.miscEquips[i].IsAir)
                {
                    savedItemsWithSlots.Add(new SavedItemData(Player.miscEquips[i].Clone(), SlotType.MiscEquip, i, Player.miscEquips[i].favorited));
                    Player.miscEquips[i].TurnToAir();
                }
            }

            // Save misc dyes
            for (int i = 0; i < Player.miscDyes.Length; i++)
            {
                if (!Player.miscDyes[i].IsAir)
                {
                    savedItemsWithSlots.Add(new SavedItemData(Player.miscDyes[i].Clone(), SlotType.MiscDye, i, Player.miscDyes[i].favorited));
                    Player.miscDyes[i].TurnToAir();
                }
            }
        }

        /// <summary>
        /// Looks for a settled vanilla gravestone near the death position and, if found, attaches the saved items to
        /// it (reinforcing it first when guaranteed placement is enabled). Returns true when the transfer is done
        /// (attached, or there was nothing to store), false when no gravestone has settled yet and we should retry.
        /// </summary>
        private bool TryAttachToVanillaGravestone()
        {
            if (savedItemsWithSlots.Count == 0)
                return true; // nothing to store - stop retrying

            Point? found = FindNearestUnclaimedGravestone();
            if (!found.HasValue)
                return false; // vanilla gravestone has not settled into a tile yet - keep waiting

            int count = savedItemsWithSlots.Count;
            AttachSavedItemsTo(found.Value, new List<TempBlock>());
            Mod.Logger.Debug($"[GraveyardStorage] Attached {count} items after {transferWaitFrames}f to vanilla gravestone at {found.Value}");
            return true;
        }

        /// <summary>
        /// Called (only when guaranteed placement is OFF) once we have waited long enough that no vanilla gravestone
        /// will appear. Drops the items on the ground - the original behavior.
        /// </summary>
        private void FinishWithoutVanillaGravestone()
        {
            if (savedItemsWithSlots.Count == 0)
                return;

            int dropped = savedItemsWithSlots.Count;
            DropSavedItems();
            savedItemsWithSlots.Clear();
            Mod.Logger.Debug($"[GraveyardStorage] No vanilla gravestone found; dropped {dropped} items at {deathPosition}");
        }

        /// <summary>
        /// Searches around the death position for the closest vanilla gravestone that has no storage yet.
        /// </summary>
        private Point? FindNearestUnclaimedGravestone()
        {
            Point? foundGravestone = null;
            int closestDistance = int.MaxValue;

            for (int x = deathPosition.X - GravestoneSearchRadius; x <= deathPosition.X + GravestoneSearchRadius; x++)
            {
                for (int y = deathPosition.Y - GravestoneSearchRadius; y <= deathPosition.Y + GravestoneSearchRadius; y++)
                {
                    if (x < 0 || x >= Main.maxTilesX || y < 0 || y >= Main.maxTilesY)
                        continue;

                    Tile tile = Main.tile[x, y];
                    if (tile.HasTile && tile.TileType == TileID.Tombstones)
                    {
                        Point origin = GravestoneChestSystem.FindGravestoneOrigin(x, y);
                        if (!GravestoneChestSystem.HasStorage(origin.X, origin.Y))
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

            return foundGravestone;
        }

        /// <summary>
        /// Registers storage at the given gravestone origin and moves all saved items into it.
        /// </summary>
        private void AttachSavedItemsTo(Point origin, List<TempBlock> tempBlocks)
        {
            var storage = GravestoneChestSystem.RegisterGravestoneStorage(origin.X, origin.Y, Player.name);
            storage.TempBlocks.AddRange(tempBlocks);

            // Transfer all saved items to storage (no limit!)
            foreach (var itemData in savedItemsWithSlots)
            {
                var slotData = new ChestSlotData(itemData.SlotType, itemData.SlotIndex, itemData.IsFavorited);
                storage.AddItem(itemData.Item, slotData);
            }

            savedItemsWithSlots.Clear();

            // Point the on-map death marker at the gravestone so it guides the player to their items rather than to
            // the exact death spot (which may be in lava / unreachable, or where the gravestone rolled away from).
            Player.lastDeathPostion = new Vector2(origin.X * 16 + 16, origin.Y * 16 + 16);
            Player.showLastDeath = true;
        }

        /// <summary>
        /// Deactivates vanilla gravestone projectiles (tombstone AI) near the death position so they never roll
        /// away and settle into a stray duplicate gravestone. Returns how many were removed.
        /// </summary>
        private int KillNearbyVanillaGravestoneProjectiles()
        {
            const int tombstoneAiStyle = 17; // vanilla gravestone / tombstone projectile AI
            int killed = 0;

            for (int i = 0; i < Main.projectile.Length; i++)
            {
                Projectile p = Main.projectile[i];
                if (p == null || !p.active || p.aiStyle != tombstoneAiStyle)
                    continue;

                int px = (int)(p.Center.X / 16f);
                int py = (int)(p.Center.Y / 16f);
                if (System.Math.Abs(px - deathPosition.X) > GravestoneSearchRadius ||
                    System.Math.Abs(py - deathPosition.Y) > GravestoneSearchRadius)
                    continue;

                // Set inactive rather than Kill() so the projectile does not place its tombstone tile on death.
                p.active = false;
                killed++;
            }

            return killed;
        }

        /// <summary>
        /// Removes vanilla gravestone tiles (with no storage) that settled near the death position - leftovers from
        /// the projectile we are replacing. Gravestones that already hold stored items are never touched.
        /// Returns how many gravestones were removed.
        /// </summary>
        private int RemoveStrayVanillaGravestonesNearDeath()
        {
            int removed = 0;
            var processed = new HashSet<Point>();

            for (int x = deathPosition.X - GravestoneSearchRadius; x <= deathPosition.X + GravestoneSearchRadius; x++)
            {
                for (int y = deathPosition.Y - GravestoneSearchRadius; y <= deathPosition.Y + GravestoneSearchRadius; y++)
                {
                    if (x < 0 || x >= Main.maxTilesX || y < 0 || y >= Main.maxTilesY)
                        continue;

                    Tile tile = Main.tile[x, y];
                    if (!tile.HasTile || tile.TileType != TileID.Tombstones)
                        continue;

                    Point origin = GravestoneChestSystem.FindGravestoneOrigin(x, y);
                    if (!processed.Add(origin))
                        continue;

                    // Never remove a gravestone that already holds stored items (e.g. from a previous death).
                    if (GravestoneChestSystem.HasStorage(origin.X, origin.Y))
                        continue;

                    ClearGravestoneTiles(origin);
                    removed++;
                }
            }

            return removed;
        }

        /// <summary>
        /// Clears the 2x2 tombstone tiles (and its sign) at the given origin without dropping a tombstone item.
        /// </summary>
        private void ClearGravestoneTiles(Point origin)
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

            // Drop the sign entity at the origin, if any.
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
        /// Places our own guaranteed gravestone at a safe spot near the death position and stores the items in it.
        /// Falls back to dropping the items only if no safe spot could be found at all.
        /// </summary>
        private void PlaceOwnGravestoneAndStore()
        {
            if (savedItemsWithSlots.Count == 0)
                return;

            var tempBlocks = new List<TempBlock>();
            Point origin = GravestoneReinforcer.PlaceNew(deathPosition, tempBlocks, out bool placed);

            if (!placed)
            {
                // No open spot anywhere nearby (player entombed in solid blocks). Force-carve one at the death spot
                // as a last resort so the items are never dropped.
                tempBlocks.Clear();
                origin = GravestoneReinforcer.ForcePlace(deathPosition, tempBlocks);
                int forced = savedItemsWithSlots.Count;
                AttachSavedItemsTo(origin, tempBlocks);
                Mod.Logger.Warn($"[GraveyardStorage] No open spot near {deathPosition}; force-placed gravestone at {origin} for {forced} items (tempBlocks={tempBlocks.Count})");
                return;
            }

            int count = savedItemsWithSlots.Count;
            AttachSavedItemsTo(origin, tempBlocks);
            Mod.Logger.Debug($"[GraveyardStorage] Placed our own gravestone at {origin} for {count} items (tempBlocks={tempBlocks.Count})");
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
