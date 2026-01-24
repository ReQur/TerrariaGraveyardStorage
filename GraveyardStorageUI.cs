using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.GameContent.UI.Elements;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.UI;

namespace GraveyardStorage
{
    /// <summary>
    /// UI state that adds a "Get Items" button to the sign interface when viewing a gravestone with stored items.
    /// </summary>
    public class GraveyardStorageUIState : UIState
    {
        private UIPanel buttonPanel;
        private UIText buttonText;
        private UIText itemCountText;
        private bool isHovering = false;

        public static LocalizedText GetItemsButtonText { get; private set; }
        public static LocalizedText GetItemsHoverText { get; private set; }
        public static LocalizedText ItemCountText { get; private set; }

        public static void RegisterLocalizations(Mod mod)
        {
            GetItemsButtonText = Language.GetOrRegister(mod.GetLocalizationKey("UI.GetItemsButton"), () => "Get Items");
            GetItemsHoverText = Language.GetOrRegister(mod.GetLocalizationKey("UI.GetItemsHover"), () => "Restore all items to their original inventory slots");
            ItemCountText = Language.GetOrRegister(mod.GetLocalizationKey("UI.ItemCount"), () => "Stored items: {0}");
        }

        public override void OnInitialize()
        {
            // Create item count text (displayed above button)
            itemCountText = new UIText("", 1f);
            itemCountText.HAlign = 0.5f;
            Append(itemCountText);

            // Create the button panel
            buttonPanel = new UIPanel();
            buttonPanel.Width.Set(140f, 0f);
            buttonPanel.Height.Set(40f, 0f);
            buttonPanel.BackgroundColor = new Color(73, 94, 171);
            buttonPanel.BorderColor = new Color(89, 116, 213);
            buttonPanel.OnLeftClick += OnGetItemsClicked;
            buttonPanel.OnMouseOver += OnButtonMouseOver;
            buttonPanel.OnMouseOut += OnButtonMouseOut;

            // Create the button text
            buttonText = new UIText(GetItemsButtonText?.Value ?? "Get Items", 1f);
            buttonText.HAlign = 0.5f;
            buttonText.VAlign = 0.5f;
            buttonPanel.Append(buttonText);

            Append(buttonPanel);
        }

        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);

            if (!GraveyardStorageSystem.IsViewingGravestoneWithItems())
                return;

            // Get item count
            int chestId = GraveyardStorageSystem.GetCurrentGravestoneChestId();
            int itemCount = GravestoneChestSystem.GetItemCount(chestId);
            
            // Update item count text
            string countText = string.Format(ItemCountText?.Value ?? "Stored items: {0}", itemCount);
            itemCountText.SetText(countText);

            // Position elements relative to sign interface
            // The sign interface is typically centered on screen
            // We'll position our button below the sign text area
            float centerX = Main.screenWidth / 2f;
            float signBottomY = Main.screenHeight / 2f + 100f; // Approximate sign bottom
            
            // Position item count text
            itemCountText.Left.Set(centerX - 70f, 0f);
            itemCountText.Top.Set(signBottomY + 10f, 0f);
            
            // Position button below the count text
            buttonPanel.Left.Set(centerX - 70f, 0f);
            buttonPanel.Top.Set(signBottomY + 40f, 0f);
        }

        private void OnGetItemsClicked(UIMouseEvent evt, UIElement listeningElement)
        {
            int chestId = GraveyardStorageSystem.GetCurrentGravestoneChestId();
            if (chestId >= 0)
            {
                SoundEngine.PlaySound(SoundID.Grab);
                
                // Close the sign interface first
                Player player = Main.LocalPlayer;
                player.sign = -1;
                Main.editSign = false;
                Main.npcChatText = string.Empty;
                
                // Restore items
                GravestoneChestSystem.RestoreItemsToPlayer(player, chestId);
                
                // Clear tracking
                VanillaGravestoneGlobalTile.CurrentGravestoneSignWithItems = -1;
                VanillaGravestoneGlobalTile.CurrentGravestonePosition = Point.Zero;
            }
        }

        private void OnButtonMouseOver(UIMouseEvent evt, UIElement listeningElement)
        {
            isHovering = true;
            buttonPanel.BackgroundColor = new Color(100, 130, 200);
            SoundEngine.PlaySound(SoundID.MenuTick);
        }

        private void OnButtonMouseOut(UIMouseEvent evt, UIElement listeningElement)
        {
            isHovering = false;
            buttonPanel.BackgroundColor = new Color(73, 94, 171);
        }

        public override void Draw(SpriteBatch spriteBatch)
        {
            // Only draw when viewing a gravestone with items
            if (!GraveyardStorageSystem.IsViewingGravestoneWithItems())
                return;

            base.Draw(spriteBatch);

            // Draw hover tooltip
            if (isHovering && buttonPanel.ContainsPoint(Main.MouseScreen))
            {
                Main.instance.MouseText(GetItemsHoverText?.Value ?? "Restore all items to their original inventory slots");
            }
        }
    }

    /// <summary>
    /// ModSystem that manages the gravestone UI overlay.
    /// </summary>
    public class GraveyardStorageUISystem : ModSystem
    {
        private UserInterface userInterface;
        private GraveyardStorageUIState uiState;

        public override void Load()
        {
            if (!Main.dedServ)
            {
                GraveyardStorageUIState.RegisterLocalizations(Mod);
                
                userInterface = new UserInterface();
                uiState = new GraveyardStorageUIState();
                uiState.Activate();
            }
        }

        public override void Unload()
        {
            uiState = null;
            userInterface = null;
        }

        public override void UpdateUI(GameTime gameTime)
        {
            if (GraveyardStorageSystem.IsViewingGravestoneWithItems())
            {
                userInterface?.SetState(uiState);
                userInterface?.Update(gameTime);
            }
            else
            {
                userInterface?.SetState(null);
            }
        }

        public override void ModifyInterfaceLayers(List<GameInterfaceLayer> layers)
        {
            // Insert our layer after the inventory layer (which includes sign interface)
            int inventoryIndex = layers.FindIndex(layer => layer.Name.Equals("Vanilla: Inventory"));
            if (inventoryIndex != -1)
            {
                layers.Insert(inventoryIndex + 1, new LegacyGameInterfaceLayer(
                    "GraveyardStorage: Get Items Button",
                    delegate
                    {
                        if (GraveyardStorageSystem.IsViewingGravestoneWithItems())
                        {
                            userInterface?.Draw(Main.spriteBatch, new GameTime());
                        }
                        return true;
                    },
                    InterfaceScaleType.UI
                ));
            }
        }
    }
}
