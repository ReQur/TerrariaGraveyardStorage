using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;

namespace GraveyardStorage
{
    /// <summary>
    /// Builds a reliable gravestone when the "Guarantee Gravestone Placement" option is enabled.
    ///
    /// <see cref="PlaceNew"/> places a fresh gravestone at a safe spot near the death position: it finds the nearest
    /// dry 2x2 pocket (moving up out of any liquid), settles it down onto the ground, puts a stone floor underneath
    /// it if it would otherwise float, and walls off any liquid still touching it with bubble blocks.
    ///
    /// The mod places its own gravestone rather than reusing Terraria's, because the vanilla gravestone starts as a
    /// projectile that rolls, flips and sticks near obstacles and often never becomes a usable tile - the death flow
    /// (<see cref="GraveyardStoragePlayer"/>) suppresses the vanilla projectile and calls this instead.
    ///
    /// Every block this class adds (stone floor + bubble walls) is recorded in the given temp-block list so it can be
    /// removed again once the player retrieves their items or the gravestone is destroyed. See
    /// <see cref="GravestoneChestSystem.RemoveTempBlocks"/>.
    /// </summary>
    public static class GravestoneReinforcer
    {
        // How far up we are willing to search for a dry pocket before giving up.
        private const int MaxUpwardSearch = 80;

        /// <summary>
        /// Places a brand new gravestone near the death position. Returns its top-left origin.
        /// </summary>
        public static Point PlaceNew(Point deathPos, List<TempBlock> temp, out bool success)
        {
            Point? spot = FindSafeSpotNear(deathPos);
            if (!spot.HasValue)
            {
                success = false;
                return Point.Zero;
            }

            Point target = spot.Value;
            PlaceFreshTombstone(target, Main.rand.Next(6));
            CreateSign(target.X, target.Y, "");
            SealAndSupport(target, temp);
            SyncArea(target);

            success = true;
            return target;
        }

        /// <summary>
        /// Absolute last resort for when there is no open spot anywhere near the death (the player was entombed in
        /// solid blocks). Forcibly carves the 2x2 at the death position - replacing whatever blocks are there - puts
        /// a stone floor under it and seals any liquid, then places the gravestone. Always succeeds, so items are
        /// never dropped when guaranteed placement is on. Returns the top-left origin.
        /// </summary>
        public static Point ForcePlace(Point deathPos, List<TempBlock> temp)
        {
            // Keep the whole footprint (body + stone floor + bubble ring) inside the world.
            int x = System.Math.Clamp(deathPos.X, 5, Main.maxTilesX - 6);
            int y = System.Math.Clamp(deathPos.Y, 5, Main.maxTilesY - 6);
            Point target = new Point(x, y);

            // PlaceFreshTombstone clears each body tile's block layer first, so any blocks in the 2x2 are replaced.
            PlaceFreshTombstone(target, Main.rand.Next(6));
            CreateSign(x, y, "");
            SealAndSupport(target, temp);
            SyncArea(target);
            return target;
        }

        // ---- placement helpers ----------------------------------------------------------------------------

        // How far to the sides we look for a clear column to place the gravestone in.
        private const int MaxHorizontalSearch = 25;
        // How far above/below the death height a column is allowed to place, so we don't shoot up a tree trunk.
        private const int ColumnVerticalWindow = 8;

        /// <summary>
        /// Finds the closest safe spot for a gravestone near the death position. Searches sideways column by column
        /// (so an obstruction directly above the death spot, like a tree trunk, doesn't push the gravestone far up)
        /// and settles each candidate down onto the ground. Falls back to a straight-up search so items are never
        /// lost. Returns the top-left origin of the chosen 2x2, or null if nothing could be found.
        /// </summary>
        private static Point? FindSafeSpotNear(Point deathPos)
        {
            Point? best = null;
            int bestScore = int.MaxValue;

            for (int dx = -MaxHorizontalSearch; dx <= MaxHorizontalSearch; dx++)
            {
                Point? spot = FindColumnSpot(deathPos.X + dx, deathPos.Y);
                if (!spot.HasValue)
                    continue;

                // Prefer spots close to where the player died (weight horizontal distance a bit more).
                int score = System.Math.Abs(dx) * 2 + System.Math.Abs(spot.Value.Y - deathPos.Y);
                if (score < bestScore)
                {
                    bestScore = score;
                    best = spot;
                }
            }

            if (best.HasValue)
                return best;

            // Fallback: nothing suitable nearby - search straight up in the death column so items are not lost.
            Point? up = FindDryPocketAbove(deathPos.X, deathPos.Y + 1);
            if (up.HasValue)
            {
                Point settled = SettleDown(up.Value);
                if (IsCleanSpot(settled))
                    return settled;
            }
            return null;
        }

        /// <summary>
        /// Looks for a dry 2x2 in the given column within a small window of the death height, then settles it down
        /// onto the ground. Returns null if the column has no clear spot near the death height (e.g. a tree trunk).
        /// </summary>
        private static Point? FindColumnSpot(int x, int aroundY)
        {
            for (int y = aroundY - ColumnVerticalWindow; y <= aroundY + ColumnVerticalWindow; y++)
            {
                if (!BodyIsAirAndDry(x, y))
                    continue;
                Point settled = SettleDown(new Point(x, y));
                if (IsCleanSpot(settled))
                    return settled;
            }
            return null;
        }

        /// <summary>
        /// Scans upward from the given position for the nearest 2x2 area that is entirely air and liquid-free.
        /// Returns the top-left of that area, or null if nothing suitable was found.
        /// </summary>
        private static Point? FindDryPocketAbove(int x, int y0)
        {
            int limit = System.Math.Max(10, y0 - MaxUpwardSearch);
            for (int y = y0 - 1; y >= limit; y--)
            {
                if (BodyIsAirAndDry(x, y))
                    return new Point(x, y);
            }
            return null;
        }

        /// <summary>
        /// Lowers a 2x2 straight down until it rests on a surface, so the gravestone sits on the ground instead of
        /// floating where the player died. Descends through air and cuttable foliage (grass, plants); stops on solid
        /// ground, liquid, or an obstacle (furniture / chest). Capped so it never falls forever.
        /// </summary>
        private static Point SettleDown(Point top)
        {
            int x = top.X;
            int y = top.Y;
            const int maxDrop = 40;

            for (int i = 0; i < maxDrop; i++)
            {
                if (!IsPassableForDescent(x, y + 2) || !IsPassableForDescent(x + 1, y + 2))
                    break; // solid ground / liquid / obstacle directly below - rest here
                y++;
            }

            return new Point(x, y);
        }

        /// <summary>
        /// Air or cuttable foliage (grass, plants, vines) - the gravestone can descend through and clear these.
        /// Liquid and any non-cuttable tile (ground, furniture, chests) stop the descent.
        /// </summary>
        private static bool IsPassableForDescent(int x, int y)
        {
            if (!InBounds(x, y))
                return false;
            Tile t = Main.tile[x, y];
            if (t.LiquidAmount > 0)
                return false; // don't sink into liquid - rest above it
            if (!t.HasTile)
                return true; // air
            return Main.tileCut[t.TileType]; // grass / plants / vines are safe to pass through and clear
        }

        /// <summary>
        /// A placement is clean only if the support row under the gravestone is solid ground or empty space we can
        /// fill with stone - never a non-solid tile like furniture or a chest (placing stone there would corrupt it).
        /// </summary>
        private static bool IsCleanSpot(Point origin)
        {
            for (int dx = 0; dx <= 1; dx++)
            {
                int sx = origin.X + dx;
                int sy = origin.Y + 2;
                if (!InBounds(sx, sy))
                    return false;
                Tile t = Main.tile[sx, sy];
                if (t.HasTile && !Main.tileSolid[t.TileType])
                    return false; // furniture / chest / other non-solid tile under the grave - avoid this spot
            }
            return true;
        }

        private static bool BodyIsAirAndDry(int x, int y)
        {
            for (int dx = 0; dx <= 1; dx++)
            {
                for (int dy = 0; dy <= 1; dy++)
                {
                    int tx = x + dx;
                    int ty = y + dy;
                    if (!InBounds(tx, ty))
                        return false;
                    Tile t = Main.tile[tx, ty];
                    if (t.HasTile || t.LiquidAmount > 0)
                        return false;
                }
            }
            return true;
        }

        // ---- tombstone tile placement ---------------------------------------------------------------------

        private static void PlaceFreshTombstone(Point origin, int style)
        {
            for (int dx = 0; dx <= 1; dx++)
            {
                for (int dy = 0; dy <= 1; dy++)
                {
                    if (!InBounds(origin.X + dx, origin.Y + dy))
                        continue;

                    Tile t = Main.tile[origin.X + dx, origin.Y + dy];
                    t.ClearTile(); // reset the block layer only - keep the background wall (tombstones don't conflict with walls)
                    t.HasTile = true;
                    t.TileType = TileID.Tombstones;
                    t.TileFrameX = (short)(style * 36 + dx * 18);
                    t.TileFrameY = (short)(dy * 18);
                    t.LiquidAmount = 0;
                }
            }
        }

        // ---- sign handling --------------------------------------------------------------------------------

        private static void CreateSign(int x, int y, string text)
        {
            // Already present?
            for (int i = 0; i < Main.sign.Length; i++)
            {
                if (Main.sign[i] != null && Main.sign[i].x == x && Main.sign[i].y == y)
                {
                    Main.sign[i].text = text ?? "";
                    return;
                }
            }

            for (int i = 0; i < Main.sign.Length; i++)
            {
                if (Main.sign[i] == null)
                {
                    Main.sign[i] = new Sign { x = x, y = y, text = text ?? "" };
                    return;
                }
            }
        }

        // ---- sealing --------------------------------------------------------------------------------------

        /// <summary>
        /// Clears liquid out of the gravestone body, puts a stone floor underneath it, and walls off any liquid
        /// still touching the sides/top with bubble blocks (which the player can walk through).
        /// </summary>
        private static void SealAndSupport(Point origin, List<TempBlock> temp)
        {
            int x = origin.X;
            int y = origin.Y;

            // Dry out the body tiles themselves.
            for (int dx = 0; dx <= 1; dx++)
            {
                for (int dy = 0; dy <= 1; dy++)
                {
                    if (InBounds(x + dx, y + dy))
                        Main.tile[x + dx, y + dy].LiquidAmount = 0;
                }
            }

            // Stone floor directly under the 2x2 (row y+2).
            PlaceSupport(x, y + 2, temp);
            PlaceSupport(x + 1, y + 2, temp);

            bool useBubbles = GraveyardStorageServerConfig.Instance?.UseBubbleBlocksInLiquid ?? true;
            if (useBubbles)
            {
                // Side columns.
                WallIfLiquid(x - 1, y, temp);
                WallIfLiquid(x - 1, y + 1, temp);
                WallIfLiquid(x + 2, y, temp);
                WallIfLiquid(x + 2, y + 1, temp);
                // Top row.
                WallIfLiquid(x, y - 1, temp);
                WallIfLiquid(x + 1, y - 1, temp);
            }
        }

        /// <summary>
        /// Fills an empty tile at (x, y) with temporary stone to support the gravestone. Any existing tile (solid
        /// ground, or furniture / chests) is left untouched - we never overwrite tiles, only fill open space.
        /// </summary>
        private static void PlaceSupport(int x, int y, List<TempBlock> temp)
        {
            if (!InBounds(x, y))
                return;

            Tile t = Main.tile[x, y];
            if (t.HasTile)
                return; // occupied (solid ground, furniture, chest, ...) - never overwrite

            t.HasTile = true;
            t.TileType = TileID.Stone;
            t.TileFrameX = 0;
            t.TileFrameY = 0;
            t.LiquidAmount = 0;
            temp.Add(new TempBlock(x, y, TileID.Stone));
        }

        /// <summary>
        /// Places a temporary bubble block into an open tile that currently holds liquid, to wall the liquid off.
        /// Never overwrites an existing tile. Bubble blocks keep liquid out while remaining walk-through.
        /// </summary>
        private static void WallIfLiquid(int x, int y, List<TempBlock> temp)
        {
            if (!InBounds(x, y))
                return;

            Tile t = Main.tile[x, y];
            if (t.LiquidAmount == 0 || t.HasTile)
                return; // only wall off open tiles that hold liquid; never overwrite an existing tile

            t.HasTile = true;
            t.TileType = TileID.Bubble;
            t.TileFrameX = 0;
            t.TileFrameY = 0;
            t.LiquidAmount = 0;
            temp.Add(new TempBlock(x, y, TileID.Bubble));
        }

        // ---- misc -----------------------------------------------------------------------------------------

        private static void SyncArea(Point origin)
        {
            if (Main.netMode == NetmodeID.SinglePlayer)
                return;

            int left = System.Math.Max(0, origin.X - 2);
            int top = System.Math.Max(0, origin.Y - 2);
            NetMessage.SendTileSquare(-1, left, top, 6, 7);
        }

        private static bool InBounds(int x, int y)
        {
            return x >= 0 && x < Main.maxTilesX && y >= 0 && y < Main.maxTilesY;
        }
    }
}
