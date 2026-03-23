namespace MalumMenuOverlay;

/// <summary>
/// Entry point for the MalumMenu external overlay.
/// </summary>
static class Program
{
    [STAThread]
    static void Main()
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.Run(new OverlayForm());
    }
}
