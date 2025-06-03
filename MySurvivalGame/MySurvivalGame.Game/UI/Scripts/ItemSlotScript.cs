// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Stride.Core.Mathematics;
using Stride.Engine;
using Stride.Graphics; // For Texture, Sprite
using Stride.UI;
using Stride.UI.Controls; // For ImageElement, TextBlock, ProgressBar
using Stride.UI.Events;   // For PointerEventArgs
using Stride.UI.Panels;   // For Grid (if RootPanel is needed)
using Stride.Input;       // For Input (used in InventoryPanelScript, good to have consistent usings)
using MySurvivalGame.Game.Data.Items; // MODIFIED: For ItemStack, ItemData

namespace MySurvivalGame.Game.UI.Scripts
{
    public class ItemSlotScript : UIScript
    {
        public ImageElement ItemIconImage { get; set; }
        public TextBlock QuantityText { get; set; }
        public ProgressBar DurabilityBar { get; set; }
        
        public UIElement RootElement { get; private set; }

        private bool isDragging = false;
        public Vector2 DragOffset { get; private set; }
        public static ItemSlotScript CurrentlyDraggedSlot { get; private set; }
        public ItemStack CurrentItemStack { get; private set; } // MODIFIED: Property name and type

        private InventoryPanelScript parentPanelScript;

        public override void Start()
        {
            base.Start(); // Important for UIScript initialization

            // Assuming this script is attached to an Entity that has a UIComponent,
            // and that UIComponent's Page is set to the ItemSlot.sdslui.
            // The RootElement of the Page is the Grid "RootPanel".
            RootElement = Entity.Get<UIComponent>()?.Page?.RootElement;

            if (RootElement == null)
            {
                Log.Error("ItemSlotScript: Could not find the root UI element for this slot.");
                return;
            }

            // Find UI elements by name from the root panel
            ItemIconImage = RootElement.FindName<ImageElement>("ItemIconImage");
            QuantityText = RootElement.FindName<TextBlock>("QuantityText");
            DurabilityBar = RootElement.FindName<ProgressBar>("DurabilityBar");

            if (ItemIconImage == null) Log.Error("ItemSlotScript: ItemIconImage not found in UI.");
            if (QuantityText == null) Log.Error("ItemSlotScript: QuantityText not found in UI.");
            if (DurabilityBar == null) Log.Error("ItemSlotScript: DurabilityBar not found in UI.");

            // Initialize slot as empty
            ClearSlot();

            // Find parent InventoryPanelScript
            var current = this.Entity.GetParent();
            while(current != null) 
            {
                parentPanelScript = current.Get<InventoryPanelScript>();
                if (parentPanelScript != null) break;
                current = current.GetParent();
            }
            // A more direct way if InventoryPanelScript is on a known entity (e.g. a root UI entity):
            // parentPanelScript = Entity.Scene.RootEntities.FirstOrDefault(e => e.Get<InventoryPanelScript>() != null)?.Get<InventoryPanelScript>();
            if (parentPanelScript == null) Log.Error($"ItemSlotScript on '{Entity.Name}' could not find parent InventoryPanelScript!");
        }

        /// <summary>
        /// Sets the item data for this slot based on an ItemStack, updating its visual representation.
        /// </summary>
        public void SetItemStack(ItemStack stack)
        {
            this.CurrentItemStack = stack;

            if (stack == null || stack.Item == null)
            {
                ClearSlot();
                return;
            }

            ItemData item = stack.Item;
            
            // Icon
            if (ItemIconImage != null)
            {
                if (!string.IsNullOrEmpty(item.IconPath))
                {
                    // TODO: Implement robust texture loading. For now, log path.
                    // Example: ItemIconImage.Source = new SpriteFromTexture(Content.Load<Texture>(item.IconPath));
                    Log.Info($"ItemSlot '{this.Entity.Name}': SetItemStack - IconPath: {item.IconPath} (Texture loading placeholder)");
                    // For testing, we can clear it or use a placeholder if one exists
                    ItemIconImage.Source = null; // Placeholder: No icon loaded
                    ItemIconImage.Visibility = Visibility.Visible; // Show if there's an item, even if icon fails to load for now
                }
                else
                {
                    ItemIconImage.Source = null;
                    ItemIconImage.Visibility = Visibility.Collapsed;
                }
            }

            // Quantity
            if (QuantityText != null)
            {
                QuantityText.Text = stack.Quantity > 1 ? stack.Quantity.ToString() : "";
                QuantityText.Visibility = stack.Quantity > 1 ? Visibility.Visible : Visibility.Hidden;
            }

            // Durability
            if (DurabilityBar != null)
            {
                if (item.Type == ItemType.Tool || item.Type == ItemType.Weapon)
                {
                    // Assuming max durability is ItemStack.DefaultMaxDurability or from ItemData if available
                    float maxDurability = ItemStack.DefaultMaxDurability; // Or item.ToolData?.MaxDurability ?? ItemStack.DefaultMaxDurability;
                    if (maxDurability > 0)
                    {
                        DurabilityBar.Value = stack.CurrentDurability / maxDurability;
                        // Show if not full durability, or always if it has durability? For now, show if not full.
                        DurabilityBar.Visibility = (stack.CurrentDurability < maxDurability && stack.CurrentDurability > 0) ? Visibility.Visible : Visibility.Collapsed;
                    }
                    else
                    {
                        DurabilityBar.Visibility = Visibility.Collapsed;
                    }
                }
                else
                {
                    DurabilityBar.Visibility = Visibility.Collapsed;
                }
            }
        }
        
        // Helper methods to get current visual data
        public Texture GetIconTexture()
        {
            // This would need to access the loaded texture if ItemIconImage.Source is set.
            // For now, it's challenging without direct Texture reference in CurrentItemStack.Item
            return (ItemIconImage?.Source as SpriteFromTexture)?.Texture;
        }
        public int GetQuantity() => CurrentItemStack?.Quantity ?? 0;
        public float? GetDurabilityNormalized()
        {
            if (CurrentItemStack != null && (CurrentItemStack.Item.Type == ItemType.Tool || CurrentItemStack.Item.Type == ItemType.Weapon))
            {
                float maxDurability = ItemStack.DefaultMaxDurability; // Or from ItemData if available
                return maxDurability > 0 ? CurrentItemStack.CurrentDurability / maxDurability : (float?)null;
            }
            return null;
        }


        public void ClearSlot()
        {
            CurrentItemStack = null;
            if (ItemIconImage != null)
            {
                ItemIconImage.Source = null;
                ItemIconImage.Visibility = Visibility.Collapsed;
            }
            if (QuantityText != null)
            {
                QuantityText.Text = "";
                QuantityText.Visibility = Visibility.Hidden;
            }
            if (DurabilityBar != null)
            {
                DurabilityBar.Visibility = Visibility.Collapsed;
            }
        }

        public override void OnPointerPressed(PointerEventArgs args)
        {
            base.OnPointerPressed(args);

            if (args.MouseButton == MouseButton.Left && CurrentItemStack != null) // MODIFIED: Check CurrentItemStack
            {
                isDragging = true;
                CurrentlyDraggedSlot = this;
                
                // Calculate offset from the top-left of the slot to the mouse click position
                Vector3 slotPosition = RootElement.ActualPosition; // This is relative to parent
                Vector2 absoluteSlotPosition = new Vector2(RootElement.GetAbsolutePosition().X, RootElement.GetAbsolutePosition().Y);
                DragOffset = args.MousePosition - absoluteSlotPosition;

                // Visual indication (handled by InventoryPanelScript now)
                // RootElement.Opacity = 0.7f; 
                
                parentPanelScript?.HandleDragStarted(this, args.MousePosition);
                args.Handled = true;
            }
            else if (args.MouseButton == MouseButton.Right)
            {
                OnRightClick(args);
            }
        }

        public override void OnPointerReleased(PointerEventArgs args)
        {
            base.OnPointerReleased(args);
            // Log.Info($"ItemSlot '{this.Entity.Name}': Pointer Released. Button: {args.MouseButton}");

            if (isDragging && args.MouseButton == MouseButton.Left)
            {
                isDragging = false;
                parentPanelScript?.HandleDragReleased(this, args.MousePosition);
                // Visual reset (handled by InventoryPanelScript now)
                // RootElement.Opacity = 1.0f;
                CurrentlyDraggedSlot = null;
                args.Handled = true;
            }
        }

        public override void OnPointerEnter(PointerEventArgs args)
        {
            base.OnPointerEnter(args);
            // Log.Info($"ItemSlot '{this.Entity.Name}': Pointer Enter.");
            parentPanelScript?.HandleSlotPointerEnter(this);
            // Example: Change background on hover if not dragging something else
            if (CurrentlyDraggedSlot == null && RootElement is Panel panel) { 
                // panel.BackgroundColor = new Color(80,80,80,255); // Hover color
            }
        }

        public override void OnPointerExit(PointerEventArgs args)
        {
            base.OnPointerExit(args);
            // Log.Info($"ItemSlot '{this.Entity.Name}': Pointer Exit.");
            parentPanelScript?.HandleSlotPointerExit(this);
            // Example: Restore background if not dragging this slot
            if (!isDragging && RootElement is Panel panel) {
                // panel.BackgroundColor = new Color(64,64,64,255); // Original color
            }
        }
        
        public void OnRightClick(PointerEventArgs args)
        {
            Log.Info($"ItemSlot '{this.Entity.Name}': Right-Clicked.");
            // Placeholder for context menu or other right-click actions
            args.Handled = true; 
        }

        // OnDestroy or Cancel can be used for cleanup if any event handlers were manually subscribed
        // to UI elements, but for overrides, base.Cancel() handles UIScript related cleanup.
        public override void Cancel()
        {
            // Any custom cleanup
            base.Cancel();
        }
    }
}
