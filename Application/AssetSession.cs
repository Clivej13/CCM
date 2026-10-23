using RaylibGameFramework.Assets;
namespace CCM.Application;

/// <summary>One graphics-thread owner; disposed while the window still exists.</summary>
public sealed class AssetSession : IDisposable
{
    public AssetManager? Manager { get; private set; }
    public void Begin(string catalogueFile,string[]? initialKeys=null)
    {
        if (Manager is not null) throw new InvalidOperationException("Asset session has already begun.");
        var catalogue = AssetConfigLoader.Load(Settings.ConfigPath(catalogueFile));
        if (catalogue.Assets.Count == 0) throw new InvalidDataException("The required asset catalogue is empty.");
        Manager = new AssetManager(catalogue);
        Manager.SetRequiredAssets(initialKeys ?? catalogue.Assets.Select(a => a.Key).ToArray());
    }
    public void Dispose() { Manager?.UnloadAll(); Manager = null; }
}
