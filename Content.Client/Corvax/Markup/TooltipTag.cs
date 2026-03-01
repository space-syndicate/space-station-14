using System.Diagnostics.CodeAnalysis;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Client.UserInterface.RichText;
using Robust.Shared.Utility;

namespace Content.Client.Corvax.Markup;

public sealed class TooltipTag : IMarkupTagHandler
{
    private const float TooltipMaxWidth = 500f;

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
            FontColorOverride = Color.LightYellow,
            TooltipSupplier = _ =>
            {
                var richLabel = new RichTextLabel { MaxWidth = TooltipMaxWidth };
                richLabel.SetMessage(FormattedMessage.FromMarkupOrThrow(tooltipText));

                var tooltip = new Tooltip();
                tooltip.GetChild(0).Children.Clear();
                tooltip.GetChild(0).Children.Add(richLabel);

                return tooltip;
            }
        };

        control = label;
        return true;
    }
}
