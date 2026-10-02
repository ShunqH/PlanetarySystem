using PlanetSystem.Physics;
using UnityEngine;

namespace PlanetSystem.Core
{
    /// <summary>
    /// Everything the UI needs to describe a body before it exists (add form) or while editing it.
    /// Mass in Msun, radius in AU, angles in radians. ReferenceId selects the primary the orbital
    /// elements are measured against: a body id, or <see cref="CenterOfMassReference"/> for the
    /// barycenter of all massive bodies (excluding the body itself).
    /// </summary>
    public sealed class BodyDefinition
    {
        public const int CenterOfMassReference = -1;

        public string Name = "Body";
        public BodyKind Kind = BodyKind.Massive;
        public double Mass = 1.0;
        public double Radius = Constants.SolarRadiusInAu;
        public Color Color = Color.white;
        public int ReferenceId = CenterOfMassReference;
        public OrbitalElements Elements = OrbitalElements.Circular(1.0);

        /// <summary>
        /// Massive bodies always orbit the barycenter of the other massive bodies; only test particles may
        /// use a specific body as primary. Applies that rule in place.
        /// </summary>
        public void NormalizeReference()
        {
            if (Kind == BodyKind.Massive) ReferenceId = CenterOfMassReference;
        }

        public BodyDefinition Clone()
        {
            return new BodyDefinition
            {
                Name = Name,
                Kind = Kind,
                Mass = Mass,
                Radius = Radius,
                Color = Color,
                ReferenceId = ReferenceId,
                Elements = Elements,
            };
        }
    }

    /// <summary>
    /// A body in the running system plus presentation data that the physics layer does not care about.
    /// </summary>
    public sealed class BodyRecord
    {
        public readonly Body Body;
        public Color Color;
        /// <summary>Primary used when displaying or editing this body's orbital elements (see BodyDefinition).</summary>
        public int ReferenceId = BodyDefinition.CenterOfMassReference;

        public int Id => Body.Id;
        public string Name => Body.Name;

        public BodyRecord(Body body, Color color, int referenceId)
        {
            Body = body;
            Color = color;
            ReferenceId = referenceId;
        }
    }
}
