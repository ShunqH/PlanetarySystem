using System;
using System.Collections.Generic;
using PlanetSystem.Physics;
using UnityEngine;

namespace PlanetSystem.Core
{
    /// <summary>
    /// Owns the <see cref="SimulationState"/> and is the single entry point for adding, editing,
    /// removing and selecting bodies. The view and UI layers only talk to this class and react to its events.
    /// Time integration lives in SimulationController.Integration.cs.
    /// </summary>
    public sealed partial class SimulationController : MonoBehaviour
    {
        public SimulationSettings Settings { get; private set; }
        public SimulationState State { get; } = new SimulationState();

        private readonly List<BodyRecord> _records = new List<BodyRecord>();
        private int _nextId = 1;

        /// <summary>Raised after any structural or numerical change to the body set.</summary>
        public event Action BodiesChanged;
        /// <summary>Raised with the newly selected body id, or -1 when the selection is cleared.</summary>
        public event Action<int> SelectionChanged;

        public IReadOnlyList<BodyRecord> Bodies => _records;
        public int SelectedId { get; private set; } = -1;

        public int MassiveCount
        {
            get { int n = 0; foreach (var r in _records) if (r.Body.IsMassive) n++; return n; }
        }

        public int TestParticleCount
        {
            get { int n = 0; foreach (var r in _records) if (!r.Body.IsMassive) n++; return n; }
        }

        public void Initialize(SimulationSettings settings)
        {
            Settings = settings;
            InitializeIntegration();
        }

        public BodyRecord Find(int id)
        {
            foreach (var r in _records) if (r.Id == id) return r;
            return null;
        }

        // ------------------------------------------------------------------
        // Limits
        // ------------------------------------------------------------------

        /// <summary>Checks whether a body of the given kind can be added (or an existing one converted to it).</summary>
        public bool CanHaveKind(BodyKind kind, int excludeId, out string reason)
        {
            int massive = 0, test = 0;
            foreach (var r in _records)
            {
                if (r.Id == excludeId) continue;
                if (r.Body.IsMassive) massive++; else test++;
            }
            if (kind == BodyKind.Massive && massive >= Settings.MaxMassiveBodies)
            {
                reason = $"Limit reached: at most {Settings.MaxMassiveBodies} massive bodies.";
                return false;
            }
            if (kind == BodyKind.TestParticle && test >= Settings.MaxTestParticles)
            {
                reason = $"Limit reached: at most {Settings.MaxTestParticles} test particles.";
                return false;
            }
            reason = null;
            return true;
        }

        // ------------------------------------------------------------------
        // Reference frames
        // ------------------------------------------------------------------

        /// <summary>
        /// Resolves the primary described by <paramref name="referenceId"/>: a specific body, or the barycenter
        /// of all massive bodies except <paramref name="excludeId"/>. Returns false when no mass is available.
        /// </summary>
        public bool TryGetReferenceState(int referenceId, int excludeId, out Vec3d position, out Vec3d velocity, out double mass)
        {
            if (referenceId != BodyDefinition.CenterOfMassReference)
            {
                var rec = Find(referenceId);
                if (rec != null && rec.Id != excludeId && rec.Body.Mass > 0.0)
                {
                    position = rec.Body.Position;
                    velocity = rec.Body.Velocity;
                    mass = rec.Body.Mass;
                    return true;
                }
                // Fall through to the barycenter when the referenced body is gone.
            }
            return State.TryGetCenterOfMass(b => b.IsMassive && b.Id != excludeId, out position, out velocity, out mass);
        }

        /// <summary>Absolute state a body described by <paramref name="def"/> would have right now (before any barycenter shift).</summary>
        public bool TryComputeState(BodyDefinition def, int excludeId, out Vec3d position, out Vec3d velocity, out double mu)
        {
            if (!TryGetReferenceState(def.ReferenceId, excludeId, out var rp, out var rv, out var rm))
            {
                position = Vec3d.Zero;
                velocity = Vec3d.Zero;
                mu = 0.0;
                return false;
            }
            double bodyMass = def.Kind == BodyKind.Massive ? def.Mass : 0.0;
            mu = Constants.G * (rm + bodyMass);
            OrbitConversion.RelativeStateFromElements(def.Elements, mu, out var relP, out var relV);
            position = rp + relP;
            velocity = rv + relV;
            return true;
        }

        /// <summary>Osculating elements of an existing body relative to its reference primary.</summary>
        public bool TryGetElements(BodyRecord rec, out OrbitalElements elements, out Vec3d referencePosition, out double mu)
        {
            elements = default;
            mu = 0.0;
            if (!TryGetReferenceState(rec.ReferenceId, rec.Id, out referencePosition, out var rv, out var rm)) return false;
            mu = Constants.G * (rm + rec.Body.Mass);
            return OrbitConversion.TryElementsFromRelativeState(rec.Body.Position - referencePosition, rec.Body.Velocity - rv, mu, out elements);
        }

        /// <summary>
        /// The orbit that should be <em>drawn</em> for a body, which is not always the orbit used for editing:
        /// <list type="bullet">
        /// <item>Reference is a specific body: the relative orbit around that body (mu = G (M_ref + m)).</item>
        /// <item>Reference is the center of mass and the body is a test particle: orbit around the barycenter (mu = G M).</item>
        /// <item>Reference is the center of mass and the body is massive: its barycentric orbit, using the reduced
        /// gravitational parameter mu = G M_others^3 / M_total^2 (exact for two bodies), so a binary shows two
        /// ellipses around the barycenter instead of one large relative ellipse around a moving star.</item>
        /// </list>
        /// Works for existing bodies (selfId = its id) and for a not-yet-added preview (selfId = -1).
        /// </summary>
        public bool TryGetDrawnOrbit(BodyKind kind, double mass, Vec3d position, Vec3d velocity, int referenceId, int selfId,
            out OrbitalElements elements, out Vec3d center, out double mu)
        {
            elements = default;
            center = Vec3d.Zero;
            mu = 0.0;
            double m = kind == BodyKind.Massive ? mass : 0.0;

            if (referenceId != BodyDefinition.CenterOfMassReference)
            {
                var rec = Find(referenceId);
                if (rec != null && rec.Id != selfId && rec.Body.Mass > 0.0)
                {
                    center = rec.Body.Position;
                    mu = Constants.G * (rec.Body.Mass + m);
                    return OrbitConversion.TryElementsFromRelativeState(position - center, velocity - rec.Body.Velocity, mu, out elements);
                }
            }

            if (!State.TryGetCenterOfMass(b => b.IsMassive && b.Id != selfId, out var cpo, out var cvo, out var mo)) return false;
            if (m <= 0.0)
            {
                center = cpo;
                mu = Constants.G * mo;
                return OrbitConversion.TryElementsFromRelativeState(position - cpo, velocity - cvo, mu, out elements);
            }

            double total = mo + m;
            center = (cpo * mo + position * m) / total;
            var cv = (cvo * mo + velocity * m) / total;
            mu = Constants.G * mo * mo * mo / (total * total);
            return OrbitConversion.TryElementsFromRelativeState(position - center, velocity - cv, mu, out elements);
        }

        /// <summary>Convenience overload for an existing body.</summary>
        public bool TryGetDrawnOrbit(BodyRecord rec, out OrbitalElements elements, out Vec3d center, out double mu)
        {
            return TryGetDrawnOrbit(rec.Body.Kind, rec.Body.Mass, rec.Body.Position, rec.Body.Velocity, rec.ReferenceId, rec.Id,
                out elements, out center, out mu);
        }

        /// <summary>Snapshot of a body as an editable definition (elements taken from the current state).</summary>
        public BodyDefinition ToDefinition(BodyRecord rec)
        {
            var def = new BodyDefinition
            {
                Name = rec.Body.Name,
                Kind = rec.Body.Kind,
                Mass = rec.Body.Mass,
                Radius = rec.Body.Radius,
                Color = rec.Color,
                ReferenceId = rec.ReferenceId,
            };
            if (TryGetElements(rec, out var el, out _, out _)) def.Elements = el;
            return def;
        }

        // ------------------------------------------------------------------
        // Mutations
        // ------------------------------------------------------------------

        /// <summary>
        /// Adds a body. The very first body (or any body added while no massive body exists) is placed at
        /// the origin at rest; otherwise the orbital elements are applied relative to the chosen primary
        /// and the whole system is shifted to its barycenter, as in Rebound.
        /// </summary>
        public BodyRecord AddBody(BodyDefinition def, out string error)
        {
            if (!EnsureEditable(out error)) return null;
            if (!CanHaveKind(def.Kind, -1, out error)) return null;
            if (!ValidateDefinition(def, out error)) return null;

            var body = new Body(_nextId++, def.Name, def.Kind, def.Mass, def.Radius);
            if (TryComputeState(def, -1, out var p, out var v, out _))
            {
                body.Position = p;
                body.Velocity = v;
            }
            else
            {
                body.Position = Vec3d.Zero;
                body.Velocity = Vec3d.Zero;
            }

            var rec = new BodyRecord(body, def.Color, def.ReferenceId);
            State.Bodies.Add(body);
            _records.Add(rec);
            State.MoveToCenterOfMass();
            CaptureInitialConditions();

            BodiesChanged?.Invoke();
            return rec;
        }

        /// <summary>
        /// Applies an edited definition to an existing body and re-centers the system.
        /// Purely cosmetic edits (name, color, radius) keep the current state and simulation time;
        /// any dynamical edit defines new initial conditions and resets the clock to t = 0.
        /// </summary>
        public bool UpdateBody(int id, BodyDefinition def, out string error)
        {
            if (!EnsureEditable(out error)) return false;
            var rec = Find(id);
            if (rec == null) { error = "Body not found."; return false; }
            if (def.ReferenceId == id) { error = "A body cannot orbit itself."; return false; }
            if (rec.Body.Kind != def.Kind && !CanHaveKind(def.Kind, id, out error)) return false;
            if (!ValidateDefinition(def, out error)) return false;

            bool dynamical = IsDynamicalChange(rec, def);
            var body = rec.Body;
            body.Name = def.Name;
            body.Kind = def.Kind;
            body.Mass = def.Kind == BodyKind.Massive ? def.Mass : 0.0;
            body.Radius = def.Radius;
            rec.Color = def.Color;
            rec.ReferenceId = def.ReferenceId;

            if (!dynamical)
            {
                BodiesChanged?.Invoke();
                return true;
            }

            if (TryComputeState(def, id, out var p, out var v, out _))
            {
                body.Position = p;
                body.Velocity = v;
            }
            // A body with no resolvable primary (e.g. the only massive body) keeps its position.

            State.MoveToCenterOfMass();
            CaptureInitialConditions();
            BodiesChanged?.Invoke();
            return true;
        }

        /// <summary>True when the edit changes anything that affects the motion.</summary>
        private bool IsDynamicalChange(BodyRecord rec, BodyDefinition def)
        {
            var current = ToDefinition(rec);
            if (current.Kind != def.Kind || current.ReferenceId != def.ReferenceId) return true;
            if (def.Kind == BodyKind.Massive && !Close(current.Mass, def.Mass, 1e-12)) return true;
            var a = current.Elements;
            var b = def.Elements;
            return !Close(a.SemiMajorAxis, b.SemiMajorAxis, 1e-10) ||
                   Math.Abs(a.Eccentricity - b.Eccentricity) > 1e-10 ||
                   AngleDiff(a.Inclination, b.Inclination) > 1e-9 ||
                   AngleDiff(a.LongitudeOfAscendingNode, b.LongitudeOfAscendingNode) > 1e-9 ||
                   AngleDiff(a.ArgumentOfPericenter, b.ArgumentOfPericenter) > 1e-9 ||
                   AngleDiff(a.TrueAnomaly, b.TrueAnomaly) > 1e-9;
        }

        private static bool Close(double a, double b, double rel) => Math.Abs(a - b) <= rel * Math.Max(Math.Abs(a), Math.Abs(b));

        private static double AngleDiff(double a, double b)
        {
            double d = Constants.WrapTwoPi(a - b);
            return Math.Min(d, Constants.TwoPi - d);
        }

        public void RemoveBody(int id)
        {
            if (!EnsureEditable(out _)) return;
            var rec = Find(id);
            if (rec == null) return;
            _records.Remove(rec);
            State.Bodies.Remove(rec.Body);

            // Bodies that used the removed one as primary fall back to the barycenter.
            foreach (var r in _records)
                if (r.ReferenceId == id) r.ReferenceId = BodyDefinition.CenterOfMassReference;

            State.MoveToCenterOfMass();
            CaptureInitialConditions();
            if (SelectedId == id) Select(-1);
            BodiesChanged?.Invoke();
        }

        public void Clear()
        {
            StopIntegration();
            _records.Clear();
            State.Bodies.Clear();
            CaptureInitialConditions();
            Select(-1);
            BodiesChanged?.Invoke();
        }

        private static bool ValidateDefinition(BodyDefinition def, out string error)
        {
            if (string.IsNullOrWhiteSpace(def.Name)) { error = "Name cannot be empty."; return false; }
            if (def.Kind == BodyKind.Massive && def.Mass <= 0.0) { error = "Massive bodies need a positive mass."; return false; }
            if (def.Radius <= 0.0) { error = "Radius must be positive."; return false; }
            var el = def.Elements;
            if (el.SemiMajorAxis <= 0.0) { error = "Semi-major axis must be positive."; return false; }
            if (el.Eccentricity < 0.0 || el.Eccentricity >= 1.0) { error = "Eccentricity must be in [0, 1)."; return false; }
            error = null;
            return true;
        }

        // ------------------------------------------------------------------
        // Selection
        // ------------------------------------------------------------------

        public void Select(int id)
        {
            if (id != -1 && Find(id) == null) id = -1;
            if (SelectedId == id) return;
            SelectedId = id;
            SelectionChanged?.Invoke(id);
        }

        // ------------------------------------------------------------------
        // Defaults for the add form
        // ------------------------------------------------------------------

        /// <summary>Sensible defaults for the next body, depending on what already exists.</summary>
        public BodyDefinition SuggestNewBody()
        {
            int massive = MassiveCount;
            int total = _records.Count;
            var def = new BodyDefinition { Color = Palette.Pick(total) };

            if (total == 0)
            {
                def.Name = "Star A";
                def.Kind = BodyKind.Massive;
                def.Mass = 1.0;
                def.Radius = Constants.SolarRadiusInAu;
            }
            else if (massive == 1)
            {
                // Second body: default to a stellar companion, since circumbinary systems are the main use case.
                def.Name = "Star B";
                def.Kind = BodyKind.Massive;
                def.Mass = 0.5;
                def.Radius = 0.6 * Constants.SolarRadiusInAu;
                def.ReferenceId = BodyDefinition.CenterOfMassReference;
                def.Elements = new OrbitalElements { SemiMajorAxis = 0.2, Eccentricity = 0.1 };
            }
            else
            {
                def.Name = $"Planet {(char)('a' + Math.Max(0, total - 2))}";
                def.Kind = BodyKind.TestParticle;
                def.Mass = 0.0;
                def.Radius = Constants.JupiterRadiusInAu;
                def.ReferenceId = BodyDefinition.CenterOfMassReference;
                def.Elements = new OrbitalElements { SemiMajorAxis = 1.0, Eccentricity = 0.05 };
            }

            if (!CanHaveKind(def.Kind, -1, out _))
            {
                def.Kind = def.Kind == BodyKind.Massive ? BodyKind.TestParticle : BodyKind.Massive;
                if (def.Kind == BodyKind.Massive && def.Mass <= 0.0) def.Mass = Constants.JupiterMassInSolar;
            }
            return def;
        }

        /// <summary>Loads a small circumbinary example so the demo is not empty on first launch.</summary>
        public void LoadCircumbinaryExample()
        {
            Clear();
            AddBody(new BodyDefinition
            {
                Name = "Star A", Kind = BodyKind.Massive, Mass = 0.69, Radius = 0.65 * Constants.SolarRadiusInAu,
                Color = Palette.Pick(0),
            }, out _);
            AddBody(new BodyDefinition
            {
                Name = "Star B", Kind = BodyKind.Massive, Mass = 0.20, Radius = 0.23 * Constants.SolarRadiusInAu,
                Color = Palette.Pick(2), ReferenceId = BodyDefinition.CenterOfMassReference,
                Elements = new OrbitalElements { SemiMajorAxis = 0.22, Eccentricity = 0.16 },
            }, out _);
            AddBody(new BodyDefinition
            {
                Name = "Planet b", Kind = BodyKind.TestParticle, Radius = 0.75 * Constants.JupiterRadiusInAu,
                Color = Palette.Pick(3),
                Elements = new OrbitalElements { SemiMajorAxis = 0.70, Eccentricity = 0.01, Inclination = Constants.DegToRad(1.0) },
            }, out _);
            AddBody(new BodyDefinition
            {
                Name = "Test c", Kind = BodyKind.TestParticle, Radius = Constants.EarthRadiusInAu,
                Color = Palette.Pick(6),
                Elements = new OrbitalElements
                {
                    SemiMajorAxis = 1.3, Eccentricity = 0.2, Inclination = Constants.DegToRad(45.0),
                    LongitudeOfAscendingNode = Constants.DegToRad(60.0), ArgumentOfPericenter = Constants.DegToRad(30.0),
                },
            }, out _);
        }
    }
}
