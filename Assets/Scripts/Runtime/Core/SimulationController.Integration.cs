using System;
using System.Collections.Generic;
using PlanetSystem.Physics;
using PlanetSystem.Physics.Integrators;
using UnityEngine;

namespace PlanetSystem.Core
{
    /// <summary>
    /// Time integration: start/stop, speed and integrator selection, per-frame advance, diagnostics and
    /// restoring the initial conditions. Bodies cannot be added, edited or removed while running.
    /// </summary>
    public sealed partial class SimulationController
    {
        /// <summary>Time acceleration choices in simulated years per real second.</summary>
        public static readonly double[] SpeedOptions =
        {
            0.01, 0.02, 0.05, 0.1, 0.2, 0.5, 1, 2, 5, 10, 20, 50, 100, 200, 500, 1000,
        };

        /// <summary>Largest real frame time fed to the integrator; longer hitches are not caught up.</summary>
        private const float MaxRealDeltaTime = 0.1f;

        private readonly ParticleArrays _particles = new ParticleArrays();
        private readonly Dictionary<int, (Vec3d Position, Vec3d Velocity)> _initial = new Dictionary<int, (Vec3d, Vec3d)>();
        private IIntegrator _integrator;
        private double _initialEnergy;
        private double _msPerEvaluation = 1e-3;
        private readonly System.Diagnostics.Stopwatch _stopwatch = new System.Diagnostics.Stopwatch();

        public bool IsRunning { get; private set; }
        public int IntegratorIndex { get; private set; }
        public int SpeedIndex { get; private set; }
        public double Speed => SpeedOptions[SpeedIndex];

        /// <summary>Step size of the running (or last used) integrator [yr]; NaN before the first run.</summary>
        public double StepSize => _integrator != null ? _integrator.StepSize : double.NaN;

        /// <summary>|E - E0| / |E0| of the massive bodies since the initial conditions; NaN when undefined.</summary>
        public double RelativeEnergyError { get; private set; } = double.NaN;

        /// <summary>Smoothed achieved speed in years per real second.</summary>
        public double EffectiveSpeed { get; private set; }

        /// <summary>Result of the most recent frame.</summary>
        public IntegrationResult LastResult { get; private set; }

        public string StatusMessage { get; private set; } = "";
        public bool StatusIsError { get; private set; }

        /// <summary>Raised when integration starts or stops.</summary>
        public event Action RunStateChanged;

        private double FrameDuration => Speed / Mathf.Max(1, Settings.TargetFrameRate);

        private IntegratorOptions Options => new IntegratorOptions
        {
            LeapfrogEta = Settings.LeapfrogStepFraction,
            YoshidaEta = Settings.YoshidaStepFraction,
            AdaptiveTolerance = Settings.AdaptiveTolerance,
        };

        private void InitializeIntegration()
        {
            IntegratorIndex = Mathf.Clamp(Settings.DefaultIntegrator, 0, IntegratorCatalog.Count - 1);
            SpeedIndex = ClosestSpeedIndex(Settings.DefaultSpeed);
        }

        private static int ClosestSpeedIndex(double speed)
        {
            int best = 0;
            for (int i = 1; i < SpeedOptions.Length; i++)
                if (Math.Abs(Math.Log(SpeedOptions[i] / speed)) < Math.Abs(Math.Log(SpeedOptions[best] / speed))) best = i;
            return best;
        }

        // ------------------------------------------------------------------
        // Controls
        // ------------------------------------------------------------------

        /// <summary>Integrator choice is locked while running (the step sequence must not change mid-run).</summary>
        public void SetIntegrator(int index)
        {
            if (IsRunning) return;
            IntegratorIndex = Mathf.Clamp(index, 0, IntegratorCatalog.Count - 1);
            _integrator = null;
            SetStatus(IntegratorCatalog.Descriptions[IntegratorIndex]);
        }

        /// <summary>Changing the speed while running re-derives the step size for the new frame duration.</summary>
        public void SetSpeed(int index)
        {
            SpeedIndex = Mathf.Clamp(index, 0, SpeedOptions.Length - 1);
            if (IsRunning) _integrator.Reset(_particles, FrameDuration);
        }

        public bool StartIntegration()
        {
            if (IsRunning) return true;
            if (_records.Count < 2)
            {
                SetStatus("Add at least two bodies before integrating.", true);
                return false;
            }
            if (MassiveCount == 0)
            {
                SetStatus("At least one massive body is needed to provide gravity.", true);
                return false;
            }

            double soft = Settings.SofteningAu;
            _particles.Load(State);
            _particles.Softening2 = soft * soft;

            // Refuse to start from overlapping bodies.
            _particles.DetectCollisions = true;
            Gravity.Compute(_particles);
            if (_particles.HasCollision)
            {
                SetStatus($"{PairNames()} overlap. Press Reset or move them apart before integrating.", true);
                return false;
            }

            _integrator = IntegratorCatalog.Create(IntegratorIndex, Options);
            _integrator.Reset(_particles, FrameDuration);
            EffectiveSpeed = Speed;
            IsRunning = true;
            SetStatus($"Integrating with {_integrator.Name}.");
            RunStateChanged?.Invoke();
            return true;
        }

        public void StopIntegration()
        {
            if (!IsRunning) return;
            IsRunning = false;
            _particles.Store(State);
            RunStateChanged?.Invoke();
            BodiesChanged?.Invoke();
        }

        /// <summary>Restores positions and velocities recorded after the last edit, and sets t = 0.</summary>
        public void ResetToInitialConditions()
        {
            bool wasRunning = IsRunning;
            IsRunning = false;
            foreach (var rec in _records)
            {
                if (!_initial.TryGetValue(rec.Id, out var s)) continue;
                rec.Body.Position = s.Position;
                rec.Body.Velocity = s.Velocity;
            }
            State.Time = 0.0;
            RelativeEnergyError = double.NaN;
            if (wasRunning) RunStateChanged?.Invoke();
            SetStatus("Restored the initial conditions (t = 0).");
            BodiesChanged?.Invoke();
        }

        /// <summary>Records the current state as the initial conditions and resets the clock.</summary>
        private void CaptureInitialConditions()
        {
            if (!_loadingScenario) HasUnsavedChanges = _records.Count > 0;
            _initial.Clear();
            foreach (var rec in _records) _initial[rec.Id] = (rec.Body.Position, rec.Body.Velocity);
            State.Time = 0.0;
            double soft = Settings.SofteningAu;
            _initialEnergy = Diagnostics.MassiveEnergy(State.Bodies, soft * soft);
            RelativeEnergyError = double.NaN;
        }

        private bool EnsureEditable(out string error)
        {
            if (IsRunning)
            {
                error = "Stop the integration before changing bodies.";
                return false;
            }
            error = null;
            return true;
        }

        // ------------------------------------------------------------------
        // Per-frame advance
        // ------------------------------------------------------------------

        private void Update()
        {
            if (!IsRunning) return;

            float real = Mathf.Min(Time.unscaledDeltaTime, MaxRealDeltaTime);
            if (real <= 0f) return;
            double duration = Speed * real;

            // Convert the per-frame CPU allowance into a force-evaluation budget using measured cost.
            int budget = (int)Math.Clamp(Settings.MaxIntegrationMsPerFrame / _msPerEvaluation, 50.0, 5_000_000.0);

            _stopwatch.Restart();
            var result = _integrator.Advance(_particles, duration, budget);
            _stopwatch.Stop();
            if (result.Evaluations > 20)
            {
                double ms = _stopwatch.Elapsed.TotalMilliseconds / result.Evaluations;
                _msPerEvaluation = 0.8 * _msPerEvaluation + 0.2 * ms;
            }
            LastResult = result;
            EffectiveSpeed = 0.9 * EffectiveSpeed + 0.1 * (result.Advanced / real);

            if (!_particles.AllFinite())
            {
                ResetToInitialConditions();
                SetStatus("Numerical blow-up (NaN). Restored the initial conditions; try the adaptive integrator.", true);
                return;
            }

            _particles.Store(State);
            UpdateEnergyError();

            if (result.Collision)
            {
                string names = PairNames();
                StopIntegration();
                SetStatus($"Collision: {names} at t = {State.Time:0.###} yr. Integration stopped.", true);
                return;
            }

            if (result.BudgetLimited)
                SetStatus($"CPU limit reached: running at {EffectiveSpeed:0.##} yr/s instead of {Speed:0.##} yr/s.");
            else if (StatusIsError == false && StatusMessage.StartsWith("CPU limit"))
                SetStatus($"Integrating with {_integrator.Name}.");
        }

        private void UpdateEnergyError()
        {
            if (MassiveCount < 2 || Math.Abs(_initialEnergy) < 1e-300)
            {
                RelativeEnergyError = double.NaN;
                return;
            }
            double soft = Settings.SofteningAu;
            double e = Diagnostics.MassiveEnergy(State.Bodies, soft * soft);
            RelativeEnergyError = Math.Abs((e - _initialEnergy) / _initialEnergy);
        }

        private string PairNames()
        {
            if (!_particles.HasCollision) return "Two bodies";
            return $"{_particles.Source[_particles.CollisionA].Name} and {_particles.Source[_particles.CollisionB].Name}";
        }

        private void SetStatus(string message, bool isError = false)
        {
            StatusMessage = message ?? "";
            StatusIsError = isError;
        }
    }
}
