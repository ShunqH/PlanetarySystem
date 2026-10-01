using System;
using PlanetSystem.Data;
using PlanetSystem.Physics;
using UnityEngine;

namespace PlanetSystem.Core
{
    /// <summary>Loading cases into the simulation and capturing the current system as a case.</summary>
    public sealed partial class SimulationController
    {
        private bool _loadingScenario;

        /// <summary>True when bodies were added or edited since the last load or save.</summary>
        public bool HasUnsavedChanges { get; private set; }

        /// <summary>Call after the current system has been saved.</summary>
        public void MarkSaved() => HasUnsavedChanges = false;

        /// <summary>
        /// Replaces the current system with a case. The loaded state becomes the new initial conditions
        /// (t = 0). The case may also choose the integrator and speed. Returns an error message, or null.
        /// </summary>
        public string LoadScenario(Scenario scenario)
        {
            if (IsRunning) return "Stop the integration before loading a case.";
            string invalid = ScenarioBuilder.Validate(scenario);
            if (invalid != null) return invalid;

            _loadingScenario = true;
            _suppressCountWarnings = true;
            try
            {
                Clear();
                var built = ScenarioBuilder.Build(scenario, _nextId);
                _nextId += built.Bodies.Count;
                for (int i = 0; i < built.Bodies.Count; i++)
                {
                    var body = built.Bodies[i];
                    var sb = scenario.Bodies[i];
                    int refIndex = sb.ReferenceIndex;
                    int referenceId = refIndex >= 0 && refIndex < i && built.Bodies[refIndex].IsMassive
                        ? built.Bodies[refIndex].Id
                        : BodyDefinition.CenterOfMassReference;
                    var color = new Color((float)sb.Color[0], (float)sb.Color[1], (float)sb.Color[2], 1f);
                    State.Bodies.Add(body);
                    _records.Add(new BodyRecord(body, color, referenceId));
                }
                State.MoveToCenterOfMass();
                CaptureInitialConditions();

                if (scenario.IntegratorIndex >= 0 && scenario.IntegratorIndex < Physics.Integrators.IntegratorCatalog.Count)
                {
                    IntegratorIndex = scenario.IntegratorIndex;
                    _integrator = null;
                }
                if (scenario.Speed > 0.0) SpeedIndex = ClosestSpeedIndex(scenario.Speed);
                HasUnsavedChanges = false;
            }
            finally
            {
                _loadingScenario = false;
                _suppressCountWarnings = false;
            }

            SetStatus($"Loaded \"{scenario.Name}\".");
            BodiesChanged?.Invoke();
            CheckBodyCountWarnings();
            return null;
        }

        /// <summary>
        /// Snapshot of the current system (positions, velocities and appearance of every body, plus the
        /// integrator and speed). Saving after a run stores the evolved state, like a game save.
        /// </summary>
        public Scenario CaptureScenario(string name)
        {
            var s = new Scenario
            {
                Name = name.Trim(),
                SavedAt = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss"),
                IntegratorIndex = IntegratorIndex,
                Speed = Speed,
                Description = State.Time > 0.0 ? $"Saved at t = {State.Time:0.###} yr of a run." : "",
            };
            for (int i = 0; i < _records.Count; i++)
            {
                var rec = _records[i];
                int refIndex = -1;
                for (int k = 0; k < _records.Count; k++)
                    if (_records[k].Id == rec.ReferenceId) { refIndex = k; break; }
                // References are stored by list position and must point to an earlier body.
                if (refIndex >= i) refIndex = -1;
                s.Bodies.Add(new ScenarioBody
                {
                    Name = rec.Body.Name,
                    Kind = rec.Body.Kind,
                    Mass = rec.Body.Mass,
                    Radius = rec.Body.Radius,
                    Color = new double[] { rec.Color.r, rec.Color.g, rec.Color.b },
                    ReferenceIndex = refIndex,
                    UsesElements = false,
                    Position = rec.Body.Position,
                    Velocity = rec.Body.Velocity,
                });
            }
            return s;
        }
    }
}
