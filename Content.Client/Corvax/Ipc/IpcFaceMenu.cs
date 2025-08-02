using System;
using Content.Shared.Corvax.Ipc;
using Robust.Client.UserInterface.Controls;
using Content.Client.UserInterface.Controls;
using Robust.Client.ResourceManagement;
using Robust.Shared.Utility;
using Robust.Shared.Prototypes;
using Robust.Shared.IoC;
using Robust.Shared.Localization;
using Robust.Client.UserInterface;
using Robust.Shared.Serialization.TypeSerializers.Implementations;

namespace Content.Client.Corvax.Ipc;

public sealed class IpcFaceMenu : FancyWindow
{
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly IResourceCache _res = default!;

    private readonly GridContainer _grid;
    public event Action<string>? FaceSelected;

    public IpcFaceMenu()
    {
        IoCManager.InjectDependencies(this);
        Title = Loc.GetString("ipc-face-menu-title");
        var scroll = new ScrollContainer();
        _grid = new GridContainer { Columns = 6 };
        scroll.AddChild(_grid);
        ContentsContainer.AddChild(scroll);
    }

    public void Populate(string profileId, string selected)
    {
        _grid.RemoveAllChildren();
        var profile = _prototype.Index<IpcFaceProfilePrototype>(profileId);
        var rsi = _res.GetResource<RSIResource>(SpriteSpecifierSerializer.TextureRoot / profile.RsiPath).RSI;
        foreach (var state in rsi)
        {
            var name = state.StateId.ToString() ?? string.Empty;
            var texture = _res.GetResource<TextureResource>(SpriteSpecifierSerializer.TextureRoot / profile.RsiPath / $"{name}.png").Texture;
            var button = new TextureButton { TextureNormal = texture };
            var box = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical };
            box.AddChild(button);
            box.AddChild(new Label { Text = name });
            var sel = name;
            button.OnPressed += _ => FaceSelected?.Invoke(sel);
            _grid.AddChild(box);
        }
    }
}
