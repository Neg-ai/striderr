// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Stride.Core.Mathematics;
using Stride.Engine;
using Stride.Graphics; // For Texture
using Stride.UI;
using Stride.UI.Controls; // For UniformGrid, TextBlock etc.
using Stride.UI.Panels;   // For StackPanel, Canvas
using Stride.Input;       // For Input.MousePosition
using System.Collections.Generic;
using System.Linq; // For First() method

// Assuming ItemSlotScript is in the same namespace or referenced correctly
// using MySurvivalGame.Game.UI.Scripts; // Self-reference not needed
using MySurvivalGame.Game.Items; // MODIFIED: For MockInventoryItem
using MySurvivalGame.Game.Player; // ADDED: For PlayerHotbarManager

namespace MySurvivalGame.Game.UI.Scripts // MODIFIED: Namespace updated
{
    public class InventoryPanelScript : UIScript
    {
        // Public property for the ItemSlot Prefab - assign in Stride Editor
        public Prefab ItemSlotPrefab { get; set; }

        // Private fields for UI elements
        private UniformGrid inventoryGrid;
        private StackPanel hotbarPanel;
        private TextBlock healthText; 
        private TextBlock weightText; 
        
        private List<ItemSlotScript> inventorySlots = new List<ItemSlotScript>();
        private List<ItemSlotScript> hotbarSlots = new List<ItemSlotScript>();
        // private List<MySurvivalGame.Game.Items.MockInventoryItem> actualPlayerItems = new List<MySurvivalGame.Game.Items.MockInventoryItem>(); // REMOVED: This will be managed by PlayerInventoryComponent
        private PlayerInventoryComponent playerInventory; // ADDED: Reference to the player's actual inventory data
        private List<ItemSlotScript> allManagedSlots = new List<ItemSlotScript>(); 

        // Fields for drag operation
        private ImageElement dragVisual;
        private ItemSlotScript sourceSlotOfDrag;
        private UIElement rootElement; // Cache root element for easier access

        // Fields for tooltip
        private UIElement tooltipPanel;
        private TextBlock itemNameText;
        private TextBlock itemTypeText;
        private TextBlock itemDescriptionText;
        private StackPanel itemStatsPanel; // Optional
        private ItemSlotScript currentlyHoveredSlot = null;
        private float hoverTimer = 0f;
        private const float hoverDelay = 0.5f; // Seconds before tooltip appears

        private PlayerHotbarManager playerHotbarManager; // ADDED: For linking
        private EventReceiver<int> activeHotbarSlotChangedReceiver; // ADDED: For hotbar selection visuals
        private Color defaultHotbarSlotColor = new Color(50, 50, 50, 150); // Example default, assuming slots have a background
        private Color selectedHotbarSlotColor = new Color(200, 200, 100, 200); // Example selected


        public override void Start()
        {
            base.Start();

            rootElement = Entity.Get<UIComponent>()?.Page?.RootElement;
            if (rootElement == null)
            {
                Log.Error("InventoryPanelScript: Root UI element not found. Ensure script is on an entity with UIComponent and Page set.");
                return;
            }

            inventoryGrid = rootElement.FindName<UniformGrid>("InventoryGrid");
            hotbarPanel = rootElement.FindName<StackPanel>("HotbarPanel");
            
            // Find PlayerStatsPanel and then its children for health and weight
            var playerStatsPanel = rootElement.FindName<StackPanel>("PlayerStatsPanel");
            if (playerStatsPanel != null && playerStatsPanel.Children.Count >= 2)
            {
                // This assumes Health is the first TextBlock and Weight is the second.
                // Naming these TextBlocks in XAML (e.g., "HealthValueText", "WeightValueText") and using FindName is more robust.
                healthText = playerStatsPanel.Children[0] as TextBlock;
                weightText = playerStatsPanel.Children[1] as TextBlock;
                if (healthText == null) Log.Warning("InventoryPanelScript: Could not find Health TextBlock in PlayerStatsPanel by index.");
                if (weightText == null) Log.Warning("InventoryPanelScript: Could not find Weight TextBlock in PlayerStatsPanel by index.");
            }
            else
            {
                Log.Warning("InventoryPanelScript: PlayerStatsPanel not found or has insufficient children for health/weight TextBlocks.");
            }


            if (ItemSlotPrefab == null)
            {
                Log.Error("InventoryPanelScript: ItemSlotPrefab is not assigned. Please assign it in the editor.");
                // No point continuing if we can't create slots
                return; 
            }
            if (inventoryGrid == null)
            {
                Log.Error("InventoryPanelScript: InventoryGrid not found in UI.");
            }
            if (hotbarPanel == null)
            {
                Log.Error("InventoryPanelScript: HotbarPanel not found in UI.");
            }

            // Initialize grids
            if (inventoryGrid != null) InitializeInventoryGrid(numberOfSlots: 48); // 6 rows * 8 columns
            if (hotbarPanel != null) InitializeHotbar(numberOfSlots: 8);
            
            // UpdatePlayerStats(currentHealth: 100, maxHealth: 100, currentWeight: 10, maxWeight: 50); // Mock data removed, will be set after playerInventory is fetched
            // PopulateWithMockData(); // MODIFIED: Commented out or removed

            // MODIFIED: Find PlayerInventoryComponent and add test items
            var playerEntityForInventory = Entity.Scene?.RootEntities.FirstOrDefault(e => e.Name == "Player");
            if (playerEntityForInventory != null)
            {
                playerInventory = playerEntityForInventory.Get<PlayerInventoryComponent>();
                if (playerInventory != null)
                {
                    // Add test items using the component's method only if the inventory is currently empty
                    // This population is now handled by PlayerInventoryComponent.Start() itself, including weights.
                    // InventoryPanelScript should primarily reflect the state of PlayerInventoryComponent.
                    // So, the AddItem calls here are redundant if PlayerInventoryComponent.Start() populates.
                    // For safety, keeping them but noting PlayerInventoryComponent.Start() is the source of truth for its own initial items.
                    // If PlayerInventoryComponent.Start already added items and calculated weight, this script will pick it up.
                    if (!playerInventory.AllPlayerItems.Any()) // This might be false if PlayerInventoryComponent.Start() already ran and added items
                    {
                        // This block might not run if PlayerInventoryComponent.Start() already populated.
                        // playerInventory.AddItem(new MockInventoryItem("Wood", "Resource", "A sturdy piece of wood.", null, 30, null, 64, EquipmentType.None, 0.5f));
                        // playerInventory.AddItem(new MockInventoryItem("Stone", "Resource", "A common grey stone.", null, 90, null, 64, EquipmentType.None, 1.0f));
                        // ... etc.
                        // The PlayerInventoryComponent.Start() method has its own AddItem calls.
                        // We will rely on those and the RecalculateCurrentWeight() call within PlayerInventoryComponent.
                    }
                    // Update stats display with values from PlayerInventoryComponent
                    UpdatePlayerStats(100, 100, playerInventory.CurrentWeight, playerInventory.MaxWeight); // Assuming 100 for health
                }
                else
                {
                     Log.Error("InventoryPanelScript: PlayerInventoryComponent not found on Player entity.");
                     UpdatePlayerStats(100, 100, 0, 50); // Fallback stats display
                }
            }
            else
            {
                Log.Error("InventoryPanelScript: Player entity not found for inventory.");
                UpdatePlayerStats(100, 100, 0, 50); // Fallback stats display
            }
            
            RefreshInventoryDisplay(); 

            // Create and add dragVisual
            dragVisual = new ImageElement 
            { 
                Name = "DragVisual", 
                Visibility = Visibility.Collapsed, 
                Width = 60,  // Slightly smaller than slot for visual feedback
                Height = 60,
                Stretch = Stretch.Uniform, 
                Margin = new Thickness(0) 
            };
            if (rootElement is Panel panelDrag) 
            {
                panelDrag.Children.Add(dragVisual);
                dragVisual.SetPanelZIndex(1000); 
            }
            else
            {
                Log.Error("InventoryPanelScript: RootElement is not a Panel, cannot add dragVisual.");
            }

            // Find Tooltip UI elements (assuming they are part of InventoryPanel.sdslui)
            tooltipPanel = rootElement.FindName<UIElement>("TooltipPanel");
            if (tooltipPanel != null)
            {
                itemNameText = tooltipPanel.FindName<TextBlock>("ItemNameText");
                itemTypeText = tooltipPanel.FindName<TextBlock>("ItemTypeText");
                itemDescriptionText = tooltipPanel.FindName<TextBlock>("ItemDescriptionText");
                itemStatsPanel = tooltipPanel.FindName<StackPanel>("ItemStatsPanel"); // Optional

                if (itemNameText == null) Log.Error("InventoryPanelScript: ItemNameText not found in TooltipPanel.");
                if (itemTypeText == null) Log.Error("InventoryPanelScript: ItemTypeText not found in TooltipPanel.");
                if (itemDescriptionText == null) Log.Error("InventoryPanelScript: ItemDescriptionText not found in TooltipPanel.");
                // No error for itemStatsPanel as it's optional / might be empty.
            }
            else
            {
                Log.Error("InventoryPanelScript: TooltipPanel not found in UI.");
            }

            // ADDED: Find PlayerHotbarManager
            var playerEntity = Entity.Scene?.RootEntities.FirstOrDefault(e => e.Name == "Player");
            if (playerEntity != null)
            {
                playerHotbarManager = playerEntity.Get<PlayerHotbarManager>();
            }
            if (playerHotbarManager == null)
            {
                Log.Error("InventoryPanelScript: Could not find PlayerHotbarManager component in the scene.");
            }

            // Initialize EventReceiver for active hotbar slot changes
            activeHotbarSlotChangedReceiver = new EventReceiver<int>(PlayerHotbarManager.ActiveHotbarSlotChangedEventKey);

            // Set initial hotbar slot colors (all default)
            for (int i = 0; i < hotbarSlots.Count; i++)
            {
                if (hotbarSlots[i]?.RootElement != null)
                {
                    hotbarSlots[i].RootElement.BackgroundColor = defaultHotbarSlotColor;
                }
            }
        }

        public override void Update()
        {
            base.Update();

            // Drag Visual Update
            if (dragVisual?.Visibility == Visibility.Visible && sourceSlotOfDrag != null && Input != null && rootElement != null)
            {
                var localMousePosition = rootElement.ScreenToLocal(Input.MousePosition);
                Vector2 finalPosition = localMousePosition - sourceSlotOfDrag.DragOffset;
                dragVisual.SetCanvasLeft(finalPosition.X);
                dragVisual.SetCanvasTop(finalPosition.Y);
            }

            // Tooltip Hover Logic
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
            
            // Tooltip Position Update (if visible)
            if (tooltipPanel != null && tooltipPanel.Visibility == Visibility.Visible && Input != null && rootElement != null)
            {
                var localMousePosition = rootElement.ScreenToLocal(Input.MousePosition);
                // Basic offset positioning. More advanced logic would ensure it doesn't go off-screen.
                tooltipPanel.SetCanvasLeft(localMousePosition.X + 15); 
                tooltipPanel.SetCanvasTop(localMousePosition.Y + 15);
            }

            // Periodically update displayed weight from PlayerInventoryComponent
            if (playerInventory != null)
            {
                // Assuming health is managed elsewhere or static for this UI scope
                UpdatePlayerStats(100, 100, playerInventory.CurrentWeight, playerInventory.MaxWeight);
            }

            // Handle Hotbar Selection Visuals
            if (activeHotbarSlotChangedReceiver.TryReceive(out int activeSlotIndex))
            {
                for (int i = 0; i < hotbarSlots.Count; i++)
                {
                    var slotScript = hotbarSlots[i];
                    if (slotScript?.RootElement != null) // Ensure slot and its root UI element exist
                    {
                        if (i == activeSlotIndex)
                        {
                            slotScript.RootElement.BackgroundColor = selectedHotbarSlotColor;
                        }
                        else
                        {
                            slotScript.RootElement.BackgroundColor = defaultHotbarSlotColor;
                        }
                    }
                }
            }
        }


        public void InitializeInventoryGrid(int numberOfSlots)
        {
            if (inventoryGrid == null || ItemSlotPrefab == null) return;

            inventoryGrid.Children.Clear();
            inventorySlots.Clear();
            allManagedSlots.Clear(); // MODIFIED: Clear the consolidated list here

            for (int i = 0; i < numberOfSlots; i++)
            {
                var itemSlotInstanceResult = ItemSlotPrefab.Instantiate();
                if (itemSlotInstanceResult == null || !itemSlotInstanceResult.Any())
                {
                    Log.Error($"InventoryPanelScript: Failed to instantiate ItemSlotPrefab for inventory slot {i}.");
                    continue;
                }
                var itemSlotEntity = itemSlotInstanceResult.First();
                
                var itemSlotScript = itemSlotEntity?.Get<ItemSlotScript>(); // Get the script component from the new entity
                if (itemSlotScript == null)
                {
                     // If ItemSlotScript is not directly on the root of the prefab,
                     // but the UI Page is, this might need adjustment.
                     // However, ItemSlotScript.Start() assumes it's on the entity with the UIComponent.
                     // This implies the ItemSlotPrefab should be an entity with a UIComponent (hosting ItemSlot.sdslui)
                     // AND the ItemSlotScript.
                    Log.Error($"InventoryPanelScript: ItemSlotScript not found on instantiated prefab for inventory slot {i}. Ensure prefab has the script.");
                    // As a fallback, if the script is expected to be added manually or is on a child, this needs different handling.
                    // For now, we assume the script is on the root of the instantiated prefab.
                    // If the prefab is JUST the UI, then we'd have to create a new Entity, add UIComponent, set Page, add script.
                    // The current setup implies ItemSlotPrefab is an EntityPrefab with script and UIComponent.
                    // If itemSlotEntity is the UIElement itself (e.g. Grid from ItemSlot.sdslui), this is wrong.
                    // ItemSlotPrefab.Instantiate() returns List<Entity>.
                    // We need to add this itemSlotEntity to the scene for its scripts to run.
                    // However, for UI, we add its RootElement to the parent UI.
                    // The ItemSlotScript should be on itemSlotEntity.
                    // Adding the entity to the scene allows its scripts (like ItemSlotScript) to run their Start/Update.
                    if(itemSlotEntity.Scene == null) this.Entity.Scene?.Entities.Add(itemSlotEntity); 
                }

                var uiComponent = itemSlotEntity.Get<UIComponent>();
                if (uiComponent?.Page?.RootElement != null)
                {
                    inventoryGrid.Children.Add(uiComponent.Page.RootElement); // Add UI root to grid
                    inventorySlots.Add(itemSlotScript);
                    allManagedSlots.Add(itemSlotScript); // MODIFIED: Add to consolidated list
                    itemSlotScript?.ClearSlot(); // Call ClearSlot on the script
                }
                else
                {
                     Log.Error($"InventoryPanelScript: Instantiated ItemSlotPrefab for inventory slot {i} is missing UIComponent or Page setup.");
                }
            }
        }

        public void InitializeHotbar(int numberOfSlots)
        {
            if (hotbarPanel == null || ItemSlotPrefab == null) return;

            hotbarPanel.Children.Clear();
            hotbarSlots.Clear();

            for (int i = 0; i < numberOfSlots; i++)
            {
                var itemSlotInstanceResult = ItemSlotPrefab.Instantiate();
                 if (itemSlotInstanceResult == null || !itemSlotInstanceResult.Any())
                {
                    Log.Error($"InventoryPanelScript: Failed to instantiate ItemSlotPrefab for hotbar slot {i}.");
                    continue;
                }
                var itemSlotEntity = itemSlotInstanceResult.First();
                var itemSlotScript = itemSlotEntity?.Get<ItemSlotScript>();

                if (itemSlotScript == null)
                {
                    Log.Error($"InventoryPanelScript: ItemSlotScript not found on instantiated prefab for hotbar slot {i}.");
                }
                if(itemSlotEntity.Scene == null) this.Entity.Scene?.Entities.Add(itemSlotEntity);
                
                var uiComponentHotbar = itemSlotEntity.Get<UIComponent>();
                if (uiComponentHotbar?.Page?.RootElement != null)
                {
                    hotbarPanel.Children.Add(uiComponentHotbar.Page.RootElement); // Add UI root to panel
                    hotbarSlots.Add(itemSlotScript);
                    allManagedSlots.Add(itemSlotScript); // MODIFIED: Add to consolidated list
                    itemSlotScript?.ClearSlot();
                }
                else
                {
                    Log.Error($"InventoryPanelScript: Instantiated ItemSlotPrefab for hotbar slot {i} is missing UIComponent or Page setup.");
                }
            }
        }

        public void UpdatePlayerStats(float currentHealth, float maxHealth, float currentWeight, float maxWeight)
        {
            if (healthText != null) healthText.Text = $"Health: {currentHealth}/{maxHealth}";
            if (weightText != null) weightText.Text = $"Weight: {currentWeight:F1}/{maxWeight:F1} kg"; // F1 for one decimal place
        }

        public void PopulateWithMockData()
        {
            // For mock textures:
            // Texture mockWoodIcon = null; // Content.Load<Texture>("PathToWoodIcon");
            // Texture mockStoneIcon = null; // Content.Load<Texture>("PathToStoneIcon");
            // Texture mockToolIcon = null; // Content.Load<Texture>("PathToToolIcon");
            
            // Worker Note: Cannot load textures without specific paths or editor context.
            // Using null for textures for now. The ItemSlotScript handles null textures for the icon itself.
            // MockInventoryItem constructor also handles null texture for its Icon property.
            // Added weight: Wood (0.5f), Stone (1.0f), Axe (1.5f), Potion (0.2f)

            if (inventorySlots.Count > 0 && inventorySlots[0] != null)
                inventorySlots[0].SetItemData(null, 50, null, 
                    new MockInventoryItem("Wood", "Resource", "A sturdy piece of wood.", null, 50, null, 64, EquipmentType.None, 0.5f));
            if (inventorySlots.Count > 1 && inventorySlots[1] != null)
                inventorySlots[1].SetItemData(null, 100, null, 
                    new MockInventoryItem("Stone", "Resource", "A common grey stone.", null, 100, null, 64, EquipmentType.None, 1.0f));
            if (inventorySlots.Count > 3 && inventorySlots[3] != null)
                inventorySlots[3].SetItemData(null, 1, 0.75f, 
                    new MockInventoryItem("Iron Axe", "Tool", "A basic axe for chopping wood. Durability: 75%", null, 1, 0.75f, 1, EquipmentType.Tool, 1.5f));

            if (hotbarSlots.Count > 0 && hotbarSlots[0] != null)
                hotbarSlots[0].SetItemData(null, 1, 0.75f, 
                    new MockInventoryItem("Iron Axe", "Tool", "A basic axe for chopping wood. Durability: 75%", null, 1, 0.75f, 1, EquipmentType.Tool, 1.5f));
            if (hotbarSlots.Count > 1 && hotbarSlots[1] != null)
                hotbarSlots[1].SetItemData(null, 10, 0.5f, 
                    new MockInventoryItem("Health Potion", "Consumable", "Restores a small amount of health. Durability indicates charges.", null, 10, 0.5f, 10, EquipmentType.Consumable, 0.2f));
                
            // Using null for textures for now. The ItemSlotScript handles null textures for the icon itself.
            // MockInventoryItem constructor also handles null texture for its Icon property.

            if (inventorySlots.Count > 0 && inventorySlots[0] != null)
                inventorySlots[0].SetItemData(null, 50, null, 
                    new MockInventoryItem("Wood", "Resource", "A sturdy piece of wood.", null, 50, null, 64, EquipmentType.None, 0.5f));
            if (inventorySlots.Count > 1 && inventorySlots[1] != null)
                inventorySlots[1].SetItemData(null, 100, null, 
                    new MockInventoryItem("Stone", "Resource", "A common grey stone.", null, 100, null, 64, EquipmentType.None, 1.0f));
            if (inventorySlots.Count > 3 && inventorySlots[3] != null)
                inventorySlots[3].SetItemData(null, 1, 0.75f, 
                    new MockInventoryItem("Iron Axe", "Tool", "A basic axe for chopping wood. Durability: 75%", null, 1, 0.75f, 1, EquipmentType.Tool, 1.5f));

            if (hotbarSlots.Count > 0 && hotbarSlots[0] != null)
                hotbarSlots[0].SetItemData(null, 1, 0.75f, 
                    new MockInventoryItem("Iron Axe", "Tool", "A basic axe for chopping wood. Durability: 75%", null, 1, 0.75f, 1, EquipmentType.Tool, 1.5f));
            if (hotbarSlots.Count > 1 && hotbarSlots[1] != null)
                hotbarSlots[1].SetItemData(null, 10, 0.5f, 
                    new MockInventoryItem("Health Potion", "Consumable", "Restores a small amount of health. Durability indicates charges.", null, 10, 0.5f, 10, EquipmentType.Consumable, 0.2f));
                
            Log.Info("InventoryPanelScript: Populated with mock item data (using MockInventoryItem instances).");
        }

        {
            if (slot == null || slot.ItemData == null) return;

            // If a tooltip is showing for a different slot, hide it immediately
            if (currentlyHoveredSlot != slot && tooltipPanel != null)
            {
                tooltipPanel.Visibility = Visibility.Collapsed;
                currentlyHoveredSlot = null; // Clear hover state as drag takes precedence
            }


            sourceSlotOfDrag = slot;
            
            if (slot.ItemIconImage?.Source != null)
            {
                dragVisual.Source = slot.ItemIconImage.Source;
                dragVisual.Visibility = Visibility.Visible;
            }
            else
            {
                dragVisual.Source = null; 
                dragVisual.Visibility = Visibility.Visible; 
            }

            var localMousePosition = rootElement.ScreenToLocal(initialMousePosition);
            Vector2 finalPosition = localMousePosition - sourceSlotOfDrag.DragOffset;
            dragVisual.SetCanvasLeft(finalPosition.X);
            dragVisual.SetCanvasTop(finalPosition.Y);

            if (sourceSlotOfDrag.RootElement != null)
            {
                sourceSlotOfDrag.RootElement.Opacity = 0.5f;
            }
            Log.Info($"Drag started from slot: {sourceSlotOfDrag.Entity.Name}");
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

            if (sourceSlotOfDrag.RootElement != null)
            {
                sourceSlotOfDrag.RootElement.Opacity = 1.0f;
            }

            ItemSlotScript targetSlot = FindSlotAtScreenPosition(dropScreenPosition);

            if (targetSlot != null && targetSlot != sourceSlotOfDrag)
            {
                Log.Info($"Dropped item from '{sourceSlotOfDrag.Entity.Name}' onto '{targetSlot.Entity.Name}'. Performing mock swap.");

                // Perform mock item data swap
                MockInventoryItem sourceItem = sourceSlotOfDrag.ItemData;
                // Visuals are part of ItemData now for icon, but SetItemData handles it
                
                MockInventoryItem targetItem = targetSlot.ItemData;

                // Use the ItemData's properties to set the new slot.
                // ItemSlotScript.SetItemData will update visuals from the MockInventoryItem.
                targetSlot.SetItemData(sourceItem?.Icon, sourceItem?.Quantity ?? 0, sourceItem?.Durability, sourceItem);
                sourceSlotOfDrag.SetItemData(targetItem?.Icon, targetItem?.Quantity ?? 0, targetItem?.Durability, targetItem);

                // MODIFIED: Update playerInventory.AllPlayerItems to reflect the swap
                if (playerInventory != null)
                {
                    playerInventory.AllPlayerItems.Clear(); // Clear the backend data
                    foreach(var slot in allManagedSlots) // Repopulate from the UI state
                    {
                        if (slot?.ItemData != null)
                        {
                            // Ensure we are adding the actual ItemData instance that is now in the slot
                            playerInventory.AllPlayerItems.Add(slot.ItemData); 
                        }
                    }
                }
            }
            else
            {
                Log.Info($"Item drop from '{sourceSlotOfDrag.Entity.Name}' was not on a valid different slot. Item returned.");
            }
            sourceSlotOfDrag = null;
            // playerInventory.AllPlayerItems is now synced with allManagedSlots.
            // Now, ensure PlayerHotbarManager is synced with the state of the hotbarSlots list.
            if (playerHotbarManager != null)
            {
                for (int i = 0; i < hotbarSlots.Count; i++)
                {
                    playerHotbarManager.UpdateHotbarSlot(i, hotbarSlots[i]?.ItemData);
                }
            }
            RefreshInventoryDisplay(); // Final refresh to ensure UI matches playerInventory (which should be authoritative)
        }

        private ItemSlotScript FindSlotAtScreenPosition(Vector2 screenPosition)
        {
            var allSlots = inventorySlots.Concat(hotbarSlots); // Combine both lists for checking
            foreach (var slot in allSlots)
            {
                if (slot?.RootElement != null && slot.RootElement.IsVisible && slot.RootElement.GetAbsoluteBounds().Contains(screenPosition))
                {
                    return slot;
                }
            }
            return null;
        }

        // --- Tooltip Handling Methods ---
        public void HandleSlotPointerEnter(ItemSlotScript slot)
        {
            if (sourceSlotOfDrag != null) return; // Don't show tooltip while dragging

            currentlyHoveredSlot = slot;
            hoverTimer = hoverDelay; 
            // Tooltip visibility is handled in Update based on timer
        }

        public void HandleSlotPointerExit(ItemSlotScript slot)
        {
            if (currentlyHoveredSlot == slot)
            {
                currentlyHoveredSlot = null;
                hoverTimer = 0f; // Reset timer
                if (tooltipPanel != null) tooltipPanel.Visibility = Visibility.Collapsed;
            }
        }

        private void ShowTooltip(ItemSlotScript slot)
        {
            if (slot?.ItemData == null || tooltipPanel == null)
            {
                if (tooltipPanel != null) tooltipPanel.Visibility = Visibility.Collapsed;
                return;
            }

            MockInventoryItem item = slot.ItemData;
            itemNameText.Text = item.Name ?? "Unknown Item";
            itemTypeText.Text = item.ItemType ?? "Unknown Type";
            itemDescriptionText.Text = item.Description ?? "No description available.";

            // Example of populating optional stats panel
            if (itemStatsPanel != null)
            {
                itemStatsPanel.Children.Clear(); // Clear previous stats
                // Example: if item has a dictionary of stats
                // if (item.Stats != null) 
                // {
                //     foreach(var stat in item.Stats)
                //     {
                //         var statText = new TextBlock { Text = $"{stat.Key}: {stat.Value}", TextColor = Color.Khaki, Margin = new Thickness(0,2,0,0) };
                //         itemStatsPanel.Children.Add(statText);
                //     }
                // }
                // For now, we'll just show/hide it based on if there's any hardcoded text or if we had actual stats
                itemStatsPanel.Visibility = itemStatsPanel.Children.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
            }
            
            tooltipPanel.Visibility = Visibility.Visible;
            // Position update is handled in main Update loop
        }

        private void RefreshInventoryDisplay()
        {
            if (playerInventory == null)
            {
                //Log.Error("RefreshInventoryDisplay: playerInventory is null. Cannot refresh UI."); // Can be spammy if Player not found early
                foreach (var slot in allManagedSlots) { slot?.ClearSlot(); } // Clear UI if no backend
                return;
            }

            // Clear all UI slots first
            foreach (var slot in allManagedSlots)
            {
                slot?.ClearSlot();
            }

            // Populate UI slots from the playerInventory.AllPlayerItems list
            for (int i = 0; i < playerInventory.AllPlayerItems.Count; i++)
            {
                if (i < allManagedSlots.Count && playerInventory.AllPlayerItems[i] != null && allManagedSlots[i] != null)
                {
                    var item = playerInventory.AllPlayerItems[i];
                    allManagedSlots[i].SetItemData(item.Icon, item.Quantity, item.Durability, item);
                }
                else if (i >= allManagedSlots.Count)
                {
                    Log.Warning("RefreshInventoryDisplay: More items in player's inventory than available UI slots.");
                    break; 
                }
            }
            // Log.Info("Inventory display refreshed from PlayerInventoryComponent."); // Can be spammy
        }

        public bool AddItemToInventory(MySurvivalGame.Game.Items.MockInventoryItem itemToAdd)
        {
            if (playerInventory == null) 
            {
                Log.Error("AddItemToInventory: playerInventory is null.");
                return false;
            }

            bool success = playerInventory.AddItem(itemToAdd);
            if (success)
            {
                RefreshInventoryDisplay();
            }
            return success;
        }

        public void RemoveItemFromSlot(ItemSlotScript slotToRemoveFrom)
        {
            if (playerInventory == null) 
            {
                Log.Error("RemoveItemFromSlot: playerInventory is null.");
                return;
            }
            if (slotToRemoveFrom?.ItemData != null)
            {
                playerInventory.RemoveItem(slotToRemoveFrom.ItemData);
            }
            else
            {
                Log.Info("RemoveItemFromSlot called on an empty slot or slot without ItemData.");
            }
            RefreshInventoryDisplay();
        }
    }
}
