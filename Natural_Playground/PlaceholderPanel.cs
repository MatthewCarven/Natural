namespace Natural_Playground;

/// <summary>A tab that isn't built yet.</summary>
internal sealed class PlaceholderPanel : UserControl
{
    public PlaceholderPanel(string what)
    {
        var label = new Label
        {
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleCenter,
            Text = $"{what} panel: not built yet.",
        };
        Controls.Add(label);
    }
}