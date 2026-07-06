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

    /// <summary>
    /// Server-side configuration for Graveyard Storage mod.
    /// These options control how the world is modified when you die, so they must be authoritative on the server
    /// in multiplayer (that is why they live in a separate ServerSide config instead of the client-side one above).
    /// </summary>
    public class GraveyardStorageServerConfig : ModConfig
    {
        public override ConfigScope Mode => ConfigScope.ServerSide;

        public static GraveyardStorageServerConfig Instance => ModContent.GetInstance<GraveyardStorageServerConfig>();

        [Header("GravestoneSafety")]

        /// <summary>
        /// When enabled, the mod guarantees the gravestone survives: it places a temporary stone floor under the
        /// gravestone so it always has something to sit on, moves it up out of any liquid, and (if it is still in a
        /// liquid) surrounds it with temporary bubble blocks. All temporary blocks are removed once the items are
        /// retrieved or the gravestone is destroyed.
        /// </summary>
        [DefaultValue(false)]
        public bool GuaranteeGravestonePlacement { get; set; } = false;

        /// <summary>
        /// When guaranteeing placement, wall off any liquid still touching the gravestone with temporary bubble
        /// blocks. Disable this if you only want the stone floor / relocation behavior.
        /// </summary>
        [DefaultValue(true)]
        public bool UseBubbleBlocksInLiquid { get; set; } = true;
    }
}
