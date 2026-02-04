using System.Diagnostics.CodeAnalysis;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.RichText;
using Robust.Shared.Utility;

namespace Content.Client.Corvax.Markup;

public sealed class TooltipTag : IMarkupTagHandler
{
    [Dependency] private readonly ILocalizationManager _loc = default!;

    public string Name => "tooltip";

    public bool TryCreateControl(MarkupNode node, [NotNullWhen(true)] out Control? control)
    {
        if (!node.Value.TryGetString(out var tooltipKey) || string.IsNullOrWhiteSpace(tooltipKey))
        {
            control = null;
            return false;
        }

        if (!_loc.TryGetString(tooltipKey, out var tooltipText))
            tooltipText = tooltipKey;

        var visibleText = tooltipText;
        if (node.Attributes.TryGetValue("text", out var textParam) && textParam.TryGetString(out var explicitText)
            && !string.IsNullOrEmpty(explicitText))
        {
            visibleText = explicitText;
        }

        var label = new Label
        {
            Text = visibleText,
            MouseFilter = Control.MouseFilterMode.Stop,
            ToolTip = tooltipText,
            FontColorOverride = Color.LightYellow
        };

        control = label;
        return true;
    }
}
