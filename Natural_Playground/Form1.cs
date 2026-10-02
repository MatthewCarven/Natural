namespace Natural_Playground;

/// <summary>
/// The shell: one window, a tab per type. Adding a tab is adding a UserControl,
/// so the panels stay independent of the form.
/// </summary>
public partial class Form1 : Form
{
    public Form1()
    {
        InitializeComponent();

        Text = "Natural Playground";
        Font = new Font("Segoe UI", 9F);

        var tabs = new TabControl { Dock = DockStyle.Fill };
        tabs.TabPages.Add(Page("ApFloat", new FloatPanel()));
        tabs.TabPages.Add(Page("ApInt", new PlaceholderPanel("ApInt")));
        Controls.Add(tabs);
    }

    private static TabPage Page(string title, Control content)
    {
        var page = new TabPage(title) { Padding = new Padding(8) };
        content.Dock = DockStyle.Fill;
        page.Controls.Add(content);
        return page;
    }
}