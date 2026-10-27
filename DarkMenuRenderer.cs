using System.Drawing;
using System.Windows.Forms;

namespace HerdrTray;

/// <summary>
/// Standard Windows menu rendering with the app's dark palette: the default item
/// metrics, highlight and image gutter stay, only the colours change.
/// </summary>
internal sealed class DarkMenuRenderer : ToolStripProfessionalRenderer
{
    private static readonly Color Panel = Color.FromArgb(0x20, 0x20, 0x24);
    private static readonly Color Border = Color.FromArgb(0x3A, 0x3A, 0x40);
    private static readonly Color Text = Color.FromArgb(0xE8, 0xE8, 0xE8);
    private static readonly Color Hover = Color.FromArgb(0x2E, 0x2E, 0x34);

    public DarkMenuRenderer()
        : base(new Table())
    {
        RoundedEdges = false;
    }

    public static Color PanelColor => Panel;

    public static Color TextColor => Text;

    private sealed class Table : ProfessionalColorTable
    {
        public override Color ToolStripDropDownBackground => Panel;

        public override Color MenuBorder => Border;

        public override Color MenuItemBorder => Hover;

        public override Color MenuItemSelected => Hover;

        public override Color MenuItemSelectedGradientBegin => Hover;

        public override Color MenuItemSelectedGradientEnd => Hover;

        public override Color MenuItemPressedGradientBegin => Hover;

        public override Color MenuItemPressedGradientEnd => Hover;

        public override Color ImageMarginGradientBegin => Panel;

        public override Color ImageMarginGradientMiddle => Panel;

        public override Color ImageMarginGradientEnd => Panel;

        public override Color ImageMarginRevealedGradientBegin => Panel;

        public override Color ImageMarginRevealedGradientMiddle => Panel;

        public override Color ImageMarginRevealedGradientEnd => Panel;

        public override Color SeparatorDark => Border;

        public override Color SeparatorLight => Border;
    }
}
