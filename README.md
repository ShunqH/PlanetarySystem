# PlanetSystem — User Guide

An interactive 3D sandbox for building planetary systems and watching them evolve under gravity,
aimed at newcomers to planetary dynamics.

## Units and conventions

| Quantity | Unit |
|---|---|
| Length | AU |
| Time | years |
| Mass | solar masses (the UI also offers Jupiter and Earth masses) |
| Angles | degrees in the UI |

Orbital elements follow the [Rebound](https://rebound.readthedocs.io) convention:
`a` semi-major axis, `e` eccentricity, `inc` inclination, `Ω` longitude of the ascending node,
`ω` argument of pericenter and `f` true anomaly (or `M` mean anomaly). The reference plane is the
horizontal grid; inclination is measured from it.

## Building a system

1. **Add star** — the first body is always massive and is placed at the origin.
2. **Add body** — every further body is defined by its orbital elements relative to a *primary*:
   either one specific massive body or the center of mass of all massive bodies (use the latter for
   circumbinary planets). A grey ghost previews the body and its orbit while you adjust the sliders.
3. After each addition the whole system is shifted so that the barycenter sits at the origin,
   exactly like Rebound's `move_to_com()`.

Two kinds of bodies exist:

- **Massive** bodies (stars, giant planets) attract everything. At most 4.
- **Massless test particles** feel gravity but exert none. At most 10.

## Editing

Click a body in the 3D view or in the left-hand list to select it. The right-hand panel shows its
current osculating elements; every change is applied immediately and the system is re-centered.
`Delete` removes the body; bodies that used it as primary fall back to the center of mass.

The coloured ellipse around each body is its **osculating orbit**: the Keplerian orbit it would follow
if only its primary acted on it. When the primary is a specific body the ellipse is drawn around that body
(relative orbit). When the primary is the center of mass, massive bodies are drawn on their **barycentric**
orbits, so a binary shows two ellipses around the barycenter; the panel still edits the relative elements
(a, e, ... of one star with respect to the other) and reports the barycentric semi-major axis for reference. Once integration is enabled these ellipses will precess and tilt under
perturbations, which is the main thing the demo is meant to show.

## Running the simulation

The top bar controls the time evolution:

| Control | What it does |
|---|---|
| **Integrator** | Numerical method, chosen before starting (locked while running). |
| **Speed** | Simulated years per real second, from 0.01 to 1000. Default 1 yr/s. Can be changed while running. |
| **Start / Stop** | Starts or pauses the integration. |
| **Reset** | Restores the system to its initial conditions and sets t = 0. |
| **t = …** | Simulated time since the initial conditions. |
| **Load example** | Polar circumbinary system: an equal-mass eccentric binary (0.5 + 0.5 M☉, a = 1 AU, e = 0.8), a Jupiter-mass planet at 5 AU and a massless planet at 10 AU, both on circular orbits with inc = 90° and Ω = 90°, so their orbit normals are aligned with the binary's eccentricity vector. |
| **Clear all** | Start from scratch. |

The demo renders at a fixed 60 frames per second, so each frame advances the system by speed / 60 years.

### Integrators

All three integrate the full N-body problem: massive bodies attract each other, test particles feel
every massive body but exert no force. They are listed from cheapest to most expensive.

| Integrator | Order | Step | Force evaluations per step | Best for |
|---|---|---|---|---|
| Leapfrog | 2 | fixed | 1 | Fast, long runs; energy error stays bounded but orbital phases drift slowly. |
| Yoshida | 4 | fixed | 3 | Long runs that need accurate phases (resonances, precession). |
| Dormand-Prince 5(4) | 5 | adaptive | 6 | Close encounters, very eccentric orbits, scattering; not symplectic, so energy drifts slowly. |

The fixed step is chosen automatically as a small fraction of the shortest dynamical timescale in the
system, evaluated at pericenter: 3 % for Leapfrog and 6 % for Yoshida. It is adjusted so each frame
contains a whole number of equal steps. If an orbit tightens a lot during the run, for example through
Kozai-Lidov eccentricity growth, the step shrinks automatically. The adaptive integrator uses a relative
tolerance of 1e-10 per step. All of these values can be changed in the `SimulationSettings` asset.

### Status bar

The bottom bar shows the current step size, the relative energy error of the massive bodies since the
initial conditions, the number of steps per frame and the achieved speed. Integration gets at most
8 ms of CPU time per frame. When a high speed needs more than that, the simulation runs slower than
requested and the status bar says so.

Integration stops automatically when two bodies touch (their physical radii overlap). Use **Reset**
to go back to the initial conditions.

### Editing and running

Bodies cannot be added, edited or deleted while the integration runs. Selecting a body while it runs
shows its **live osculating elements**, which is a good way to watch inclination or eccentricity
oscillate. After stopping, editing a body's orbit or mass defines new initial conditions and resets
t to 0. Renaming a body or changing its colour or radius does not.

## Camera and keyboard

The app has two interaction modes. **Tab** cycles between them and **Esc** always returns to edit mode.
Switching mode keeps the current camera position. The active mode, view and keys are shown at the
bottom left of the screen.

### Shortcuts that work in every mode

| Key | Action |
|---|---|
| F1-F4 | Select body 1-4 (in list order) |
| F5 | Start / stop integration |
| F6 / F7 | Slower / faster |
| Tab | Next mode |
| Esc | Back to edit mode |

On a Mac keyboard the F keys control brightness and volume by default. Hold **fn**, or enable
"Use F1, F2, etc. keys as standard function keys" in System Settings > Keyboard.
Shortcuts are ignored while you are typing in a text field.

### Preset views (keys 1-6, both modes)

| Key | View |
|---|---|
| 1 | Oblique view from above the reference plane (startup view) |
| 2 | Top view, looking down the z axis (x right, y up) |
| 3 | Side view along the x axis (y right, z up) |
| 4 | Side view along the y axis (x right, z up) |
| 5 | Tracks the primary star: line of sight in its orbital plane, perpendicular to its eccentricity vector |
| 6 | Tracks the primary star: line of sight along its eccentricity vector |

The camera glides to the chosen view and keeps the whole system in frame. Views 5 and 6 follow the
orbit of the first massive body in the list, looking down on its orbital plane from 45 degrees with the
orbit normal pointing up on screen. For a binary this means the camera co-rotates with the binary's
orbital plane and apsidal line. You can then watch how an outer planet's orbit tilts and precesses
*relative to the binary*. If the primary is on a near-circular orbit the node line replaces the
eccentricity vector. If it has no orbit, as for a single star, the reference frame is used instead.

Returning from free-fly leaves the camera where it was until you press 1-6.
In free-fly, 1-6 glide the camera to a snapshot of that view, which then becomes the new starting
point for flying. Views 5 and 6 do not keep tracking in free-fly. Any movement or look input cancels
the glide.
Click a body, or a row in the list, to select it.

### Free-fly mode

The camera behaves like a drone.

| Input | Action |
|---|---|
| Drag (mouse or one-finger trackpad press-and-drag) | Look around |
| W / S | Forward / back along the view direction |
| A / D | Left / right |
| Q / E | Down / up |
| Scroll wheel or two-finger trackpad scroll | Dolly forward / back (a burst of W / S) |
| Shift | Move faster |
| 1-6 | Reset the camera to a preset view and keep flying from there |

Movement speed is set from the size of the system when you enter free-fly. Moving closer does not
change the movement or look sensitivity. A click without dragging still selects bodies.

## Notes

- Rendered body sizes are exaggerated logarithmically so planets stay visible at AU scales.
- The preset views auto-frame the bound orbits. Bodies that become unbound lose their ellipse and
  may leave the view.
