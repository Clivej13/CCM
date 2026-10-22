using Raylib_cs;
using RaylibGameFramework.Assets;

namespace RaylibGameFramework.ThreeD;

/// <summary>Small helpers for changing the albedo colour of one independent model instance.</summary>
public static class MaterialOverrides
{
    public static unsafe void SetAlbedoColor(ModelInstance instance, int runtimeMaterialIndex, Color color)
    {
        ArgumentNullException.ThrowIfNull(instance);
        Model model = instance.Model;
        if (runtimeMaterialIndex < 0 || runtimeMaterialIndex >= model.MaterialCount)
            throw new ArgumentOutOfRangeException(nameof(runtimeMaterialIndex));

        model.Materials[runtimeMaterialIndex].Maps[(int)MaterialMapIndex.Albedo].Color = color;
    }
}
