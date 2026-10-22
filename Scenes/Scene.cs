namespace CCM.Scenes;

/// <summary>Application owns transitions and calls Exit before entering the next scene.</summary>
public abstract class Scene
{
    public virtual void Enter() { }
    public abstract void Update(float seconds);
    public abstract void Render();
    public virtual void Exit() { }
}
