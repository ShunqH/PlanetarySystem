namespace PlanetSystem.Physics
{
    /// <summary>
    /// Category of a body in the simulation.
    /// Massive bodies attract every other body; test particles feel gravity but exert none.
    /// </summary>
    public enum BodyKind
    {
        Massive = 0,
        TestParticle = 1,
    }

    /// <summary>
    /// Physical state of a single body. Positions in AU, velocities in AU/yr, mass in Msun, radius in AU.
    /// Pure data; the integrator and the view layer both read/write this.
    /// </summary>
    public sealed class Body
    {
        public int Id;
        public string Name;
        public BodyKind Kind;
        public double Mass;
        public double Radius;
        public Vec3d Position;
        public Vec3d Velocity;

        public bool IsMassive => Kind == BodyKind.Massive;

        public Body(int id, string name, BodyKind kind, double mass, double radius)
        {
            Id = id;
            Name = name;
            Kind = kind;
            Mass = kind == BodyKind.Massive ? mass : 0.0;
            Radius = radius;
        }
    }
}
