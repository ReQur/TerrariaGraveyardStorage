using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.UI;

namespace GraveyardStorage
{
    /// <summary>
    /// System that manages gravestone item storage behavior.
    /// Tracks when player is viewing a gravestone with stored items.
    /// </summary>
    public class GraveyardStorageSystem : ModSystem
    {
        public static LocalizedText ItemsStoredMessage { get; private set; }

        public override void Load()
        {
            // Register localized text
            ItemsStoredMessage = Language.GetOrRegister(Mod.GetLocalizationKey("Common.ItemsStoredMessage"), 
                () => "Items stored: {0}");
        }

        public override void Unload()
        {
            // Nothing to clean up
        }

        public override void PostUpdateInput()
        {
            // Track when player closes sign interface
            Player player = Main.LocalPlayer;
            
            if (player.sign < 0)
            {
                // Sign closed - clear tracking
                VanillaGravestoneGlobalTile.CurrentGravestoneSignWithItems = -1;
                VanillaGravestoneGlobalTile.CurrentGravestonePosition = Point.Zero;
            }
        }

        /// <summary>
        /// Checks if the player is currently viewing a gravestone with stored items.
        /// </summary>
        public static bool IsViewingGravestoneWithItems()
        {
            Player player = Main.LocalPlayer;
            
            // Check if player has a sign open
            if (player.sign < 0)
                return false;
            
            // Check if this is a gravestone with items
            Point pos = VanillaGravestoneGlobalTile.CurrentGravestonePosition;
            if (pos == Point.Zero)
                return false;
            
            int chestId = GravestoneChestSystem.GetChestForGravestone(pos.X, pos.Y);
            return chestId >= 0;
        }

        /// <summary>
        /// Gets the chest ID for the currently viewed gravestone, or -1 if none.
        /// </summary>
        public static int GetCurrentGravestoneChestId()
        {
            Point pos = VanillaGravestoneGlobalTile.CurrentGravestonePosition;
            if (pos == Point.Zero)
                return -1;
            
            return GravestoneChestSystem.GetChestForGravestone(pos.X, pos.Y);
        }
    }
}
