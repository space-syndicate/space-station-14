using System;
using Content.Shared.Corvax.Ipc;
using Content.Shared.Humanoid.Markings;
using Robust.Client.UserInterface.Controls;
using Content.Client.UserInterface.Controls;
using Robust.Client.GameObjects;
using Robust.Shared.Prototypes;
using Robust.Shared.IoC;
using Robust.Shared.Localization;
using Robust.Client.UserInterface;
using Robust.Shared.Utility;

namespace Content.Client.Corvax.Ipc;

public sealed class IpcFaceMenu : FancyWindow
{
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly SpriteSystem _sprite = default!;

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
        foreach (var face in profile.Faces)
        {
            if (!_prototype.TryIndex(face, out MarkingPrototype? marking) || marking.Sprites.Count == 0)
                continue;

            if (marking.Sprites[0] is not SpriteSpecifier.Rsi rsi)
                continue;

            var texture = _sprite.Frame0(rsi);
            var button = new TextureButton { TextureNormal = texture };
            var box = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical };
            box.AddChild(button);
            box.AddChild(new Label { Text = Loc.GetString($"marking-{marking.ID}") });
            var sel = marking.ID;
            button.OnPressed += _ => FaceSelected?.Invoke(sel);
            _grid.AddChild(box);
        }
    }
}
