using System.Numerics;
using CCM.Game;

namespace CCM.Scenes;

internal readonly record struct BounceLight(Vector3 Position,Vector3 Color);

internal sealed class FireplaceLightRig(
    FireplaceInteractable fireplace,IReadOnlyList<BounceLight> secondary)
{
    public FireplaceInteractable Fireplace => fireplace;
    public Vector3 MainPosition => fireplace.FireOrigin+new Vector3(0,0.35f,0);
    public IReadOnlyList<BounceLight> Secondary => secondary;
}
