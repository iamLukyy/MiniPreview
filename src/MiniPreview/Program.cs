namespace MiniPreview;

internal static class Program
{
    [System.STAThread]
    public static int Main(string[] args)
    {
        if (args.Length > 0 && args[0] == "--selftest")
        {
            return SelfTest.SelfTestRunner.Run();
        }

        var app = new App();
        // App.xaml has no resources, so InitializeComponent is not generated; just Run directly.
        return app.Run();
    }
}
