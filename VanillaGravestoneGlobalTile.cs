using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace GraveyardStorage
{
    /// <summary>
    /// GlobalTile that handles vanilla gravestones with stored items.
    /// Lets vanilla sign interface work normally, tracks when a gravestone with items is being viewed.
    /// </summary>
    public class VanillaGravestoneGlobalTile : GlobalTile
    {
        // Track which gravestone (by sign index) the player is currently viewing that has items
        public static int CurrentGravestoneSignWithItems { get; set; } = -1;
        public static Point CurrentGravestonePosition { get; set; } = Point.Zero;

        public override void RightClick(int i, int j, int type)
        {
            // Only handle vanilla tombstones
            if (type != TileID.Tombstones)
                return;

            // Check if this gravestone has stored items
            Point origin = GravestoneChestSystem.FindGravestoneOrigin(i, j);
            
            if (GravestoneChestSystem.HasStorage(origin.X, origin.Y))
            {
                // This gravestone has items - track it for UI
                // The sign interface will open normally via vanilla
                CurrentGravestonePosition = origin;
                
                // Find the sign at this position
                int signIndex = GetSignAtPosition(origin.X, origin.Y);
                if (signIndex >= 0)
                {
                    CurrentGravestoneSignWithItems = signIndex;
                }
            }
            else
            {
                // No items - clear tracking
                CurrentGravestoneSignWithItems = -1;
                CurrentGravestonePosition = Point.Zero;
            }
            
            // Let vanilla handle the sign interface
        }

        /// <summary>
        /// Gets the sign index at a tile position, or -1 if none.
        /// </summary>
        private static int GetSignAtPosition(int x, int y)
        {
            for (int i = 0; i < Main.sign.Length; i++)
            {
                if (Main.sign[i] != null && Main.sign[i].x == x && Main.sign[i].y == y)
                {
                    return i;
                }
            }
            return -1;
        }

        public override void KillTile(int i, int j, int type, ref bool fail, ref bool effectOnly, ref bool noItem)
        {
            // When a gravestone with stored items is destroyed, drop the items
            if (type != TileID.Tombstones)
                return;

            if (!fail && !effectOnly)
            {
                GravestoneChestSystem.OnGravestoneKilled(i, j);
            }
        }
    }
}
