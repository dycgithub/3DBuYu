# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

This is a Unity 2022.3.13f1c1 (LTS) project named "3DBuYu" (3D捕鱼) - a 3D tower defense/shooting game featuring turrets that attack enemies on a spherical world.

**Render Pipeline**: Universal Render Pipeline (URP) 14.0.9

**Main Scene**: `Assets/Scenes/SampleScene.unity`

## Development

### Opening the Project

Open with Unity Editor 2022.3.13f1c1 or compatible version. The project uses standard Unity build pipeline (File > Build Settings).

### Running Tests

The Movement module has an Editor test suite (~200+ unit/integration tests) at `Assets/Scripts/Movement/Tests/Editor/`. Open in Unity, go to Window > General > Test Runner, and run `SphereMovement` tests. A visual test management window is also available: `SphereMovementTestWindow`.

### Creating ScriptableObject Assets

- **Turret Level Data**: Right-click in Project window → Create → Turret → Level Data
- **Bullet Config**: Right-click in Project window → Create → Turret → Bullet Config
- **Movement Config**: Right-click in Project window → Create → Movement → Config

## Architecture

### Movement System (`Assets/Scripts/Movement/`)

Isolated via assembly definition (`SphereMovement.asmdef`, namespace `SphereMovement`). Supports both **plane** and **spherical** movement modes via strategy pattern.

**Subdirectory structure:**
- **Interfaces/**: `IInputProvider`, `IMovementInputHandler`, `IMovementStrategy`, `ISurface`, `ISphericalPositionCalculator`, `ISmoothMovementController`, `IOrientationController`
- **Core/**: `SurfaceMovement` (main component, replaces old `SphereMovement`), `MovementInputHandler`, `MovementStrategyFactory`, `SmoothMovementController`, `OrientationController`, `SphericalPositionCalculator`, `SphericalMovementStrategy`, `PlaneMovementStrategy`
- **Input/**: `MovementInput` (input component), `SurfaceMovementInput`, `UnityInputProvider`, `KeyboardInputProvider`, `MockInputProvider`
- **Camera/**: `ThirdPersonCamera` (replaces old `SphereCameraController`) with `Standard` and `Spherical` follow modes, mouse orbit, scroll zoom, collision detection
- **Environment/**: `SphereSurface` - implements `ISurface`, defines the sphere (center, radius), handles coordinate conversion and visualization
- **Data/**: `MovementConfig` ScriptableObject for centralized movement parameters
- **Tests/Editor/**: Full test suite (`TestRunner`, unit tests for all core classes, integration tests, `SphereMovementTestWindow`)
- Root level: `SphericalCoordinates.cs` (math utilities)

**Key design**: `SurfaceMovement` component requires a `MovementInput` component. The `MovementMode` enum (`Plane` / `Spherical`) determines which strategy is used. `SphereSurface` is required for spherical mode. Input handling is injectable via `IMovementInputHandler`.

### Turret System (`Assets/Scripts/Turret/`)
- `Turret.cs`: Main controller - searches for nearest enemies (tagged "Enemy"), rotates the turret head, fires bullets using object pooling
- `TurretLevelData.cs`: ScriptableObject for turret stats (damage, range, fire rate, upgrade progression)

### Bullet System (`Assets/Scripts/Bullet/`)
- `Bullet.cs`: Projectile behavior with homing/straight-line movement, penetration mechanics, collision handling
- `BulletConfig.cs`: ScriptableObject for bullet properties (speed, color, penetration, wall penetration)
- Bullets use object pooling and auto-release based on max distance or timeout

### Enemy System (`Assets/Scripts/Enemy/`)
- `EnemyBase.cs`: Base enemy with health, movement, and attack behavior
- `EnemyNormal.cs`, `EnemyFast.cs`, `EnemyTank.cs`, `EnemyFlying.cs`: Enemy variants
- `EnemySpawnManager.cs`: Spawns enemies based on wave configuration
- `EnemySpawnData.cs`: ScriptableObject for spawn patterns
- `StateMachine.cs`: State machine for enemy AI behavior

### Flocking System (`Assets/Scripts/Flocking/`)
- `globalFlock.cs`: Manages a school of fish - spawns fish within swim limits, periodically sets random goal positions, provides `FishSpeed()` for speed control, visualizes bounds with `OnDrawGizmosSelected`
- `flock.cs`: Per-fish boids behavior - separation (avoid near neighbors), cohesion (move toward group center), alignment (match group speed), boundary containment (turn back at swim limits)

### Rope/Wave System (`Assets/Scripts/Rope/`)
- `NodeLineWave.cs`: Interactive rope simulation with node-based wave propagation. Node0 is dragged with W/S keys; subsequent nodes follow via chain lerp. On return-to-origin, triggers a wave shape that propagates forward through the node chain. Supports multiple concurrent waves, drag fade, and collision-free wave-drag interaction.
- `FirstNodeStates.cs`: State machine (`FirstNodeStateMachine`) for rope's first node: `Origin`, `Leave`, `Back`, `Still`. Tracks drag state transitions.

### Game System (`Assets/Scripts/Game/`)
- `GameManager.cs`: Central controller - game states (Menu/Playing/Paused/GameOver/Victory), difficulty levels, game flow
- `ResourceManager.cs`: Player resources (coins, experience, gems, keys) with events for UI updates
- `SaveSystem.cs`: JSON serialization save/load with separate files for game data, resources, and settings
- `DropManager.cs`: Enemy death drops (coins, experience, health packs, power-ups, gems) with weighted random selection and magnetic pickup

### Other Systems

- **Player** (`Assets/Scripts/Player/`): `PlayerHealth.cs` - damage, healing, invincibility frames, health regen, death/respawn
- **Camera** (`Assets/Scripts/Camera/`): `CameraFollow.cs` (FixedDistance/Orbit/Smooth modes), `CameraShake.cs`
- **Effects** (`Assets/Scripts/Effects/`): `EffectManager.cs` (singleton effect spawning), `UpgradeEffectConfig.cs` (ScriptableObject)
- **Audio** (`Assets/Scripts/Audio/`): `AudioManager.cs` - centralized SFX/music management
- **Utilities** (`Assets/Scripts/Utils/`): `ObjectPool.cs` - generic pooling for Components (`ObjectPool<T>`) and classes (`ObjectPoolSimple<T>`)

### Design Patterns

1. **ScriptableObject Pattern**: All configuration data (TurretLevelData, BulletConfig, UpgradeEffectConfig, EnemySpawnData, MovementConfig)
2. **Object Pooling**: Bullets pooled via `ObjectPool<T>` to avoid GC pressure
3. **Strategy Pattern**: Movement uses `IMovementStrategy` with `SphericalMovementStrategy` and `PlaneMovementStrategy`
4. **Singleton**: EffectManager, GameManager, ResourceManager, DropManager, AudioManager
5. **State Machine**: Enemy AI (`StateMachine`), Rope first node (`FirstNodeStateMachine`)
6. **Assembly Definitions**: `SphereMovement.asmdef` isolates the movement module

### Key Tags

- `"Enemy"`: Target detection (Turret) and collision (Bullet)
- `"Player"`: Drop item pickup detection
- `"Wall"`: Collision detection (bullets can optionally ignore walls)

## Development Status

### Completed Systems
- Turret system with upgrades
- Bullet system with penetration + object pooling
- Spherical/plane movement with strategy pattern + full test suite
- Third-person camera (standard + spherical modes, mouse orbit)
- Enemy AI with multiple variants + state machine
- Resource management + save/load (JSON)
- Drop system with magnetic pickup
- Player health system
- Audio manager foundation
- Fish flocking (boids)
- Rope node-wave simulation

### Missing Systems (TODO)
- UI System: No menus, HUD, pause screen, shop interface
- Wave System: No wave progression or level management (partially implemented)
- Skill System: No player skills (active/passive)
- Tutorial System: No onboarding or guidance
- Achievement System: No achievements or quest tracking
- CI/CD: No automated build pipeline
- Level Design: Only SampleScene exists

## Code Conventions

- Chinese comments for XML documentation (`/// <summary>`)
- English for all code (class names, variables, methods)
- Namespaces match folder structure
- `[RequireComponent]` attributes used where appropriate
