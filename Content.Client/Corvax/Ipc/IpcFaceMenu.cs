using System;
using System.Numerics;
using Content.Shared.Corvax.Ipc;
using Content.Shared.Humanoid.Markings;
using Content.Client.UserInterface.Controls;
using Robust.Client.GameObjects;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;
using Robust.Shared.Localization;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Client.Corvax.Ipc;

public sealed class IpcFaceMenu : FancyWindow
{
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly IEntityManager _entMan = default!;
    private readonly SpriteSystem _sprite;

    private readonly ItemList _list;
    public event Action<string>? FaceSelected;
    private bool _suppressSelection;

    public IpcFaceMenu()
    {
        IoCManager.InjectDependencies(this);
        _sprite = _entMan.System<SpriteSystem>();

        Title = Loc.GetString("ipc-face-menu-title");

        MinSize = new Vector2(300, 400);

        _list = new ItemList
        {
            VerticalExpand = true,
            HorizontalExpand = true
        };

        _list.OnItemSelected += args =>
        {
            if (_suppressSelection)
                return;

            if (_list[args.ItemIndex].Metadata is string id)
                FaceSelected?.Invoke(id);
        };

        ContentsContainer.AddChild(_list);
    }

    public void Populate(string profileId, string selected)
    {
        _suppressSelection = true;
        _list.Clear();
        var profile = _prototype.Index<IpcFaceProfilePrototype>(profileId);
        foreach (var face in profile.Faces)
        {
            if (!_prototype.TryIndex(face, out MarkingPrototype? marking) || marking.Sprites.Count == 0)
                continue;

            if (marking.Sprites[0] is not SpriteSpecifier.Rsi rsi)
                continue;

            var texture = _sprite.Frame0(rsi);
            var item = _list.AddItem(Loc.GetString($"marking-{marking.ID}"), texture);
            item.Metadata = marking.ID;

            if (marking.ID == selected)
                item.Selected = true;
        }
        _suppressSelection = false;
    }
}
