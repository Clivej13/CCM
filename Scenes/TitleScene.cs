using CCM.Application;
using Raylib_cs;
namespace CCM.Scenes;

public sealed class TitleScene(AssetSession presentation, DeviceSelection selection,
    Func<TitleInput> readInput, Action completed) : Scene
{
    private bool rendered;
    public override void Enter() => rendered = false;
    public override void Update(float seconds)
    {
        if (rendered && selection.Update(readInput())) completed();
    }
    public override void Render()
    {
        Raylib.ClearBackground(new Color(12, 16, 24, 255));
        int centre = Raylib.GetScreenHeight()/2;
        DrawCentred("CCM", centre-100, 80);
        var mark = presentation.Manager!.GetModel("title-mark");
        var camera = new Camera3D
        {
            Position = new(0, 0, 5), Target = System.Numerics.Vector3.Zero,
            Up = System.Numerics.Vector3.UnitY, FovY = 8, Projection = CameraProjection.Orthographic
        };
        Raylib.BeginMode3D(camera);
        Raylib.DrawModel(mark, System.Numerics.Vector3.Zero, 1, Color.SkyBlue);
        Raylib.EndMode3D();
        DrawCentred("Press any key or controller button", centre+52, 24);
        DrawCentred("Your input selects the controls for this session", centre+90, 18);
        rendered = true;
    }
    private static void DrawCentred(string text, int y, int size) =>
        Raylib.DrawText(text, (Raylib.GetScreenWidth()-Raylib.MeasureText(text,size))/2, y, size, Color.RayWhite);
}
