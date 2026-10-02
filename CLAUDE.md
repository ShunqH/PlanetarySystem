# PlanetSystem — project conventions

- Unity 6000.3.5f2, URP, new Input System only, uGUI (legacy Text, no TextMeshPro assets required).
- All code comments and user-facing docs are in English.
- Units: AU / yr / Msun, G = 4 pi^2. Physics frame is z-up (reference plane = xy); mapped to Unity as (x, z, y) in `Units.ToUnity`.
- Orbital elements use Rebound conventions (a, e, inc, Omega, omega, f). Conversions are ports of Rebound's `reb_particle_from_orbit` / `reb_orbit_from_particle`.
- Primaries: massive bodies always orbit the barycenter of the other massive bodies (`BodyDefinition.NormalizeReference`, also enforced in `ScenarioBuilder` and `LoadScenario`); only test particles may name a specific massive body.
- No hard body limits; `SimulationSettings` holds warning thresholds (10 massive, 20 test particles). `Gravity` switches to a parallel kernel above `ParallelThreshold` pair interactions (measured break-even about 3000).
- Assemblies: `PlanetSystem.Physics` (pure C#, no engine refs), `PlanetSystem.Data` (pure C#: `Scenario` model, `MiniJson`, `ScenarioJson`, `ScenarioBuilder`, `BuiltInScenarios`), and `PlanetSystem.Runtime` (Core / View / UI / Interaction). Tests for Physics and Data live in `Assets/Scripts/Tests/EditMode`.
- Cases: presets are defined in `BuiltInScenarios` with orbital elements; saved cases store exact Cartesian state. `ScenarioLibrary` persists saved cases to `Application.persistentDataPath/saved_cases.json` with atomic writes. Load/capture live in `SimulationController.Scenarios.cs`.
- Pop-ups (`ScenarioMenu`, `ModalDialog`) sit on a separate overlay canvas; global hotkeys are suspended while `ModalDialog.OwnsKeyboard`.
- Integrators live in `Assets/Scripts/Physics/Integrators` behind `IIntegrator` (Leapfrog DKD, Yoshida 4, Dormand-Prince 5(4) adaptive); register new ones in `IntegratorCatalog`. They operate on `ParticleArrays` (massive bodies first). Fixed steps come from `Timescales.ShortestTimescale` and divide the frame duration exactly.
- The run loop is in `SimulationController.Integration.cs`: 60 fps target (vSync off), speed in yr per real second, CPU cap in ms per frame converted to a force-evaluation budget. Bodies are immutable while running; dynamical edits recapture the initial conditions and reset t to 0.
- Interaction lives in `Assets/Scripts/Runtime/Interaction`: `InteractionController` (global hotkeys, `InteractionState` machine cycled by Tab, click-vs-drag pointer handling) and `CameraController` (preset/tracking views 1-6, drone free-fly, and the Focus state in `CameraController.Focus.cs` that follows a massive body without rotating). All selection goes through `InteractionController.RequestSelect` so Focus can retarget on massive bodies. `SceneViewManager.LateUpdate` calls `CameraController.Tick` between updating bodies and placing labels, so there is no frame lag. Body picking is screen-space (`SceneViewManager.PickBody`); bodies have no colliders. UI keyboard navigation is disabled so WASD never drives widgets.
- The scene is built procedurally by `AppBootstrap`; `Assets/Scenes/Main.unity` only holds the camera, light, volume and the Bootstrap object. Do not hand-author UI prefabs.
- Materials used at runtime live in `Assets/Resources/Materials` so they are included in builds.
- Roadmap: see PLAN.md (Chinese). Usage: see README.md.
