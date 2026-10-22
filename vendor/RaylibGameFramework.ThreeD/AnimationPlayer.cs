using Raylib_cs;
using RaylibGameFramework.Assets;

namespace RaylibGameFramework.ThreeD;

public sealed class AnimationPlayer
{
    public const float DefaultFramesPerSecond = 60f;
    private int _appliedFrame = -1;
    private readonly int _frameCount;

    public ModelInstance? Instance { get; }
    public ModelAnimation Animation { get; }
    public float FramesPerSecond { get; }
    public bool Loop { get; set; }
    public float PlaybackSpeed { get; set; } = 1f;
    public float CurrentTime { get; private set; }
    public float CurrentFrame { get; private set; }
    public int FrameCount => _frameCount;
    public bool IsComplete => !Loop && CurrentFrame >= Math.Max(0, FrameCount - 1);

    public AnimationPlayer(ModelInstance? instance, ModelAnimation animation,
        bool loop = true, float framesPerSecond = DefaultFramesPerSecond, float phase = 0f)
    {
        if (framesPerSecond <= 0 || !float.IsFinite(framesPerSecond))
            throw new ArgumentOutOfRangeException(nameof(framesPerSecond));

        Instance = instance;
        Animation = animation;
        _frameCount = animation.KeyFrameCount;
        FramesPerSecond = framesPerSecond;
        Loop = loop;
        SeekPhase(phase);
    }

    public AnimationPlayer(int frameCount, bool loop = true,
        float framesPerSecond = DefaultFramesPerSecond, float phase = 0f)
        : this(null, default, loop, framesPerSecond, phase)
    {
        if (frameCount <= 0)
            throw new ArgumentOutOfRangeException(nameof(frameCount));
        _frameCount = frameCount;
        SeekPhase(phase);
    }

    public void Update(float deltaTime)
    {
        if (!float.IsFinite(deltaTime) || deltaTime <= 0 || FrameCount <= 0 || PlaybackSpeed == 0)
            return;

        float duration = FrameCount / FramesPerSecond;
        CurrentTime += deltaTime * PlaybackSpeed;

        if (Loop)
        {
            CurrentTime = Modulo(CurrentTime, duration);
        }
        else
        {
            CurrentTime = Math.Clamp(CurrentTime, 0, Math.Max(0, duration - 1f / FramesPerSecond));
        }

        ApplyCurrentFrame();
    }

    public void SeekTime(float time)
    {
        if (!float.IsFinite(time))
            throw new ArgumentOutOfRangeException(nameof(time));

        float duration = FrameCount <= 0 ? 0 : FrameCount / FramesPerSecond;
        CurrentTime = Loop && duration > 0
            ? Modulo(time, duration)
            : Math.Clamp(time, 0, Math.Max(0, duration - 1f / FramesPerSecond));
        ApplyCurrentFrame();
    }

    public void SeekPhase(float phase)
    {
        if (!float.IsFinite(phase))
            throw new ArgumentOutOfRangeException(nameof(phase));
        SeekTime(Math.Clamp(phase, 0, 1) * (FrameCount / FramesPerSecond));
    }

    private void ApplyCurrentFrame()
    {
        CurrentFrame = FrameCount <= 0 ? 0 : Math.Clamp(CurrentTime * FramesPerSecond, 0, FrameCount - 1);
        int frame = (int)CurrentFrame;
        if (frame == _appliedFrame || Instance is null || FrameCount <= 0)
            return;

        Raylib.UpdateModelAnimation(Instance.Model, Animation, frame);
        _appliedFrame = frame;
    }

    private static float Modulo(float value, float modulus) =>
        value - MathF.Floor(value / modulus) * modulus;
}
