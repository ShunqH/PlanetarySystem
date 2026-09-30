using System;

namespace PlanetSystem.Physics.Integrators
{
    /// <summary>
    /// Adaptive explicit Runge-Kutta 5(4) of Dormand and Prince with FSAL (six new force evaluations
    /// per accepted step). The step size follows the local error, so close encounters and very eccentric
    /// orbits are resolved automatically. Not symplectic: energy error slowly drifts, at a rate set by
    /// the tolerance. The most expensive but most robust choice.
    /// </summary>
    public sealed class DormandPrince5Integrator : IIntegrator
    {
        // Butcher tableau
        private const double A21 = 1.0 / 5.0;
        private const double A31 = 3.0 / 40.0, A32 = 9.0 / 40.0;
        private const double A41 = 44.0 / 45.0, A42 = -56.0 / 15.0, A43 = 32.0 / 9.0;
        private const double A51 = 19372.0 / 6561.0, A52 = -25360.0 / 2187.0, A53 = 64448.0 / 6561.0, A54 = -212.0 / 729.0;
        private const double A61 = 9017.0 / 3168.0, A62 = -355.0 / 33.0, A63 = 46732.0 / 5247.0, A64 = 49.0 / 176.0, A65 = -5103.0 / 18656.0;
        private const double B1 = 35.0 / 384.0, B3 = 500.0 / 1113.0, B4 = 125.0 / 192.0, B5 = -2187.0 / 6784.0, B6 = 11.0 / 84.0;
        // Error coefficients: 5th-order minus embedded 4th-order weights.
        private const double E1 = 71.0 / 57600.0, E3 = -71.0 / 16695.0, E4 = 71.0 / 1920.0,
            E5 = -17253.0 / 339200.0, E6 = 22.0 / 525.0, E7 = -1.0 / 40.0;

        private const double Safety = 0.9, MinFactor = 0.2, MaxFactor = 5.0;

        private readonly double _rtol;
        private readonly double _atol;

        private double[] _y = Array.Empty<double>(), _yn = Array.Empty<double>(), _tmp = Array.Empty<double>();
        private double[] _k1 = Array.Empty<double>(), _k2 = Array.Empty<double>(), _k3 = Array.Empty<double>(),
            _k4 = Array.Empty<double>(), _k5 = Array.Empty<double>(), _k6 = Array.Empty<double>(), _k7 = Array.Empty<double>();
        private int _dim;
        private double _h;
        private double _lastStep;

        /// <param name="tolerance">Relative error tolerance per step (e.g. 1e-10).</param>
        public DormandPrince5Integrator(double tolerance)
        {
            _rtol = Math.Max(tolerance, 1e-15);
            _atol = _rtol * 1e-4;
        }

        public string Name => "Dormand-Prince 5(4)";
        public double StepSize => _lastStep;

        public void Reset(ParticleArrays p, double frameDuration)
        {
            _dim = 6 * p.Count;
            if (_y.Length < _dim)
            {
                _y = new double[_dim]; _yn = new double[_dim]; _tmp = new double[_dim];
                _k1 = new double[_dim]; _k2 = new double[_dim]; _k3 = new double[_dim]; _k4 = new double[_dim];
                _k5 = new double[_dim]; _k6 = new double[_dim]; _k7 = new double[_dim];
            }
            double tau = Timescales.ShortestTimescale(p);
            _h = double.IsInfinity(tau) ? frameDuration : Math.Min(frameDuration, 0.01 * tau);
            _h = Math.Max(_h, 1e-12);
            _lastStep = _h;

            Pack(p, _y);
            p.DetectCollisions = false;
            Derivative(p, _y, _k1);
        }

        public IntegrationResult Advance(ParticleArrays p, double duration, int maxForceEvaluations)
        {
            var result = new IntegrationResult();
            long before = p.ForceEvaluations;
            double remaining = duration;
            p.ClearCollision();

            while (remaining > 1e-15 * Math.Max(1.0, Math.Abs(p.Time)))
            {
                if (p.ForceEvaluations - before + 6 > maxForceEvaluations)
                {
                    result.BudgetLimited = true;
                    break;
                }

                bool clamped = _h >= remaining;
                double h = clamped ? remaining : _h;

                // Stages 2-6 (collision detection off: trial positions are not physical).
                p.DetectCollisions = false;
                Combine(_tmp, _y, h, A21, _k1);
                Derivative(p, _tmp, _k2);
                Combine(_tmp, _y, h, A31, _k1, A32, _k2);
                Derivative(p, _tmp, _k3);
                Combine(_tmp, _y, h, A41, _k1, A42, _k2, A43, _k3);
                Derivative(p, _tmp, _k4);
                Combine(_tmp, _y, h, A51, _k1, A52, _k2, A53, _k3, A54, _k4);
                Derivative(p, _tmp, _k5);
                Combine(_tmp, _y, h, A61, _k1, A62, _k2, A63, _k3, A64, _k4, A65, _k5);
                Derivative(p, _tmp, _k6);

                // 5th-order solution; its derivative (k7) is evaluated at the candidate end point (FSAL).
                for (int i = 0; i < _dim; i++)
                    _yn[i] = _y[i] + h * (B1 * _k1[i] + B3 * _k3[i] + B4 * _k4[i] + B5 * _k5[i] + B6 * _k6[i]);
                p.DetectCollisions = true;
                p.ClearCollision();
                Derivative(p, _yn, _k7);

                double err = 0.0;
                for (int i = 0; i < _dim; i++)
                {
                    double e = h * (E1 * _k1[i] + E3 * _k3[i] + E4 * _k4[i] + E5 * _k5[i] + E6 * _k6[i] + E7 * _k7[i]);
                    double sc = _atol + _rtol * Math.Max(Math.Abs(_y[i]), Math.Abs(_yn[i]));
                    double r = Math.Abs(e) / sc;
                    if (r > err) err = r;
                }
                if (double.IsNaN(err)) err = double.PositiveInfinity;

                double factor = err == 0.0 ? MaxFactor : Math.Min(MaxFactor, Math.Max(MinFactor, Safety * Math.Pow(err, -0.2)));
                double hMin = 1e-14 * Math.Max(1.0, Math.Abs(p.Time));

                if (err <= 1.0 || h <= hMin)
                {
                    // Accept.
                    Swap(ref _y, ref _yn);
                    Swap(ref _k1, ref _k7);
                    p.Time += h;
                    remaining -= h;
                    result.Steps++;
                    _lastStep = h;
                    double hNew = h * factor;
                    // A step shortened only to land on the frame boundary must not shrink future steps.
                    _h = clamped ? Math.Max(_h, hNew) : hNew;
                    if (p.HasCollision) { result.Collision = true; break; }
                }
                else
                {
                    // Reject and retry with a smaller step.
                    p.ClearCollision();
                    _h = Math.Max(h * Math.Min(1.0, factor), hMin);
                }
            }

            Unpack(_y, p);
            result.Advanced = duration - Math.Max(remaining, 0.0);
            result.Evaluations = (int)(p.ForceEvaluations - before);
            return result;
        }

        // ------------------------------------------------------------------
        // State vector helpers. Layout per particle: x, y, z, vx, vy, vz.
        // ------------------------------------------------------------------

        private static void Pack(ParticleArrays p, double[] y)
        {
            for (int i = 0, k = 0; i < p.Count; i++, k += 6)
            {
                y[k] = p.X[i]; y[k + 1] = p.Y[i]; y[k + 2] = p.Z[i];
                y[k + 3] = p.VX[i]; y[k + 4] = p.VY[i]; y[k + 5] = p.VZ[i];
            }
        }

        private static void Unpack(double[] y, ParticleArrays p)
        {
            for (int i = 0, k = 0; i < p.Count; i++, k += 6)
            {
                p.X[i] = y[k]; p.Y[i] = y[k + 1]; p.Z[i] = y[k + 2];
                p.VX[i] = y[k + 3]; p.VY[i] = y[k + 4]; p.VZ[i] = y[k + 5];
            }
        }

        /// <summary>dy/dt = (v, a(x)). Positions are written into p so the shared gravity kernel can be used.</summary>
        private static void Derivative(ParticleArrays p, double[] y, double[] dy)
        {
            for (int i = 0, k = 0; i < p.Count; i++, k += 6)
            {
                p.X[i] = y[k]; p.Y[i] = y[k + 1]; p.Z[i] = y[k + 2];
            }
            Gravity.Compute(p);
            for (int i = 0, k = 0; i < p.Count; i++, k += 6)
            {
                dy[k] = y[k + 3]; dy[k + 1] = y[k + 4]; dy[k + 2] = y[k + 5];
                dy[k + 3] = p.AX[i]; dy[k + 4] = p.AY[i]; dy[k + 5] = p.AZ[i];
            }
        }

        private void Combine(double[] o, double[] y, double h, double a1, double[] k1)
        {
            for (int i = 0; i < _dim; i++) o[i] = y[i] + h * (a1 * k1[i]);
        }

        private void Combine(double[] o, double[] y, double h, double a1, double[] k1, double a2, double[] k2)
        {
            for (int i = 0; i < _dim; i++) o[i] = y[i] + h * (a1 * k1[i] + a2 * k2[i]);
        }

        private void Combine(double[] o, double[] y, double h, double a1, double[] k1, double a2, double[] k2, double a3, double[] k3)
        {
            for (int i = 0; i < _dim; i++) o[i] = y[i] + h * (a1 * k1[i] + a2 * k2[i] + a3 * k3[i]);
        }

        private void Combine(double[] o, double[] y, double h, double a1, double[] k1, double a2, double[] k2,
            double a3, double[] k3, double a4, double[] k4)
        {
            for (int i = 0; i < _dim; i++) o[i] = y[i] + h * (a1 * k1[i] + a2 * k2[i] + a3 * k3[i] + a4 * k4[i]);
        }

        private void Combine(double[] o, double[] y, double h, double a1, double[] k1, double a2, double[] k2,
            double a3, double[] k3, double a4, double[] k4, double a5, double[] k5)
        {
            for (int i = 0; i < _dim; i++) o[i] = y[i] + h * (a1 * k1[i] + a2 * k2[i] + a3 * k3[i] + a4 * k4[i] + a5 * k5[i]);
        }

        private static void Swap(ref double[] a, ref double[] b)
        {
            var t = a;
            a = b;
            b = t;
        }
    }
}
