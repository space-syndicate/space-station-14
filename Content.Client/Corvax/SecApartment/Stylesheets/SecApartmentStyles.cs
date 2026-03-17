using Content.Client.Resources;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;

namespace Content.Client.Corvax.SecApartment.Stylesheets;

public sealed class SecApartmentStyles
{
    private readonly IResourceCache _resCache;

    public static Color TabActiveColor => Color.FromHex("#ff4444");
    public static Color TabInactiveColor => Color.FromHex("#ff8888");
    public static Color HeadingColor => Color.FromHex("#ff4444");
    public static Color SubHeadingColor => Color.FromHex("#ff8888");
    public static Color TextColor => Color.FromHex("#ff9999");
    public static Color SubTextColor => Color.FromHex("#ff8888");
    public static Color PlaceholderColor => Color.FromHex("#ff6666");

    public const string StyleClassButtonRed = "ButtonRed";
    public const string StyleClassConsoleLineEdit = "ConsoleLineEdit";
    public const string StyleClassConsoleHeading = "ConsoleHeading";
    public const string StyleClassOptionButton = "SecApartmentOptionButton";

    public SecApartmentStyles(IResourceCache resCache)
    {
        _resCache = resCache;
    }

    private StyleBoxFlat CreateStyleBox(Color backgroundColor, Color borderColor,
        Thickness borderThickness, Thickness? contentMargin = null)
    {
        var style = new StyleBoxFlat
        {
            BackgroundColor = backgroundColor,
            BorderColor = borderColor,
            BorderThickness = borderThickness
        };

        if (contentMargin.HasValue)
        {
            style.ContentMarginLeftOverride = contentMargin.Value.Left;
            style.ContentMarginRightOverride = contentMargin.Value.Right;
            style.ContentMarginTopOverride = contentMargin.Value.Top;
            style.ContentMarginBottomOverride = contentMargin.Value.Bottom;
        }

        return style;
    }

    public StyleBox GetTabActiveStyle() => CreateStyleBox(
        Color.FromHex("#440000"),
        TabActiveColor,
        new Thickness(2, 2, 2, 0),
        new Thickness(10, 5, 10, 5)
    );

    public StyleBox GetTabInactiveStyle() => CreateStyleBox(
        Color.FromHex("#220000"),
        TabInactiveColor,
        new Thickness(2, 2, 2, 0),
        new Thickness(10, 5, 10, 5)
    );

    public StyleBox GetPanelStyle() => CreateStyleBox(
        Color.FromHex("#110000"),
        TabActiveColor,
        new Thickness(2),
        new Thickness(5, 5, 5, 5)
    );

    public StyleBox GetButtonRedStyle() => CreateStyleBox(
        Color.FromHex("#660000"),
        Color.FromHex("#ff4444"),
        new Thickness(1),
        new Thickness(8, 4, 8, 4)
    );

    public StyleBox GetLineEditStyle() => CreateStyleBox(
        Color.FromHex("#110000"),
        Color.FromHex("#ff4444"),
        new Thickness(1),
        new Thickness(4, 2, 4, 2)
    );

    public Font GetBoldFont(int size = 12) => _resCache.GetFont(new[]
    {
        "/Fonts/NotoSans/NotoSans-Bold.ttf",
        "/Fonts/NotoSans/NotoSansSymbols-Regular.ttf",
        "/Fonts/NotoSans/NotoSansSymbols2-Regular.ttf"
    }, size);

    public Font GetRegularFont(int size = 12) => _resCache.GetFont(new[]
    {
        "/Fonts/NotoSans/NotoSans-Regular.ttf",
        "/Fonts/NotoSans/NotoSansSymbols-Regular.ttf",
        "/Fonts/NotoSans/NotoSansSymbols2-Regular.ttf"
    }, size);

    public static StyleRule CreateButtonRedRule(StyleBox buttonRedStyle, Font font, Color fontColor, Color disabledColor)
    {
        return new StyleRule(
            new SelectorElement(typeof(Button), new[] { StyleClassButtonRed }, null, null),
            new[]
            {
                new StyleProperty("stylebox", buttonRedStyle),
                new StyleProperty("font-color", fontColor),
                new StyleProperty("font", font),
                new StyleProperty("font-color-disabled", disabledColor)
            }
        );
    }

    public static StyleRule CreateLineEditRule(StyleBox lineEditStyle, Font font, Color textColor, Color placeholderColor)
    {
        return new StyleRule(
            new SelectorElement(typeof(LineEdit), new[] { StyleClassConsoleLineEdit }, null, null),
            new[]
            {
                new StyleProperty("stylebox", lineEditStyle),
                new StyleProperty("font-color", textColor),
                new StyleProperty("font", font),
                new StyleProperty("placeholder-color", placeholderColor),
                new StyleProperty("cursor-color", TabActiveColor),
                new StyleProperty("selection-color", TabActiveColor.WithAlpha(0.3f))
            }
        );
    }

    public StyleBox GetOptionButtonStyle() => CreateStyleBox(
        Color.FromHex("#330000"),
        TabActiveColor,
        new Thickness(1),
        new Thickness(6, 3, 6, 3)
    );

    public static StyleRule CreateOptionButtonRule(StyleBox optionStyle, Font font, Color fontColor, Color disabledColor)
    {
        return new StyleRule(
            new SelectorElement(typeof(OptionButton), new[] { StyleClassOptionButton }, null, null),
            new[]
            {
                new StyleProperty(ContainerButton.StylePropertyStyleBox, optionStyle),
                new StyleProperty("font", font),
                new StyleProperty("font-color", fontColor),
                new StyleProperty("font-color-disabled", disabledColor)
            }
        );
    }
    public static StyleRule CreateOptionButtonBackgroundRule()
    {
        return new StyleRule(
            new SelectorElement(typeof(PanelContainer), new[] { OptionButton.StyleClassOptionsBackground }, null, null),
            new[]
            {
                new StyleProperty(PanelContainer.StylePropertyPanel, new StyleBoxFlat
                {
                    BackgroundColor = Color.FromHex("#330000"),
                    BorderColor = TabActiveColor,
                    BorderThickness = new Thickness(1)
                })
            }
        );
    }
    public static StyleRule CreateTabContainerRule(StyleBox tabActiveStyle, StyleBox tabInactiveStyle,
        StyleBox panelStyle, Font font, Color activeColor, Color inactiveColor)
    {
        return new StyleRule(
            new SelectorElement(typeof(TabContainer), null, null, null),
            new[]
            {
                new StyleProperty("tab-stylebox", tabActiveStyle),
                new StyleProperty("tab-stylebox-inactive", tabInactiveStyle),
                new StyleProperty("panel-stylebox", panelStyle),
                new StyleProperty("tab-font-color", activeColor),
                new StyleProperty("tab-font-color-inactive", inactiveColor),
                new StyleProperty("font", font)
            }
        );
    }
}
