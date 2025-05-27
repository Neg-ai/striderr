// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Stride.Core.Mathematics;
using Stride.Engine;
using Stride.Graphics; 
using Stride.UI;
using Stride.UI.Controls; 
using Stride.UI.Panels;   
using Stride.Input;       
using System.Collections.Generic;
using System.Linq; 

using MySurvivalGame.Data.Items;    // MODIFIED: For ItemData, ItemStack
using MySurvivalGame.Game.Player;   // For PlayerInventoryComponent, PlayerHotbarManager

namespace MySurvivalGame.Game.UI.Scripts 
{
    public class InventoryPanelScript : UIScript
    {
        public Prefab ItemSlotPrefab { get; set; }

        // UI Elements
        private UniformGrid inventoryGrid;
        private StackPanel hotbarPanel;
        private TextBlock healthText; 
        private TextBlock weightText; 
        private ProgressBar staminaBar; // ADDED: For stamina display
        
        private List<ItemSlotScript> inventorySlotScripts = new List<ItemSlotScript>(); 
        private List<ItemSlotScript> hotbarSlotScripts = new List<ItemSlotScript>();    
        private PlayerInventoryComponent playerInventory; 
        private PlayerHotbarManager playerHotbarManager;
        private PlayerStaminaComponent playerStamina; // ADDED: For stamina data

        // Drag Operation
        private ImageElement dragVisual;
        private ItemSlotScript sourceSlotOfDrag;
        private UIElement rootElement; 

        // Tooltip
        private UIElement tooltipPanel;
        private TextBlock itemNameText;
        private TextBlock itemTypeText;
        private TextBlock itemDescriptionText;
        private StackPanel itemStatsPanel; 
        private ItemSlotScript currentlyHoveredSlot = null;
        private float hoverTimer = 0f;
        private const float hoverDelay = 0.5f; 


        public override void Start()
        {
            base.Start();

            rootElement = Entity.Get<UIComponent>()?.Page?.RootElement;
            if (rootElement == null)
            {
                Log.Error("InventoryPanelScript: Root UI element not found.");
                return;
            }

            inventoryGrid = rootElement.FindName<UniformGrid>("InventoryGrid");
            hotbarPanel = rootElement.FindName<StackPanel>("HotbarPanel");
            
            var playerStatsPanel = rootElement.FindName<StackPanel>("PlayerStatsPanel");
            if (playerStatsPanel != null)
            {
                // Assuming Health is child 0, Weight is child 1. StaminaBar needs a name or specific index.
                if (playerStatsPanel.Children.Count >= 2) 
                {
                    healthText = playerStatsPanel.Children[0] as TextBlock;
                    weightText = playerStatsPanel.Children[1] as TextBlock;
                }
                // Find StaminaBar by name (preferred) or index (fallback)
                staminaBar = playerStatsPanel.FindName<ProgressBar>("StaminaBar");
                if (staminaBar == null && playerStatsPanel.Children.Count >= 3) // Fallback if not named and order is known
                {
                    staminaBar = playerStatsPanel.Children[2] as ProgressBar;
                }
                if (staminaBar == null)
                {
                    Log.Warning("InventoryPanelScript: StaminaBar ProgressBar not found in PlayerStatsPanel. Stamina UI will not update.");
                }
            }
            else { Log.Warning("InventoryPanelScript: PlayerStatsPanel not found."); }
            
            if (ItemSlotPrefab == null) { Log.Error("InventoryPanelScript: ItemSlotPrefab is not assigned."); return; }
            if (inventoryGrid == null) { Log.Error("InventoryPanelScript: InventoryGrid not found."); }
            if (hotbarPanel == null) { Log.Error("InventoryPanelScript: HotbarPanel not found."); }

            // Initialize UI slots. Numbers should align with PlayerInventoryComponent.MaxInventorySlots
            // Example: PlayerInventoryComponent.MaxInventorySlots = 20. Hotbar = 8, MainInv = 12.
            int numHotbarSlots = 8; 
            int numMainInventoryGridSlots = 12; 
            
            if (hotbarPanel != null) InitializeHotbar(numHotbarSlots);
            if (inventoryGrid != null) InitializeInventoryGrid(numMainInventoryGridSlots); 
            
            // Find Player components
            var playerEntity = Entity.Scene?.RootEntities.FirstOrDefault(e => e.Name == "Player"); 
            if (playerEntity != null)
            {
                playerInventory = playerEntity.Get<PlayerInventoryComponent>();
                if (playerInventory != null)
                {
                    if (playerInventory.MaxInventorySlots < numHotbarSlots + numMainInventoryGridSlots)
                    {
                        Log.Warning($"InventoryPanelScript: PlayerInventoryComponent.MaxInventorySlots ({playerInventory.MaxInventorySlots}) " +
                                    $"is less than UI configured slots ({numHotbarSlots + numMainInventoryGridSlots}). " +
                                    $"Some UI slots will not be usable or map correctly.");
                    }
                    playerInventory.OnInventoryChanged += RefreshInventoryDisplay;
                    Log.Info("InventoryPanelScript: Successfully subscribed to PlayerInventory.OnInventoryChanged.");
                }
                else { Log.Error("InventoryPanelScript: PlayerInventoryComponent not found on Player entity."); }

                playerHotbarManager = playerEntity.Get<PlayerHotbarManager>();
                if (playerHotbarManager == null) { Log.Error("InventoryPanelScript: PlayerHotbarManager not found on Player entity."); }

                // ADDED: Get PlayerStaminaComponent and subscribe to its event
                playerStamina = playerEntity.Get<PlayerStaminaComponent>();
                if (playerStamina != null && staminaBar != null)
                {
                    playerStamina.OnStaminaChanged += UpdateStaminaUI;
                    UpdateStaminaUI(playerStamina.CurrentStamina, playerStamina.MaxStamina); // Initialize UI
                    Log.Info("InventoryPanelScript: Successfully subscribed to PlayerStamina.OnStaminaChanged.");
                }
                else if (staminaBar != null) // staminaBar exists but component doesn't
                {
                    Log.Warning("InventoryPanelScript: PlayerStaminaComponent not found on Player entity. Stamina UI will not be linked.");
                    staminaBar.Visibility = Visibility.Collapsed; // Hide if no data source
                }
            }
            else { Log.Error("InventoryPanelScript: Player entity not found. Cannot link components."); }
            
            SetupDragVisual();
            SetupTooltipPanel();
            RefreshInventoryDisplay(); 
        }

        private void SetupDragVisual()
        {
            dragVisual = new ImageElement 
            { 
                Name = "DragVisual", 
                Visibility = Visibility.Collapsed, 
                Width = 50, Height = 50, // Slightly smaller than slot
                Stretch = Stretch.Uniform, 
                Margin = new Thickness(0) 
            };
            if (rootElement is Panel panelDrag) 
            {
                panelDrag.Children.Add(dragVisual);
                dragVisual.SetPanelZIndex(1000); 
            }
            else { Log.Error("InventoryPanelScript: RootElement is not a Panel, cannot add dragVisual."); }
        }

        private void SetupTooltipPanel()
        {
            tooltipPanel = rootElement.FindName<UIElement>("TooltipPanel");
            if (tooltipPanel != null)
            {
                itemNameText = tooltipPanel.FindName<TextBlock>("ItemNameText");
                itemTypeText = tooltipPanel.FindName<TextBlock>("ItemTypeText");
                itemDescriptionText = tooltipPanel.FindName<TextBlock>("ItemDescriptionText");
                itemStatsPanel = tooltipPanel.FindName<StackPanel>("ItemStatsPanel");
                tooltipPanel.Visibility = Visibility.Collapsed;
            }
            else { Log.Error("InventoryPanelScript: TooltipPanel not found in UI."); }
        }
        
        public override void Update()
        {
            base.Update();
            UpdateDragVisualPosition();
            HandleTooltipTimer();
            UpdateTooltipPosition();
        }

        private void UpdateDragVisualPosition()
        {
            if (dragVisual?.Visibility == Visibility.Visible && sourceSlotOfDrag != null && Input != null && rootElement != null)
            {
                var localMousePosition = rootElement.ScreenToLocal(Input.MousePosition);
                Vector2 finalPosition = localMousePosition - sourceSlotOfDrag.DragOffset;
                dragVisual.SetCanvasLeft(finalPosition.X);
                dragVisual.SetCanvasTop(finalPosition.Y);
            }
        }

        private void HandleTooltipTimer()
        {
            if (currentlyHoveredSlot != null && tooltipPanel != null && tooltipPanel.Visibility == Visibility.Collapsed)
            {
                if (hoverTimer > 0f)
                {
                    hoverTimer -= (float)Game.UpdateTime.Elapsed.TotalSeconds;
                }
                if (hoverTimer <= 0f)
                {
                    ShowTooltip(currentlyHoveredSlot);
                }
            }
        }
        
        private void UpdateTooltipPosition()
        {
            if (tooltipPanel != null && tooltipPanel.Visibility == Visibility.Visible && Input != null && rootElement != null)
            {
                var localMousePosition = rootElement.ScreenToLocal(Input.MousePosition);
                tooltipPanel.SetCanvasLeft(localMousePosition.X + 15); 
                tooltipPanel.SetCanvasTop(localMousePosition.Y + 15);
            }
        }

        public void InitializeInventoryGrid(int numberOfSlots) 
        {
            if (inventoryGrid == null || ItemSlotPrefab == null) return;
            inventoryGrid.Children.Clear();
            inventorySlotScripts.Clear(); 
            for (int i = 0; i < numberOfSlots; i++) { CreateAndAddSlot(inventoryGrid, inventorySlotScripts, $"InventorySlot_{i}"); }
        }

        public void InitializeHotbar(int numberOfSlots) 
        {
            if (hotbarPanel == null || ItemSlotPrefab == null) return;
            hotbarPanel.Children.Clear();
            hotbarSlotScripts.Clear();
            for (int i = 0; i < numberOfSlots; i++) { CreateAndAddSlot(hotbarPanel, hotbarSlotScripts, $"HotbarSlot_{i}"); }
        }

        private void CreateAndAddSlot(Panel parentPanel, List<ItemSlotScript> scriptList, string entityNamePrefix)
        {
            var itemSlotEntity = ItemSlotPrefab.Instantiate().FirstOrDefault();
            if (itemSlotEntity == null)
            {
                Log.Error($"InventoryPanelScript: Failed to instantiate ItemSlotPrefab for {entityNamePrefix}.");
                return;
            }
            itemSlotEntity.Name = entityNamePrefix + scriptList.Count;

            var itemSlotScript = itemSlotEntity.Get<ItemSlotScript>();
            if (itemSlotScript == null) { Log.Error($"InventoryPanelScript: ItemSlotScript not found on prefab {itemSlotEntity.Name}."); }
            
            if (itemSlotEntity.Scene == null) { this.Entity.AddChild(itemSlotEntity); }

            var uiComponent = itemSlotEntity.Get<UIComponent>();
            if (uiComponent?.Page?.RootElement != null)
            {
                parentPanel.Children.Add(uiComponent.Page.RootElement); 
                scriptList.Add(itemSlotScript); 
            }
            else { Log.Error($"InventoryPanelScript: Prefab {itemSlotEntity.Name} is missing UIComponent or Page setup."); }
        }

        public void UpdatePlayerStats(float currentHealth, float maxHealth, float currentWeight, float maxWeight)
        {
            if (healthText != null) healthText.Text = $"Health: {currentHealth}/{maxHealth}";
            if (weightText != null) weightText.Text = $"Weight: {currentWeight:F1}/{maxWeight:F1} kg";
        }

        public void HandleDragStarted(ItemSlotScript slot, Vector2 initialMousePosition)
        {
            if (slot == null || slot.CurrentItemStack == null) return;

            if (currentlyHoveredSlot != slot && tooltipPanel != null)
            {
                tooltipPanel.Visibility = Visibility.Collapsed;
                currentlyHoveredSlot = null; 
            }
            sourceSlotOfDrag = slot;
            
            if (slot.CurrentItemStack?.Item?.IconPath != null)
            {
                // TODO: Implement robust texture loading for dragVisual.Source
                Log.Info($"InventoryPanelScript: DragStarted - IconPath: {slot.CurrentItemStack.Item.IconPath} (Texture loading placeholder for dragVisual)");
                dragVisual.Source = null; // Placeholder until texture loading is sorted
                dragVisual.Visibility = Visibility.Visible;
            }
            else { dragVisual.Visibility = Visibility.Collapsed; }

            var localMousePosition = rootElement.ScreenToLocal(initialMousePosition);
            Vector2 finalPosition = localMousePosition - sourceSlotOfDrag.DragOffset;
            dragVisual.SetCanvasLeft(finalPosition.X);
            dragVisual.SetCanvasTop(finalPosition.Y);

            if (sourceSlotOfDrag.RootElement != null) { sourceSlotOfDrag.RootElement.Opacity = 0.5f; }
            Log.Info($"Drag started from slot: {GetSlotIdentifier(sourceSlotOfDrag)} with item '{sourceSlotOfDrag.CurrentItemStack?.Item?.ItemName ?? "Empty"}'.");
        }

        public void HandleDragReleased(ItemSlotScript originalSourceSlot, Vector2 dropScreenPosition)
        {
            dragVisual.Visibility = Visibility.Collapsed;
            if (sourceSlotOfDrag == null || originalSourceSlot != sourceSlotOfDrag) 
            {
                if (originalSourceSlot?.RootElement != null) originalSourceSlot.RootElement.Opacity = 1.0f; 
                sourceSlotOfDrag = null;
                return;
            }

            if (sourceSlotOfDrag.RootElement != null) { sourceSlotOfDrag.RootElement.Opacity = 1.0f; }

            ItemSlotScript targetSlotScript = FindSlotAtScreenPosition(dropScreenPosition);

            if (targetSlotScript != null && targetSlotScript != sourceSlotOfDrag)
            {
                int sourceIndex = GetGlobalSlotIndex(sourceSlotOfDrag);
                int targetIndex = GetGlobalSlotIndex(targetSlotScript);

                if (sourceIndex != -1 && targetIndex != -1 && playerInventory != null)
                {
                    Log.Info($"Dropped item from slot index {sourceIndex} ('{sourceSlotOfDrag.CurrentItemStack?.Item?.ItemName}') onto slot index {targetIndex} ('{targetSlotScript.CurrentItemStack?.Item?.ItemName}'). Attempting swap.");
                    playerInventory.SwapSlots(sourceIndex, targetIndex);
                    // RefreshInventoryDisplay() is called by PlayerInventory.OnInventoryChanged event
                }
                else { Log.Warning($"Could not determine valid indices for drag and drop. Source: {sourceIndex}, Target: {targetIndex}"); }
            }
            else { Log.Info($"Item drop from '{GetSlotIdentifier(sourceSlotOfDrag)}' was not on a valid different slot."); }
            sourceSlotOfDrag = null;
        }

        private ItemSlotScript FindSlotAtScreenPosition(Vector2 screenPosition)
        {
            foreach (var slotScript in hotbarSlotScripts)
            {
                if (slotScript?.RootElement != null && slotScript.RootElement.IsVisible && slotScript.RootElement.GetAbsoluteBounds().Contains(screenPosition))
                    return slotScript;
            }
            foreach (var slotScript in inventorySlotScripts)
            {
                if (slotScript?.RootElement != null && slotScript.RootElement.IsVisible && slotScript.RootElement.GetAbsoluteBounds().Contains(screenPosition))
                    return slotScript;
            }
            return null;
        }

        private string GetSlotIdentifier(ItemSlotScript slotScript)
        {
            if (slotScript == null) return "UnknownSlot";
            int index = hotbarSlotScripts.IndexOf(slotScript);
            if (index != -1) return $"Hotbar_{index}";
            index = inventorySlotScripts.IndexOf(slotScript);
            if (index != -1) return $"Inventory_{index}"; 
            return slotScript.Entity.Name; 
        }
        
        private int GetGlobalSlotIndex(ItemSlotScript slotScript)
        {
            if (slotScript == null || playerInventory == null) return -1;

            int uiIndex = hotbarSlotScripts.IndexOf(slotScript);
            if (uiIndex != -1) 
            {
                if (uiIndex < playerInventory.MaxInventorySlots) return uiIndex;
                Log.Error($"Hotbar slot script {slotScript.Entity.Name} with UI index {uiIndex} is out of PlayerInventory bounds ({playerInventory.MaxInventorySlots}).");
                return -1;
            }

            uiIndex = inventorySlotScripts.IndexOf(slotScript);
            if (uiIndex != -1)
            {
                int globalIndex = hotbarSlotScripts.Count + uiIndex;
                if (globalIndex < playerInventory.MaxInventorySlots) return globalIndex;
                Log.Error($"Inventory slot script {slotScript.Entity.Name} with UI index {uiIndex} (global {globalIndex}) is out of PlayerInventory bounds ({playerInventory.MaxInventorySlots}).");
                return -1;
            }
            Log.Warning($"InventoryPanelScript: Slot {slotScript.Entity.Name} not found in UI slot lists for indexing.");
            return -1;
        }

        public void HandleSlotPointerEnter(ItemSlotScript slot)
        {
            if (sourceSlotOfDrag != null) return; 
            currentlyHoveredSlot = slot;
            hoverTimer = hoverDelay; 
        }

        public void HandleSlotPointerExit(ItemSlotScript slot)
        {
            if (currentlyHoveredSlot == slot)
            {
                currentlyHoveredSlot = null;
                hoverTimer = 0f; 
                if (tooltipPanel != null) tooltipPanel.Visibility = Visibility.Collapsed;
            }
        }

        private void ShowTooltip(ItemSlotScript slotScript)
        {
            if (slotScript?.CurrentItemStack?.Item == null || tooltipPanel == null)
            {
                if (tooltipPanel != null) tooltipPanel.Visibility = Visibility.Collapsed;
                return;
            }

            ItemData item = slotScript.CurrentItemStack.Item;
            itemNameText.Text = item.ItemName ?? "Unknown Item";
            itemTypeText.Text = item.Type.ToString(); 
            itemDescriptionText.Text = item.Description ?? "No description available.";

            if (itemStatsPanel != null)
            {
                itemStatsPanel.Children.Clear(); 
                if (item.WeaponData != null)
                {
                    itemStatsPanel.Children.Add(new TextBlock { Text = $"Damage: {item.WeaponData.Damage}", TextColor = Color.White, Margin = new Thickness(0,2,0,0) });
                    if (item.WeaponData.ClipSize > 0)
                        itemStatsPanel.Children.Add(new TextBlock { Text = $"Clip: {item.WeaponData.ClipSize}", TextColor = Color.White, Margin = new Thickness(0,2,0,0) });
                }
                if (item.ToolData != null)
                {
                     itemStatsPanel.Children.Add(new TextBlock { Text = $"Efficiency: {item.ToolData.Efficiency}", TextColor = Color.White, Margin = new Thickness(0,2,0,0) });
                }
                 if (item.ConsumableData != null)
                {
                    if(item.ConsumableData.HealthChange != 0) itemStatsPanel.Children.Add(new TextBlock { Text = $"Health: {item.ConsumableData.HealthChange:+#;-#;0}", TextColor = Color.White, Margin = new Thickness(0,2,0,0) });
                    if(item.ConsumableData.HungerChange != 0) itemStatsPanel.Children.Add(new TextBlock { Text = $"Hunger: {item.ConsumableData.HungerChange:+#;-#;0}", TextColor = Color.White, Margin = new Thickness(0,2,0,0) });
                    if(item.ConsumableData.ThirstChange != 0) itemStatsPanel.Children.Add(new TextBlock { Text = $"Thirst: {item.ConsumableData.ThirstChange:+#;-#;0}", TextColor = Color.White, Margin = new Thickness(0,2,0,0) });
                }
                itemStatsPanel.Visibility = itemStatsPanel.Children.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
            }
            tooltipPanel.Visibility = Visibility.Visible;
        }

        private void RefreshInventoryDisplay()
        {
            if (playerInventory == null)
            {
                foreach (var slotScript in hotbarSlotScripts) { slotScript?.SetItemStack(null); }
                foreach (var slotScript in inventorySlotScripts) { slotScript?.SetItemStack(null); }
                return;
            }

            int hotbarUISlotCount = hotbarSlotScripts.Count;
            for (int i = 0; i < hotbarUISlotCount; i++)
            {
                if (i < playerInventory.InventorySlots.Count)
                {
                    hotbarSlotScripts[i]?.SetItemStack(playerInventory.InventorySlots[i]);
                }
                else { hotbarSlotScripts[i]?.SetItemStack(null); }
            }

            int mainInvUISlotCount = inventorySlotScripts.Count;
            for (int i = 0; i < mainInvUISlotCount; i++)
            {
                int inventoryDataIndex = hotbarUISlotCount + i; 
                if (inventoryDataIndex < playerInventory.InventorySlots.Count)
                {
                    inventorySlotScripts[i]?.SetItemStack(playerInventory.InventorySlots[inventoryDataIndex]);
                }
                else { inventorySlotScripts[i]?.SetItemStack(null); }
            }
            
             if (playerHotbarManager != null)
            {
                for (int i = 0; i < hotbarUISlotCount; i++)
                {
                    if (i < playerInventory.InventorySlots.Count)
                    {
                        playerHotbarManager.UpdateHotbarSlot(i, playerInventory.InventorySlots[i]?.Item);
                    }
                    else { playerHotbarManager.UpdateHotbarSlot(i, null); }
                }
            }
        }

        public override void Cancel()
        {
            if (playerInventory != null)
            {
                playerInventory.OnInventoryChanged -= RefreshInventoryDisplay;
            }
            // ADDED: Unsubscribe from PlayerStaminaComponent event
            if (playerStamina != null && staminaBar != null) 
            {
                playerStamina.OnStaminaChanged -= UpdateStaminaUI;
            }
            base.Cancel();
        }

        private void UpdateStaminaUI(float currentStamina, float maxStamina)
        {
            if (staminaBar != null)
            {
                if (maxStamina > 0)
                {
                    staminaBar.Value = currentStamina / maxStamina; // ProgressBar Value is 0 to 1
                    staminaBar.Visibility = Visibility.Visible; 
                }
                else 
                {
                    staminaBar.Value = 0;
                    staminaBar.Visibility = Visibility.Collapsed;
                }
                // Log.Info($"Stamina UI Updated: {currentStamina}/{maxStamina}"); // Can be spammy
            }
        }
    }
}
