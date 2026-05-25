using System.Runtime.InteropServices;
using WinRT;
using Windows.Graphics.Capture;

namespace MiniPreview.Capture;

[ComImport]
[Guid("3628E81B-3CAC-4C60-B7F4-23CE0E0C3356")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IGraphicsCaptureItemInterop
{
    IntPtr CreateForWindow([In] IntPtr window, [In] ref Guid iid);
    IntPtr CreateForMonitor([In] IntPtr monitor, [In] ref Guid iid);
}

internal static class CaptureItemFactory
{
    // IID of Windows.Graphics.Capture.GraphicsCaptureItem (IInspectable-derived WinRT interface)
    private static readonly Guid IID_IGraphicsCaptureItem = new("79C3F95B-31F7-4EC2-A464-632EF5D30760");

    // IID of IGraphicsCaptureItemInterop COM interface
    private static readonly Guid IID_IGraphicsCaptureItemInterop = new("3628E81B-3CAC-4C60-B7F4-23CE0E0C3356");

    public static GraphicsCaptureItem CreateForWindow(IntPtr hwnd)
    {
        // ActivationFactory.Get(string, Guid) returns IObjectReference on .NET 8 / CsWinRT 2.x
        var factoryRef = WinRT.ActivationFactory.Get(
            "Windows.Graphics.Capture.GraphicsCaptureItem",
            IID_IGraphicsCaptureItemInterop);

        var factory = factoryRef.AsInterface<IGraphicsCaptureItemInterop>();
        var iid = IID_IGraphicsCaptureItem;
        var rawPtr = factory.CreateForWindow(hwnd, ref iid);
        return MarshalInterface<GraphicsCaptureItem>.FromAbi(rawPtr);
    }

    public static GraphicsCaptureItem CreateForMonitor(IntPtr hmonitor)
    {
        var factoryRef = WinRT.ActivationFactory.Get(
            "Windows.Graphics.Capture.GraphicsCaptureItem",
            IID_IGraphicsCaptureItemInterop);

        var factory = factoryRef.AsInterface<IGraphicsCaptureItemInterop>();
        var iid = IID_IGraphicsCaptureItem;
        var rawPtr = factory.CreateForMonitor(hmonitor, ref iid);
        return MarshalInterface<GraphicsCaptureItem>.FromAbi(rawPtr);
    }
}
