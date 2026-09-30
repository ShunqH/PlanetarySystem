using System;

namespace PlanetSystem.Physics
{
    /// <summary>
    /// Conversions between orbital elements and Cartesian state vectors, ported from Rebound
    /// (reb_particle_from_orbit / reb_orbit_from_particle). All vectors are relative to the primary.
    /// </summary>
    public static class OrbitConversion
    {
        private const double MinRelError = 1e-12;
        private const double MinInc = 1e-8;

        /// <summary>
        /// Computes the position and velocity of a body relative to its primary from orbital elements.
        /// mu is G (M_primary + m_body).
        /// </summary>
        public static void RelativeStateFromElements(in OrbitalElements el, double mu, out Vec3d position, out Vec3d velocity)
        {
            double a = el.SemiMajorAxis;
            double e = el.Eccentricity;
            if (a <= 0.0) throw new ArgumentException("Semi-major axis must be positive for elliptic orbits.");
            if (e < 0.0 || e >= 1.0) throw new ArgumentException("Eccentricity must be in [0, 1).");

            double f = el.TrueAnomaly;
            double r = a * (1.0 - e * e) / (1.0 + e * Math.Cos(f));
            double v0 = Math.Sqrt(mu / (a * (1.0 - e * e)));

            double cO = Math.Cos(el.LongitudeOfAscendingNode), sO = Math.Sin(el.LongitudeOfAscendingNode);
            double co = Math.Cos(el.ArgumentOfPericenter), so = Math.Sin(el.ArgumentOfPericenter);
            double cf = Math.Cos(f), sf = Math.Sin(f);
            double ci = Math.Cos(el.Inclination), si = Math.Sin(el.Inclination);

            position = new Vec3d(
                r * (cO * (co * cf - so * sf) - sO * (so * cf + co * sf) * ci),
                r * (sO * (co * cf - so * sf) + cO * (so * cf + co * sf) * ci),
                r * (so * cf + co * sf) * si);

            velocity = new Vec3d(
                v0 * ((e + cf) * (-ci * co * sO - cO * so) - sf * (co * cO - ci * so * sO)),
                v0 * ((e + cf) * (ci * co * cO - sO * so) - sf * (co * sO + ci * so * cO)),
                v0 * ((e + cf) * co * si - sf * si * so));
        }

        /// <summary>
        /// Position relative to the primary at an arbitrary true anomaly, keeping all other elements fixed.
        /// Used to sample the osculating ellipse for rendering.
        /// </summary>
        public static Vec3d PositionAtTrueAnomaly(in OrbitalElements el, double f)
        {
            double a = el.SemiMajorAxis;
            double e = el.Eccentricity;
            double r = a * (1.0 - e * e) / (1.0 + e * Math.Cos(f));

            double cO = Math.Cos(el.LongitudeOfAscendingNode), sO = Math.Sin(el.LongitudeOfAscendingNode);
            double co = Math.Cos(el.ArgumentOfPericenter), so = Math.Sin(el.ArgumentOfPericenter);
            double cf = Math.Cos(f), sf = Math.Sin(f);
            double ci = Math.Cos(el.Inclination), si = Math.Sin(el.Inclination);

            return new Vec3d(
                r * (cO * (co * cf - so * sf) - sO * (so * cf + co * sf) * ci),
                r * (sO * (co * cf - so * sf) + cO * (so * cf + co * sf) * ci),
                r * (so * cf + co * sf) * si);
        }

        /// <summary>Fills the buffer with points along the full ellipse, equally spaced in true anomaly.</summary>
        public static void SampleOrbit(in OrbitalElements el, Vec3d[] buffer)
        {
            int n = buffer.Length;
            for (int i = 0; i < n; i++)
            {
                double f = Constants.TwoPi * i / n;
                buffer[i] = PositionAtTrueAnomaly(el, f);
            }
        }

        /// <summary>
        /// Computes osculating orbital elements from a relative state vector.
        /// Returns false if the orbit is unbound (e &gt;= 1 or a &lt;= 0) or the state is degenerate.
        /// </summary>
        public static bool TryElementsFromRelativeState(Vec3d d, Vec3d dv, double mu, out OrbitalElements o)
        {
            o = default;
            if (mu <= 0.0) return false;

            double r = d.Length;
            if (r < MinRelError) return false;

            double vsq = dv.LengthSquared;
            double vcircsq = mu / r;
            double vdiffsq = vsq - vcircsq;
            double vr = Vec3d.Dot(d, dv) / r;

            Vec3d h = Vec3d.Cross(d, dv);
            double hmag = h.Length;
            if (hmag < MinRelError) return false;

            double denom = vsq - 2.0 * mu / r;
            if (denom >= 0.0) return false; // unbound
            double a = -mu / denom;

            double muinv = 1.0 / mu;
            Vec3d evec = new Vec3d(
                muinv * (vdiffsq * d.X - r * vr * dv.X),
                muinv * (vdiffsq * d.Y - r * vr * dv.Y),
                muinv * (vdiffsq * d.Z - r * vr * dv.Z));
            double e = evec.Length;
            if (e >= 1.0) return false;

            double inc = Kepler.Acos2(h.Z, hmag, 1.0);

            // Node vector n = z x h = (-hy, hx, 0)
            double nx = -h.Y, ny = h.X;
            double nmag = Math.Sqrt(nx * nx + ny * ny);
            double Omega = Kepler.Acos2(nx, nmag, ny);

            bool planar = inc < MinInc || inc > Math.PI - MinInc;
            double omega, f;

            if (e < MinRelError)
            {
                // Circular orbit: pericenter undefined; measure f from the ascending node (or x axis when planar).
                omega = 0.0;
                if (planar)
                {
                    double theta = Kepler.Acos2(d.X, r, d.Y);
                    f = inc < Math.PI / 2.0 ? theta - Omega : Omega - theta;
                }
                else
                {
                    f = Kepler.Acos2(nx * d.X + ny * d.Y, nmag * r, d.Z);
                }
            }
            else
            {
                if (planar)
                {
                    double pomega = Kepler.Acos2(evec.X, e, evec.Y);
                    omega = inc < Math.PI / 2.0 ? pomega - Omega : Omega - pomega;
                }
                else
                {
                    omega = Kepler.Acos2(nx * evec.X + ny * evec.Y, nmag * e, evec.Z);
                }
                f = Kepler.Acos2(Vec3d.Dot(evec, d), e * r, vr);
            }

            o.SemiMajorAxis = a;
            o.Eccentricity = e;
            o.Inclination = inc;
            o.LongitudeOfAscendingNode = Constants.WrapTwoPi(Omega);
            o.ArgumentOfPericenter = Constants.WrapTwoPi(omega);
            o.TrueAnomaly = Constants.WrapTwoPi(f);
            return true;
        }
    }
}
