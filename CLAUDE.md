# PlanetSystem — project conventions

- Unity 6000.3.5f2, URP, new Input System only, uGUI (legacy Text, no TextMeshPro assets required).
- All code comments and user-facing docs are in English.
- Units: AU / yr / Msun, G = 4 pi^2. Physics frame is z-up (reference plane = xy); mapped to Unity as (x, z, y) in `Units.ToUnity`.
- Orbital elements use Rebound conventions (a, e, inc, Omega, omega, f). Conversions are ports of Rebound's `reb_particle_from_orbit` / `reb_orbit_from_particle`.
- Body limits: 4 massive, 10 test particles (see `SimulationSettings`).
- Assemblies: `PlanetSystem.Physics` (pure C#, no engine refs, unit-tested) and `PlanetSystem.Runtime` (Core / View / UI). Tests live in `Assets/Scripts/Tests/EditMode`.
- The scene is built procedurally by `AppBootstrap`; `Assets/Scenes/Main.unity` only holds the camera, light, volume and the Bootstrap object. Do not hand-author UI prefabs.
- Materials used at runtime live in `Assets/Resources/Materials` so they are included in builds.
- Roadmap: see PLAN.md (Chinese). Usage: see USER_GUIDE.md.
