using System.ComponentModel;
using Terraria.ModLoader;
using Terraria.ModLoader.Config;

namespace GraveyardStorage
{
    /// <summary>
    /// Defines how items are placed in the player's inventory when restored from a tombstone.
    /// </summary>
    public enum ItemPlacementMode
    {
        /// <summary>
        /// Only favorited (starred) items will replace items in their original slots.
        /// Other items go to empty slots or drop.
        /// </summary>
        ReplaceStarred,

        /// <summary>
        /// All items will try to replace items in their original slots.
        /// Displaced items are moved to empty slots or dropped.
        /// </summary>
        ReplaceAll,

        /// <summary>
        /// No replacement - items only go to empty slots or drop.
        /// This mode is always used when another player picks up items from your tombstone.
        /// </summary>
        NoReplacement
    }

    /// <summary>
    /// Client-side configuration for Graveyard Storage mod.
    /// </summary>
    public class GraveyardStorageConfig : ModConfig
    {
        public override ConfigScope Mode => ConfigScope.ClientSide;

        public static GraveyardStorageConfig Instance => ModContent.GetInstance<GraveyardStorageConfig>();

        [Header("ItemRestoration")]
        [DefaultValue(ItemPlacementMode.ReplaceStarred)]
        [DrawTicks]
        public ItemPlacementMode PlacementMode { get; set; } = ItemPlacementMode.ReplaceStarred;
    }
}
