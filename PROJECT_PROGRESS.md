# PROJECT PLAN: Stride3D Survival Game Engine v1.0.0

**PLAN ID:** PLAN-STRIDE-SURV-001
**Date:** 2025-05-23T19:54:00Z
**Version:** 1.0.0

## 1. PROJECT OVERVIEW

### 1.1. Objective
To develop a robust and feature-rich survival game engine using Stride3D (version 4.2.0.2381), providing a foundational platform for creating immersive survival game experiences. The engine will support both single-player and multiplayer modes, with a focus on modularity, performance, and ease of customization.

### 1.2. Requirements

#### 1.2.1. Core Engine Features:
*   **World Building:** Procedural or manual world generation, dynamic weather, day/night cycles.
*   **Player Mechanics:** Health, hunger, thirst, stamina, inventory, crafting, building.
*   **Combat System:** Melee and ranged combat, AI for creatures and NPCs.
*   **Networking:** Support for multiplayer (PvP/PvE).
*   **Persistence:** Saving and loading game state.

#### 1.2.2. Specific Gameplay Features:
*   **Separate PvP/PvE Modes:** Distinct rule sets and server configurations for Player vs. Player and Player vs. Environment gameplay.
*   **Tribe Log System:** A logging system similar to Ark: Survival Evolved, detailing significant tribe-related events (e.g., player joins/leaves, structures destroyed, creatures tamed/killed by tribe members).
*   **Security Camera System:** Placeable in-game cameras that players can use to monitor remote locations.
*   **Screaming NPC:** An NPC character that can detect other NPCs or players within a certain range and emit a vocal "scream" or alert, notifying nearby friendly entities.
*   **Souls-like Melee Combat (FPS/TPS):**
    *   **Stamina-based actions:** Attacks, dodges, blocks consume stamina.
    *   **Lock-on targeting:** Ability to focus on a single enemy.
    *   **Hitbox-based combat:** Precise collision detection for attacks.
    *   **Parry/riposte system:** Defensive maneuvers that can open enemies to counter-attacks.
    *   **Variety of weapon types:** Each with unique move sets and attack animations.
    *   **FPS/TPS views:** Player choice of perspective.
*   **Animation Merging (Mixamo) & Ragdoll Physics:**
    *   Integration of Mixamo animations for player and NPC characters.
    *   Advanced animation merging techniques to blend multiple animations smoothly (e.g., running while aiming).
    *   Physics-based ragdoll effects for characters upon death or significant impact.
    *   Area-based damage system linked to ragdoll physics (e.g., headshots, limb damage affecting animations and character behavior).
    *   Reference Three.js example for ragdoll setup: Implement ragdoll physics similar to the provided three.js example, focusing on realistic joint constraints and impact responses.
*   **Dual Melee System:** Support for two distinct melee modes: a standard mode (e.g., Ark-like) and a toggleable "Souls-like" precision melee mode. The Souls-like mode would be primarily for melee weapons, while the standard mode accommodates a broader range of actions. {PRIORITY:HIGH}

### 1.3. Constraints
*   **Engine:** Stride3D version 4.2.0.2381. Project files must be updated if starting from an older template.
*   **Timeline:** Phased development, aiming for iterative releases.
*   **Team Size:** Primarily individual development with potential for collaboration.
*   **Budget:** Open-source / personal project constraints.

## 2. ARCHITECTURE

### 2.1. Diagram Description
*(A conceptual diagram would be inserted here in a full document. For this text-based plan, it's described.)*

The architecture will be modular, based on Stride's Entity-Component-System (ECS) model.
*   **Core Engine Layer:** Low-level systems (Rendering, Physics, Audio, Input, Networking).
*   **Game Systems Layer:** Manages game logic (World Management, Player State, AI Director, Crafting, Building, Combat). These systems will interact with entities and their components.
*   **Entity & Components Layer:** Player characters, NPCs, creatures, items, world objects, each with specific components defining their behavior and data.
*   **Game API/Modding Layer:** Interfaces for extending or modifying game functionality (optional, future goal).
*   **Presentation Layer:** UI, HUD, visual effects, soundscapes.

### 2.2. Rationale
*   **Modularity (ECS):** Stride's ECS is ideal for managing complex game objects and behaviors, allowing for easy addition, removal, or modification of features.
*   **Scalability:** Clear separation of concerns allows different systems to be developed and optimized independently.
*   **Performance:** Stride's design is performance-oriented. The architecture will leverage this by keeping game logic efficient.

## 3. COMPONENT BREAKDOWN

### 3.1. Hierarchy
*   **Game (Root Scene)**
    *   **GlobalSystems** (Scripts for managing overall game state, time, weather, AI director)
    *   **World** (Terrain, foliage, dynamic objects, environment probes)
    *   **Player Entities**
        *   CharacterControllerComponent
        *   StatsComponent (Health, Hunger, Thirst, Stamina)
        *   InventoryComponent
        *   CraftingComponent
        *   CombatComponent (handles attacks, damage, abilities)
            *   Note: Melee weapon functionality will need to adapt to both standard and Souls-like combat modes. Input handling for mode toggle is crucial.
        *   AnimationComponent (linked to Animation Merging system)
        *   NetworkSyncComponent
        *   InputComponent
            *   Note: Input handling will need to support a toggle for switching between standard melee and Souls-like melee modes.
    *   **NPC/Creature Entities**
        *   AIControllerComponent (Pathfinding, Behavior Trees)
        *   StatsComponent
        *   CombatComponent
        *   AnimationComponent (linked to Animation Merging system)
        *   RagdollComponent
        *   NetworkSyncComponent
        *   ScreamerBehaviorComponent (for the specific NPC type)
    *   **Item Entities** (Weapons, tools, resources, consumables)
        *   ItemComponent (Data: type, stats, stackability)
        *   PickupComponent
        *   RenderComponent
    *   **Structure Entities** (Foundations, walls, crafting stations, security cameras)
        *   StructureComponent (Data: health, owner, type)
        *   BuildableComponent
        *   InteractableComponent (e.g., for camera view, door open/close)
        *   NetworkSyncComponent
    *   **Specialized Systems Entities**
        *   TribeLogSystem (Script managing tribe event data and display)
        *   SecurityCameraSystem (Script managing camera feeds and player interaction)

### 3.2. Component Specifications (Examples)

*   **StatsComponent:**
    *   `MaxHealth: float`
    *   `CurrentHealth: float`
    *   `Hunger: float`
    *   `MaxHunger: float`
    *   `Thirst: float`
    *   `MaxThirst: float`
    *   `Stamina: float`
    *   `MaxStamina: float`
    *   `OnHealthChanged: event`
    *   `OnStaminaChanged: event`

*   **CombatComponent (Souls-like focus):**
    *   `CurrentWeapon: ItemEntity`
    *   `LockOnTarget: Entity`
    *   `Attack(WeaponAttackType): bool` (Returns true if attack initiated)
    *   `Dodge(Vector3 direction): bool`
    *   `Block(): bool`
    *   `Parry(): bool` (Timed block for riposte opportunity)
    *   `TakeDamage(float amount, DamageType type, Entity source, Bone hitBone)`
    *   `OnDamageTaken: event`
    *   `OnTargetLocked: event`

*   **ScreamerBehaviorComponent:**
    *   `DetectionRadius: float`
    *   `DetectionAngle: float` (Field of view)
    *   `TargetTypes: List<EntityType>` (e.g., Player, HostileNPC)
    *   `ScreamSound: SoundEffect`
    *   `AlertCooldown: float`
    *   `CheckForTargets(): void` (Called periodically)
    *   `Scream(): void` (Plays sound, triggers alert to nearby friendlies)

*   **RagdollComponent:**
    *   `IsActive: bool`
    *   `RootBone: Bone`
    *   `LimbHitboxes: Dictionary<Bone, HitboxShape>`
    *   `ActivateRagdoll(Vector3 impulse)`
    *   `DeactivateRagdoll()`
    *   `ApplyForceToLimb(Bone limb, Vector3 force, ForceMode mode)`

## 4. IMPLEMENTATION STRATEGY

### 4.1. Phases
1.  **Phase 1: Core Engine Setup & Basic Player**
    *   Project creation, version control.
    *   Basic player entity, movement, camera (FPS/TPS).
    *   Stride scene setup, basic lighting.
2.  **Phase 2: Weapons, Tools, and Sound {DURATION:3w}**
    *   Core Survival Mechanics: Health, hunger, thirst, stamina. Basic inventory and item pickup. Day/night cycle.
    *   **Initial Weapon/Tool Implementation & Sound Design Pass:**
        *   Implement basic melee weapons (e.g., from `TASK-002-A`) and tools.
        *   **Sound Design:** Weapon and tool implementation in this phase must consider a detailed list of sound event categories:
            *   Equip (draw/ready)
            *   Unequip (holster/put away)
            *   Idle handling (subtle movement, cloth, metal, wood as appropriate)
            *   Attack (swing, fire, or use)
            *   Impact (hit target: flesh, wood, stone, metal, glass, ground, water)
            *   Miss (swing or fire with no hit)
            *   Durability break (distinct sound for tool/weapon breaking)
            *   Reload (if applicable)
            *   Ammo insert/remove (if applicable)
            *   Special (unique actions, e.g., charge, prime, detonate, etc.)
3.  **Phase 3: Combat System - Melee Focus**
    *   Souls-like combat mechanics (stamina, lock-on, basic attacks, dodge).
    *   Standard melee combat mechanics.
    *   Implementation of the toggle between Souls-like and Standard melee modes.
    *   Hitbox system and damage application.
    *   Basic enemy AI (placeholder).
4.  **Phase 4: Animation & Physics**
    *   Character animation integration (Mixamo).
    *   Animation merging system development.
    *   Ragdoll physics implementation and area-based damage.
5.  **Phase 5: Advanced Gameplay Features**
    *   Crafting and Building systems.
    *   Security Camera system.
    *   Screaming NPC implementation.
    *   Tribe Log system.
6.  **Phase 6: Vehicles & Scouting Tools {DURATION:2w}**
    *   Implement `TASK-006-A` (boats, spyglass, binoculars, scouting drone).
7.  **Phase 7: Networking & Multiplayer**
    *   Basic multiplayer synchronization (player movement, actions).
    *   PvP/PvE mode distinction.
    *   Networked interactions for core features.
8.  **Phase 8: World & Content**
    *   Basic procedural world generation or manual level design tools.
    *   More diverse NPCs and creatures.
    *   Sound design and VFX polish (revisit and enhance earlier sound work, add environmental/character sounds).
9.  **Phase 9: Testing, Optimization & Refinement**
    *   Comprehensive testing.
    *   Performance profiling and optimization.
    *   Bug fixing and polish.

### 4.2. Tasks (with example prompts for an AI assistant)

*   **TASK-001-A: Implement FPS/TPS camera and movement.**
    *   **Prompt/Description:** "Create a new Stride 4.2.0.2381 project. Set up a basic scene with a ground plane. Adapt existing template scripts: `PlayerCamera.cs` for switchable FPS/TPS views (with camera collision handling), `PlayerInput.cs` for input event management, and `PlayerController.cs` (utilizing `CharacterComponent`) for player movement (WASD, jump).
    **Status:** Completed
    *   **Souls-like Melee Mechanics Integration:** Investigate Stride3D's capabilities for core Souls-like features: target lock-on, target switching (e.g., mouse wheel or dedicated keys), and dynamic camera adjustments for optimal combat visibility. Explore implementing distinct melee attack states (e.g., light, heavy, special) and basic combo sequences. Add new input events to `PlayerInput.cs` for lock-on, dodge, and different attack types. Consider how player model orientation and movement should adapt when locked onto a target (e.g., strafing, maintaining focus).
    *   **Animation System with Mixamo Blending:** Investigate Stride's animation system for advanced blending techniques, particularly upper/lower body animation separation and merging (inspired by the user's three.js ragdoll/animation example, focusing on animation aspects here). Use a placeholder character model initially and integrate a selection of Mixamo animations (e.g., idle, walk, run, basic attacks, dodge). This includes the sub-task of pre-processing or designing a workflow to generate separate upper-body (e.g., aiming, attacking) and lower-body (e.g., walking, running, strafing) animation clips from full-body Mixamo animations. Implement initial logic in an animation controller script to play and blend these clips based on player state (e.g., lower body plays walk/run, upper body plays idle or aiming). Note that physics-based ragdoll effects for realistic damage feedback and death animations are a related but distinct future task (now part of Phase 4 Ragdoll).
    *   **Input Remapping Clarification:** The initial setup will utilize the template's approach for input handling (e.g., hardcoded key lists or simple mapping in `PlayerInput.cs`). Full UI-driven input remapping is a more extensive feature planned for a later task.
    *   **PvP/PvE Design Consideration:** Develop the player controller with an awareness of future PvP and PvE mode distinctions. Consider how systems like targeting, damage application, or ability usage might need to differ or be configurable based on the game mode.
    *   **NPC Alert System Note:** Player actions (e.g., making noise, being detected) will eventually need to interface with an NPC alert system. This is for future integration and not part of this immediate task, but the player controller should be extensible enough to support such interactions.
    *   **Dual Melee Mode Toggle:** The player controller must support a toggleable melee mode system (standard vs. Souls-like). Input for this toggle should be considered. This is a {PRIORITY:HIGH} feature.
    *   **Souls-like Melee Mechanics Investigation Findings & Design Outline:**
        *   **I. Lock-On System:**
            *   **Target Acquisition:**
                *   Proposal: Use `Simulation.ShapeSweep()` with a `SphereColliderShape` from the player/camera.
                *   Target Identification: Define a specific `CollisionFilterGroup` (e.g., "TargetableEnemy") and a `TargetableComponent.cs` script on enemy entities. This component will provide a lock-on point (e.g., a `Transform` or `Vector3` offset).
            *   **Target Selection:** If multiple valid targets are found by the sweep, select the one closest to the player's forward vector or camera's center screen. Store the current target (e.g., `Entity targetEntity` in `PlayerCombatController`).
            *   **Camera Control (Locked-On):**
                *   In `PlayerCamera.cs`, when a target is locked, the camera should smoothly orient to keep both the player and the target's lock-on point in view, typically trying to frame them based on combat needs. Player input (right stick/mouse) allows for minor adjustments or orbiting around the target.
            *   **Player Movement (Locked-On):**
                *   In `PlayerController.cs` (or a new `PlayerCombatController.cs`), when locked-on:
                    *   Forward/backward input (W/S or left stick Y-axis) moves the player towards or away from the target.
                    *   Horizontal input (A/D or left stick X-axis) makes the player strafe/orbit around the target while maintaining orientation towards it.
                    *   Player model should always face the target.
            *   **Target Switching:** Implement input (e.g., right stick flick, dedicated buttons like Q/E or L1/R1) to cycle through other valid targets within the acquisition range.
            *   **Exiting Lock-On:** Lock-on state is exited if:
                *   Player presses the lock-on toggle input again.
                *   Target is defeated (e.g., health <= 0).
                *   Target moves out of a maximum lock-on range or breaks line of sight for a certain duration.
        *   **II. Animation System for Melee (using `AnimationComponent`):**
            *   **Upper/Lower Body Blending:**
                *   The `AnimationComponent` allows playing multiple animations. Manage `PlayingAnimation` instances directly.
                *   Lower body animations (e.g., Idle, Walk, Run, Strafe_L/R, Dodge_Fwd/Bwd/L/R) should be played on a lower set of bones. Use `AnimationBlendOperation.LinearBlend` for smooth transitions between movement states.
                *   Upper body animations (e.g., Idle_Upper, Attack_Light, Attack_Heavy, Block_Idle, HitReaction_Upper) should be played on an upper set of bones. Use `AnimationBlendOperation.Additive` with a weight of 1.0 for these, or `LinearBlend` if they are full-body but masked.
                *   This requires Mixamo (or other) animations to be either pre-separated into upper/lower body clips or for the animation system to support bone masking during playback. Stride's `AnimationComponent` might require manual setup of which bones are affected by which `PlayingAnimation` if direct masking isn't available per-track in the same layer.
            *   **Playing Attacks/Actions:** Use `AnimationComponent.Play()` for distinct actions like attacks or dodges. These might temporarily override either the upper body or full body. `AnimationComponent.Crossfade()` can be used for smoother transitions if animations are designed for it.
            *   **Hit Reactions:** Play specific hit reaction animations upon taking damage. These would typically affect the upper body or full body and interrupt other actions.
            *   **`IBlendTreeBuilder`:** Stride provides this interface. For advanced blending (e.g., multi-directional movement, speed-based animation changes within a single state), a custom blend tree might be necessary. However, initial upper/lower body separation and action overrides can likely be managed with careful `Play()` and `Crossfade()` calls on the `AnimationComponent` without a full custom `IBlendTreeBuilder`.
        *   **III. Core Combat Scripting:**
            *   **Player States:** Introduce a player state machine (e.g., an `enum PlayerState` managed in `PlayerController.cs` or a dedicated `PlayerCombatController.cs`). States could include: `Idle`, `Moving`, `LockedOnIdle`, `LockedOnMoving`, `Attacking`, `Dodging`, `Blocking`, `HitStun`, `Dead`. Player input and animation playback will be handled based on the current state.
            *   **Stamina Management:** Create a `StaminaComponent.cs` (or integrate into an existing `PlayerStats.cs`) to manage stamina. Actions like attacking, dodging, blocking, and potentially running will consume stamina. Stamina should regenerate over time when not performing stamina-consuming actions. Running out of stamina could prevent actions or induce a fatigue state.
            *   **Input Expansion (PlayerInput.cs):**
                *   Add new `EventKey`s for combat actions: `LockOnToggleEventKey`, `DodgeEventKey`, `LightAttackEventKey`, `HeavyAttackEventKey`, `BlockEventKey`, `ParryEventKey` (future).
                *   Map these to keyboard/mouse and gamepad inputs (e.g., Middle Mouse/R3 for Lock-On, Space/B for Dodge, LMB/RB for Light Attack, Shift+LMB/RT for Heavy Attack, RMB/LB for Block).
        *   **IV. Future Considerations (Briefly Mention):**
            *   **Blocking/Parrying:** Implement mechanics for active blocking (reduce/negate damage, consume stamina) and parrying (timed block for a counter-attack opportunity).
            *   **Attack Combos:** Allow sequencing of light and heavy attacks into predefined combo chains.
            *   **Invincibility Frames (I-frames):** Grant temporary invulnerability during parts of the dodge animation.
            *   **Animation-Driven Movement (Root Motion):** Investigate Stride's support for root motion. If robust, it could simplify attack/dodge movement by driving character displacement from animation data rather than purely script-based movement. This requires animations to be authored with root motion.
*   **TASK-001-B: Integrate modular inventory and hotbar.**
    *   **Prompt/Description:** "Develop a foundational inventory system. ... (rest of prompt as is)"
*   **[TASK-002-A] Implement Basic Melee Weapons and Tools (Pick, Hatchet):** (Prompt for this task would detail initial weapon/tool setup, linking to `PlayerEquipment`, basic attack animations, and resource gathering interaction. Placeholder for now.)
*   **[TASK-002-B] Add ranged, explosive, and special weapons {ESTIMATE:24h}**
    *   **Prompt:**
        > Extend the weapon system to include bows (multiple arrow types), firearms, explosives, and special weapons (plasma, railgun). Each should have distinct sound effects and visual feedback.
*   **[TASK-006-A] Add boats, spyglass, binoculars, scouting drone {ESTIMATE:24h}**
    *   **Prompt:**
        > Implement controllable boats, handheld spyglass, binoculars, and a deployable scouting drone with remote camera and minimap integration.
*   **Task Example (Phase 3 - Updated):** "Implement a lock-on targeting system for the CombatComponent. When a button is pressed, the player should target the nearest enemy within a specified range and cone of view. The camera should smoothly adjust to keep both player and target in frame. Ensure this integrates with the dual melee mode toggle."
*   **(Other task examples as they were)**

### 4.3. Implementation Order Diagram Description
*(Existing description is likely still fine, but phases are re-numbered)*

`[Core Setup] -> [Weapons/Tools/Sound] -> [Combat Systems] -> [Animation/Physics] -> [Advanced Gameplay] -> [Vehicles/Scouting] -> [Networking] -> [World/Content] -> [Testing/Polish]`

Dependencies: (Review if any changes needed due to new phase order)
*   Combat depends on Player Mechanics.
*   Advanced Gameplay Features depend on Combat and Player Mechanics.
*   Networking depends on most other systems being functional locally.
*   Ragdoll/Animation depends on basic character setup.

## 5. TESTING STRATEGY
*(Existing content is likely fine)*

---
## 6. APPENDICES

### A. Weapon Stats Table
| Weapon               | Damage | Fire Rate | Range | Special         |
|----------------------|--------|-----------|-------|-----------------|
| Pick                 | 15     | 0.5/s     | Melee | Mining bonus    |
| Hatchet              | 20     | 0.6/s     | Melee | Wood bonus      |
| Mining Drill         | 10     | 1.5/s     | Melee | Area mining     |
| Revolver             | 45     | 0.8/s     | 30m   | High recoil     |
| Modern Pistol        | 35     | 2.0/s     | 25m   | Fast reload     |
| Bolt Sniper          | 90     | 0.5/s     | 150m  | High accuracy   |
| Semi-auto Sniper     | 70     | 1.0/s     | 100m  |                 |
| Assault Rifle        | 30     | 8.0/s     | 60m   | Full-auto       |
| Double Barrel Shotgun| 60x2   | 0.5/s     | 10m   | Wide spread     |
| Shotgun              | 40     | 1.2/s     | 15m   |                 |
| Plasma Rifle         | 15     | 5.0/s     | 50m   | AOE, shield dmg |
| Rail Gun             | 120    | 0.2/s     | 200m  | Piercing        |
| Grenade Launcher     | 100    | 0.8/s     | 40m   | Explosive       |
| Grenade              | 120    | -         | 15m   | Throwable       |
| C4                   | 300    | -         | -     | Timed/detonator |
| Rocket Launcher      | 250    | 0.5/s     | 60m   | Splash damage   |

---

### B. Structure Material Table
| Material     | HP    | Weakness           | Strength                |
|--------------|-------|--------------------|-------------------------|
| Thatch       | 200   | Fire, rain         | Cheap, fast to build    |
| Wood         | 400   | Fire, axes         | Easy to upgrade         |
| Stone        | 800   | Explosives         | Weatherproof            |
| Metal        | 2000  | Plasma, C4         | Conducts electricity    |
| Advanced     | 4000  | Plasma, rockets    | Immune to small arms    |
| Glass        | 600   | Melee, explosives  | Greenhouse effect       |

---

### C. Defensive Structure Table
| Defense Type       | Damage/Effect    | Power Use | Special                      |
|--------------------|------------------|-----------|------------------------------|
| Spike Wall         | 40/sec contact   | 0         | Bleed effect                 |
| Bear Trap          | Immobilize 10s   | 0         | Single-use                   |
| Turret (Rifle)     | 50/shot          | 5/min     | Ammo required                |
| Heavy Turret       | 80/shot          | 10/min    | Armor-piercing               |
| Plasma Turret      | 30/shot (AOE)    | 15/min    | Ignores shields              |
| Flame Turret       | 20/sec (burn)    | 8/min     | Area denial                  |
| Tesla Coil         | 80 (shock)       | 20/min    | Chain to 3 targets           |
| Force Field        | Blocks all but C4| 50/min    | Only explosive/plasma damage |

---

### D. Power & Irrigation Table
| Device             | Power Use | Range/Capacity | Notes                       |
|--------------------|-----------|----------------|-----------------------------|
| Gas Generator      | 20/min    | 50m cable      | Needs fuel                  |
| Field Generator    | 40/min    | 30m radius     | Wireless power              |
| Battery            | -         | 1000 units     | Stores excess power         |
| Pump               | 10/min    | 100m pipe      | Moves water uphill          |
| Reservoir          | -         | 2000L          | Stores water                |
| Sprinkler          | 2/min     | 8m radius      | Irrigates crops             |

---

## 7. ADDITIONAL USER REQUIREMENTS (Incorporated into relevant task prompts)
*(Existing content generally fine, ensure dual melee note is robust)*

---

## 8. GENERAL TECHNICAL NOTES & WORKFLOW
*(Existing content fine)*

---
**End of Plan**
---
