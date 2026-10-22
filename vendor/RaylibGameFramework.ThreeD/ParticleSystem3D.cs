using System.Numerics;
using Raylib_cs;

namespace RaylibGameFramework.ThreeD;

public sealed class ParticleSystem3D
{
    private readonly List<Particle3D> _particles = [];
    private readonly Random _random;
    public const int DefaultMaximumParticles = 720;

    public ParticleSystem3D(int maximumParticles = DefaultMaximumParticles, int? randomSeed = null)
    {
        if (maximumParticles <= 0)
            throw new ArgumentOutOfRangeException(nameof(maximumParticles));
        MaximumParticles = maximumParticles;
        _random = randomSeed.HasValue ? new Random(randomSeed.Value) : Random.Shared;
    }

    public int MaximumParticles { get; }
    public int ActiveCount => _particles.Count;
    public int PeakActiveCount { get; private set; }
    public IReadOnlyList<Particle3D> Particles => _particles;

    public void Clear() => _particles.Clear();

    public bool Emit(Particle3D particle)
    {
        if (_particles.Count >= MaximumParticles)
            return false;
        particle.Active = true;
        _particles.Add(particle);
        PeakActiveCount = Math.Max(PeakActiveCount, _particles.Count);
        return true;
    }

    public int Burst(Vector3 origin, int count, float speed = 1f, float lifetime = 0.5f,
        float size = 0.1f, Color? color = null)
    {
        if (count < 0)
            throw new ArgumentOutOfRangeException(nameof(count));
        int emitted = 0;
        for (int i = 0; i < count && _particles.Count < MaximumParticles; i++)
        {
            Vector3 direction = RandomUnitVector();
            emitted += Emit(new Particle3D
            {
                Position = origin,
                Velocity = direction * speed,
                Lifetime = lifetime,
                Size = size,
                Color = color ?? Color.White,
                Active = true
            }) ? 1 : 0;
        }
        return emitted;
    }

    public bool EmitLocal(Transform3D transform, Vector3 localPosition, Vector3 localVelocity,
        float lifetime, float size, Color color, Vector3? localAcceleration = null)
    {
        return Emit(new Particle3D
        {
            Position = transform.TransformPoint(localPosition),
            Velocity = transform.TransformDirection(localVelocity),
            Acceleration = transform.TransformDirection(localAcceleration ?? Vector3.Zero),
            Lifetime = lifetime,
            Size = size,
            Color = color,
            Active = true
        });
    }

    public void Update(float deltaTime)
    {
        if (!float.IsFinite(deltaTime) || deltaTime <= 0)
            return;

        for (int i = _particles.Count - 1; i >= 0; i--)
        {
            Particle3D particle = _particles[i];
            particle.Age += deltaTime;
            if (!particle.Active || particle.Age >= particle.Lifetime)
            {
                _particles.RemoveAt(i);
                continue;
            }

            particle.Velocity += particle.Acceleration * deltaTime;
            particle.Position += particle.Velocity * deltaTime;
            particle.Rotation += deltaTime * 8f;
            _particles[i] = particle;
        }
    }

    public void Draw()
    {
        if (_particles.Count == 0)
            return;

        Raylib.BeginBlendMode(BlendMode.Additive);
        foreach (Particle3D particle in _particles)
        {
            float fade = 1f - particle.NormalizedAge;
            Color color = new(particle.Color.R, particle.Color.G, particle.Color.B,
                (byte)(particle.Color.A * fade));
            Raylib.DrawSphere(particle.Position, particle.Size * (0.7f + fade * 0.5f), color);
        }
        Raylib.EndBlendMode();
    }

    private Vector3 RandomUnitVector()
    {
        Vector3 vector;
        do
        {
            vector = new Vector3(RandomRange(-1, 1), RandomRange(-1, 1), RandomRange(-1, 1));
        } while (vector.LengthSquared() < 0.01f);
        return Vector3.Normalize(vector);
    }

    private float RandomRange(float minimum, float maximum) =>
        minimum + (float)_random.NextDouble() * (maximum - minimum);
}
