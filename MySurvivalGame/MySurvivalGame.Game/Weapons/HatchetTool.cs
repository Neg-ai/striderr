// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.
// Adapted for MySurvivalGame.

using Stride.Engine;

namespace MySurvivalGame.Game.Weapons
{
    public class HatchetTool : BaseMeleeWeapon
    {
        public override void Start()
        {
            base.Start();
            // Log.Info("HatchetTool script started and attached to an entity.");
            // Specific hatchet initialization can go here if needed in the future.
        }

        // PrimaryAction, SecondaryAction, OnEquip, OnUnequip will be inherited from BaseMeleeWeapon.
        // Override them here if Hatchet needs unique behavior beyond what WeaponToolData defines
        // and BaseMeleeWeapon implements.
    }
}
