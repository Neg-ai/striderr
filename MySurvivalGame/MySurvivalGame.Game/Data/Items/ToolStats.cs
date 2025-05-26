using Stride.Core; // For DataContract and DataMember

namespace MySurvivalGame.Data.Items
{
    [DataContract]
    public class ToolStats
    {
        public enum ToolSpecificType
        {
            Generic, // e.g. a multi-tool or a general repair tool
            Pickaxe,
            Hatchet,
            Drill,
            FishingRod,
            RepairTool
            // Add more as needed
        }

        public enum SpecialBonusResource
        {
            None,
            Wood,
            Stone,
            Metal,
            Flesh, // For harvesting creatures
            Fiber   // For harvesting plants
            // Add more as needed
        }

        /// <summary>
        /// Specific type of the tool, which might influence its primary use or animation.
        /// </summary>
        [DataMember]
        public ToolSpecificType Type { get; set; } = ToolSpecificType.Generic;

        /// <summary>
        /// Base efficiency of the tool, could be used in calculating gathering speed or repair amount.
        /// </summary>
        [DataMember]
        public float Efficiency { get; set; } = 1.0f;

        /// <summary>
        /// The type of resource this tool provides a special bonus for (e.g., a pickaxe for stone).
        /// </summary>
        [DataMember]
        public SpecialBonusResource BonusResource { get; set; } = SpecialBonusResource.None;

        /// <summary>
        /// The multiplier for the bonus resource (e.g., 1.5f for 50% extra stone).
        /// Only applicable if BonusResource is not None.
        /// </summary>
        [DataMember]
        public float BonusMultiplier { get; set; } = 1.0f;

        /// <summary>
        /// Damage dealt when used as an improvised weapon. 
        /// Tools are generally not primary weapons but can still do some damage.
        /// </summary>
        [DataMember]
        public float Damage { get; set; } = 5.0f;

        /// <summary>
        /// Range of the tool in meters (e.g., for melee-style usage).
        /// </summary>
        [DataMember]
        public float Range { get; set; } = 1.5f;
    }
}
