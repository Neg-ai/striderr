// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.
// Adapted for MySurvivalGame.

using Stride.Engine;
using Stride.Core.Mathematics; // For Vector3
using Stride.Physics;         // For CharacterComponent
using Stride.Animations;      // For AnimationComponent, PlayingAnimation, AnimationClip etc.
using System.Linq;            // For LINQ operations like FirstOrDefault

namespace MySurvivalGame.Game
{
    public class PlayerAnimationController : SyncScript
    {
        public AnimationComponent TargetAnimationComponent { get; set; } // MODIFIED: Renamed property
        public CharacterComponent Character { get; set; }

        // Conceptual names for animations that would be in TargetAnimationComponent.Animations dictionary
        // For this test, we'll use "RifleIdle" for both base idle and additive upper body,
        // and "WalkForward" for movement.
        // MODIFIED: Renamed animation constants as per subtask
        private const string IdleAnim = "Idle";
        private const string WalkAnim = "Walk";
        private const string RunAnim = "Run";

        // Conceptual names for action animations
        private const string JumpStartAnim = "Jump_Start"; // Example
        private const string LightAttackAnim = "Attack_Light"; // Example
        private const string HeavyAttackAnim = "Attack_Heavy"; // Example
        private const string DodgeForwardAnim = "Dodge_Forward"; // Example
        private const string BlockIdleAnim = "Block_Idle"; // Example for blocking stance

        public PlayerController PlayerController { get; set; }

        // Event Receivers for player actions
        private EventReceiver jumpEventReceiver;
        private EventReceiver lightAttackReceiver;
        private EventReceiver heavyAttackReceiver;
        private EventReceiver dodgeReceiver;
        private EventReceiver blockEventReceiver; // Renamed from blockReceiver to avoid conflict with PlayerController's one if any confusion

        public override void Start()
        {
            // Ensure components are linked, preferrably via the editor
            if (TargetAnimationComponent == null)
            {
                TargetAnimationComponent = Entity.Get<AnimationComponent>();
                if (TargetAnimationComponent == null)
                    Log.Error("PlayerAnimationController: TargetAnimationComponent not found on entity or not assigned.");
            }

            // CharacterComponent is expected on the parent entity ("Player")
            var parentEntity = Entity.GetParent();
            if (Character == null && parentEntity != null)
            {
                Character = parentEntity.Get<CharacterComponent>();
                if (Character == null)
                    Log.Error("PlayerAnimationController: CharacterComponent not found on parent entity or not assigned.");
            }

            if (PlayerController == null && parentEntity != null)
            {
                PlayerController = parentEntity.Get<PlayerController>();
                if (PlayerController == null)
                    Log.Warning("PlayerAnimationController: PlayerController not found on parent entity. Run animation speed threshold and stamina checks might not work.");
            }

            // Initialize EventReceivers
            // These assume PlayerInput is broadcasting these events globally or accessible if they were instance based.
            // PlayerInput.JumpEventKey etc. are static.
            jumpEventReceiver = new EventReceiver(PlayerInput.JumpEventKey);
            lightAttackReceiver = new EventReceiver(PlayerInput.LightAttackEventKey);
            heavyAttackReceiver = new EventReceiver(PlayerInput.HeavyAttackEventKey);
            dodgeReceiver = new EventReceiver(PlayerInput.DodgeEventKey);
            blockEventReceiver = new EventReceiver(PlayerInput.BlockEventKey);


            // --- Initial Animation State & Investigation into Layering/Masking ---
            // Stride's AnimationComponent plays AnimationClips. A PlayingAnimation is an instance of an AnimationClip being played.
            // The AnimationComponent has a list of PlayingAnimations.
            //
            // **Primary Question for Investigation:** How to make one PlayingAnimation affect only upper body bones,
            // and another PlayingAnimation affect only lower body bones simultaneously?
            //
            // **1. Bone Masking per PlayingAnimation:**
            //    - Ideal Scenario: `AnimComponent.Play("Walk_Forward", new AnimationPlayParameters { BoneMask = lowerBodyBoneNamesList });`
            //    - Stride's `PlayingAnimation` class does NOT seem to have a direct `BoneMask` property or equivalent
            //      that takes a list of bone names to include/exclude for that specific animation instance *at runtime* when playing.
            //    - `AnimationClip` itself doesn't store a bone mask that `PlayingAnimation` would inherit for a specific playback.
            //
            // **2. Animation Layers (Unity-style):**
            //    - Unity has a concept of layers in its Animator, where each layer can have a weight and an Avatar Mask.
            //    - Stride's `AnimationComponent` has a `BlendOperation` (Additive, Linear) but this applies globally to how
            //      multiple currently playing animations are combined, not to layering with distinct masks per layer.
            //    - There isn't an obvious "Layers" collection on `AnimationComponent` where each layer could have its own mask.
            //
            // **3. Additive Animations & `AnimationBlendOperation.Additive`:**
            //    - This is supported. If "Rifle_Idle_Upper" is a true additive animation (designed to only contain rotations relative
            //      to a base pose, typically the T-pose or another idle), then playing it additively might work IF the lower body
            //      animation ("Walk_Forward") correctly provides the base pose for all bones, and "Rifle_Idle_Upper" only has
            //      meaningful animation data for upper body bones.
            //    - If "Rifle_Idle_Upper" is a standard animation (not additive), playing it additively might lead to undesirable
            //      results (e.g., doubled rotations).
            //
            // **4. `IAnimationBlender` & Custom Blenders:**
            //    - `AnimationComponent` uses an `IAnimationBlender` (default is `AnimationBlender`).
            //    - It might be possible to create a custom `IAnimationBlender` that, during its `Blend` method,
            //      selectively applies bone transformations based on some criteria (e.g., animation name conventions
            //      or custom properties attached to `PlayingAnimation.StateObject` if that's usable).
            //    - This is an advanced approach and would require deep understanding of Stride's animation evaluation pipeline.
            //
            // **5. Pre-Processing Animations:**
            //    - The most straightforward way, if runtime masking is not directly available, is to ensure animations are
            //      authored/exported correctly:
            //        a) "Walk_Forward" should ideally only contain keyframes for lower body bones.
            //        b) "Rifle_Idle_Upper" should ideally only contain keyframes for upper body bones.
            //    - If animations are full-body, they would need to be split in a DCC tool (e.g., Blender) into separate
            //      clips for upper and lower body.
            //
            // **6. Stride `SkeletonUpdater` and `BoneMask` (Low-level internal type):**
            //    - Digging into Stride's source, there's `Stride.Animations.SkeletonUpdater` which uses a `BoneMask`.
            //    - However, this `BoneMask` (a struct of bitfields) seems to be computed internally based on which bones an
            //      `AnimationClip` actually has animation data for. It's not something a user typically sets per `PlayingAnimation`
            //      to restrict a full-body animation to a subset of bones at runtime.
            //
            // **Conclusion for Initial Setup (based on public API) & Investigation Summary:**
            // - Stride's `AnimationComponent` plays `AnimationClip` assets.
            // - For distinct upper/lower body animations from full-body clips, animations generally need to be pre-split
            //   in a DCC tool (e.g., Blender) or authored specifically as additive layers.
            // - Stride does not appear to offer a high-level, built-in runtime bone masking feature on `PlayingAnimation`
            //   to restrict a full-body clip to certain bones for a specific playback instance without custom `IAnimationBlender` development.
            // - Current blending will rely on `AnimationBlendOperation.Additive` for upper body layers,
            //   assuming the animation clips are authored appropriately (either as true additive clips or pre-split).
            // - The system seems to rely more on animations being authored for the parts they should affect.
            // - For this script, we will assume animations *would be* correctly authored (e.g., "Idle_Upper_Additive" only affects upper bones).
            //   Then, `BlendOperation.Additive` for the upper body animation is the most promising approach.

            // Example: Try to play a base idle and an upper body additive idle if available
            // This assumes "Idle_Upper_Additive" is a true additive animation or only affects upper bones.
            // And "Idle_Lower" (or a full body idle) provides the base.
            
            // Ensure animations are present before trying to play
            // Play base idle animation (linear blend)
            if (TargetAnimationComponent?.Animations.ContainsKey(IdleAnim) ?? false)
            {
                 TargetAnimationComponent.Play(IdleAnim, new AnimationPlayParameters { Loop = true, BlendOperation = AnimationBlendOperation.Linear, Key = "BaseLayer" }); // MODIFIED: Using new const and key
            }
            else
            {
                // Log if conceptual "IdleAnim" is not in the TargetAnimationComponent.Animations dictionary
            }

            // Play upper body idle animation (additive blend)
            // For this test, we use the same "RifleIdle" animation conceptually for the additive layer.
            // In a real scenario, this would likely be a separate "RifleIdle_UpperAdditive" clip.
            if (TargetAnimationComponent?.Animations.ContainsKey(IdleAnim) ?? false)
            {
                TargetAnimationComponent.Play(IdleAnim, new AnimationPlayParameters { Loop = true, BlendOperation = AnimationBlendOperation.Additive, Key = "UpperLayer" }); // MODIFIED: Using new const and key
            }
             else
            {
                // Log if conceptual "IdleAnim" for upper body is not in the TargetAnimationComponent.Animations dictionary
            }
        }

        public override void Update()
        {
            if (TargetAnimationComponent == null || Character == null)
                return;

            bool isMoving = Character.Velocity.LengthSquared() > 0.1f;
            float runSpeedThresholdSquared = PlayerController != null ? (PlayerController.MaxRunSpeed * 0.75f) * (PlayerController.MaxRunSpeed * 0.75f) : (5.0f * 0.75f) * (5.0f * 0.75f); // Fallback if no controller
            bool isRunning = isMoving && Character.Velocity.LengthSquared() > runSpeedThresholdSquared;

            var currentBaseAnimation = TargetAnimationComponent.PlayingAnimations.FirstOrDefault(pa => pa.Key == "BaseLayer");
            string targetBaseAnimName = IdleAnim;

            if (isMoving)
            {
                targetBaseAnimName = isRunning ? RunAnim : WalkAnim;
            }

            if (TargetAnimationComponent.Animations.ContainsKey(targetBaseAnimName))
            {
                if (currentBaseAnimation == null || currentBaseAnimation.Name != targetBaseAnimName || !currentBaseAnimation.Enabled)
                {
                    currentBaseAnimation?.Stop();
                    TargetAnimationComponent.Play(targetBaseAnimName, new AnimationPlayParameters { Loop = true, BlendOperation = AnimationBlendOperation.Linear, Key = "BaseLayer" });
                    // Log.Info($"BaseLayer switched to {targetBaseAnimName}");
                }
            }
            else
            {
                // Fallback to Idle if Walk/Run not found, or Walk if Run not found.
                if (targetBaseAnimName == RunAnim && !TargetAnimationComponent.Animations.ContainsKey(RunAnim))
                {
                    targetBaseAnimName = WalkAnim; // Try walk
                }
                if (targetBaseAnimName == WalkAnim && !TargetAnimationComponent.Animations.ContainsKey(WalkAnim))
                {
                     targetBaseAnimName = IdleAnim; // Try Idle
                }
                // Play fallback if different from current
                 if (TargetAnimationComponent.Animations.ContainsKey(targetBaseAnimName) && (currentBaseAnimation == null || currentBaseAnimation.Name != targetBaseAnimName || !currentBaseAnimation.Enabled))
                 {
                    currentBaseAnimation?.Stop();
                    TargetAnimationComponent.Play(targetBaseAnimName, new AnimationPlayParameters { Loop = true, BlendOperation = AnimationBlendOperation.Linear, Key = "BaseLayer" });
                 }
                // Log.WarningOnce($"{targetBaseAnimName} animation not found.");
            }

            // Handle Action Events & Upper Body / Action Layer
            HandleActionAnimations();

            // Ensure upper body additive animation (e.g. Idle or BlockIdle) is playing if no other action is overriding it.
            // This part needs to be smarter: if an action animation is playing on "ActionLayer" or "UpperLayer", it might override this.
            // For now, the old logic for upper body idle is kept but might be superseded by block/action logic.
            var currentUpperAnimation = TargetAnimationComponent.PlayingAnimations.FirstOrDefault(pa => pa.Key == "UpperLayer");
            bool isActionPlayingOnUpper = TargetAnimationComponent.PlayingAnimations.Any(pa => pa.Key == "ActionLayer" && pa.Enabled);


            if (!isActionPlayingOnUpper && PlayerController != null && PlayerController.isBlocking)
            {
                 if (TargetAnimationComponent.Animations.ContainsKey(BlockIdleAnim)) {
                    if(currentUpperAnimation == null || currentUpperAnimation.Name != BlockIdleAnim || !currentUpperAnimation.Enabled) {
                        currentUpperAnimation?.Stop();
                        TargetAnimationComponent.Play(BlockIdleAnim, new AnimationPlayParameters { Loop = true, BlendOperation = AnimationBlendOperation.Linear, Key = "UpperLayer" }); // Or Additive if it's designed that way
                    }
                 } else {
                    // Log.WarningOnce($"{BlockIdleAnim} not found for blocking stance.");
                    // Fallback to normal upper idle if block anim not present
                    PlayUpperIdleIfNotPlaying(currentUpperAnimation);
                 }
            }
            else if (!isActionPlayingOnUpper) // Not blocking, no other action on upper layer
            {
                 PlayUpperIdleIfNotPlaying(currentUpperAnimation);
            }


            // --- Further Investigation Notes (already present in Start(), reiterated here for clarity in report) ---
            // Stride's `AnimationComponent` plays `AnimationClip` assets.
            // For distinct upper/lower body animations from full-body clips, animations generally need to be pre-split
            // in a DCC tool (e.g., Blender) or authored specifically as additive layers.
            // Stride does not appear to offer a high-level, built-in runtime bone masking feature on `PlayingAnimation`
            // to restrict a full-body clip to certain bones for a specific playback instance without custom `IAnimationBlender` development.
            // Current blending will rely on `AnimationBlendOperation.Additive` for upper body layers,
            // assuming the animation clips are authored appropriately (either as true additive clips or pre-split).
        }

        private void PlayUpperIdleIfNotPlaying(PlayingAnimation currentUpperAnimation)
        {
            if (TargetAnimationComponent.Animations.ContainsKey(IdleAnim)) // Assuming IdleAnim can serve as upper body additive base
            {
                 if(currentUpperAnimation == null || currentUpperAnimation.Name != IdleAnim || !currentUpperAnimation.Enabled)
                 {
                    currentUpperAnimation?.Stop(); // Stop whatever was on upper
                    TargetAnimationComponent.Play(IdleAnim, new AnimationPlayParameters { Loop = true, BlendOperation = AnimationBlendOperation.Additive, Key = "UpperLayer" });
                 }
            }
        }

        private void HandleActionAnimations()
        {
            // Using a general "ActionLayer". Could be "UpperLayer" if actions are upper-body only.
            // Additive blending allows overlaying on existing movement. Linear would replace.
            // Non-looping for one-shot actions.

            if (jumpEventReceiver.TryReceive())
            {
                Log.Info($"Attempting to play animation: {JumpStartAnim}");
                if (TargetAnimationComponent.Animations.ContainsKey(JumpStartAnim))
                {
                    TargetAnimationComponent.Play(JumpStartAnim, new AnimationPlayParameters { BlendOperation = AnimationBlendOperation.Additive, Key = "ActionLayer", Loop = false });
                } else { Log.Warning($"{JumpStartAnim} animation not found."); }
            }

            if (lightAttackReceiver.TryReceive())
            {
                Log.Info($"Attempting to play animation: {LightAttackAnim}");
                if (TargetAnimationComponent.Animations.ContainsKey(LightAttackAnim))
                {
                    TargetAnimationComponent.Play(LightAttackAnim, new AnimationPlayParameters { BlendOperation = AnimationBlendOperation.Additive, Key = "ActionLayer", Loop = false });
                } else { Log.Warning($"{LightAttackAnim} animation not found."); }
            }

            if (heavyAttackReceiver.TryReceive())
            {
                Log.Info($"Attempting to play animation: {HeavyAttackAnim}");
                if (TargetAnimationComponent.Animations.ContainsKey(HeavyAttackAnim))
                {
                    TargetAnimationComponent.Play(HeavyAttackAnim, new AnimationPlayParameters { BlendOperation = AnimationBlendOperation.Additive, Key = "ActionLayer", Loop = false });
                } else { Log.Warning($"{HeavyAttackAnim} animation not found."); }
            }

            if (dodgeReceiver.TryReceive())
            {
                Log.Info($"Attempting to play animation: {DodgeForwardAnim}");
                if (TargetAnimationComponent.Animations.ContainsKey(DodgeForwardAnim))
                {
                    TargetAnimationComponent.Play(DodgeForwardAnim, new AnimationPlayParameters { BlendOperation = AnimationBlendOperation.Linear, Key = "ActionLayer", Loop = false }); // Linear to override movement
                } else { Log.Warning($"{DodgeForwardAnim} animation not found."); }
            }

            // Block animation is handled in main Update loop based on PlayerController.isBlocking state
            // because it's a sustained state, not a one-shot event here.
        }
    }
}
