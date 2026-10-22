using CCM.Application;
using Raylib_cs;
namespace CCM.Scenes;

public sealed class GameScene(SessionInput input, Action returnToMenu) : Scene
{
    public override void Update(float seconds)
    {
        if (input.ReturnPressed()) returnToMenu();
    }
    public override void Render() => Raylib.ClearBackground(Color.Black);
}
