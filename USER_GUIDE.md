# PlanetSystem — User Guide

An interactive 3D sandbox for building planetary systems, aimed at newcomers to planetary dynamics.
This version lets you assemble a system and inspect its orbits; time evolution is coming next.

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

## Top bar

- **Load example** — a Kepler-16-like binary with a circumbinary planet and a highly inclined test particle.
- **Clear all** — start from scratch.

## Notes

- Rendered body sizes are exaggerated logarithmically so planets stay visible at AU scales.
- The camera auto-frames the system; interactive camera controls arrive in a later phase.
