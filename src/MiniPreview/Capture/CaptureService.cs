using System.Runtime.InteropServices;
using Vortice.Direct3D11;
using Windows.Graphics.Capture;
using Windows.Graphics.DirectX;
using Windows.Graphics.DirectX.Direct3D11;
using WinRT;

namespace MiniPreview.Capture;

/// <summary>One captured frame: raw BGRA pixel data + dimensions. Consumer converts to bitmap.</summary>
public sealed record CapturedFrame(byte[] Bgra, int Width, int Height, int SourceStride);

public sealed class CaptureService : IDisposable
{
    private D3D11Device? _d3d;
    private StagingTexturePool? _pool;
    private GraphicsCaptureItem? _item;
    private Direct3D11CaptureFramePool? _framePool;
    private GraphicsCaptureSession? _session;
    private IDirect3DDevice? _winrtDevice;
    private int _fps = 5;
    private long _lastFrameTicks;
    private bool _paused;
    private readonly object _gate = new();

    /// <summary>Fired from worker thread. Consumer must marshal to UI thread.</summary>
    public event Action<CapturedFrame>? FrameReady;
    public event Action? TargetClosed;

    public bool IsRunning => _session != null;
    public bool IsPaused => _paused;
    public int Fps => _fps;

    public void Start(IntPtr hwnd, int fps)
    {
        Stop();
        lock (_gate)
        {
            _fps = Math.Clamp(fps, 1, 60);
            _d3d = new D3D11Device();
            _pool = new StagingTexturePool(_d3d.Device);
            _winrtDevice = CreateWinRTDeviceFromD3D11(_d3d.Device);
            _item = CaptureItemFactory.CreateForWindow(hwnd);
            _item.Closed += OnItemClosed;

            var size = _item.Size;
            _framePool = Direct3D11CaptureFramePool.CreateFreeThreaded(
                _winrtDevice,
                DirectXPixelFormat.B8G8R8A8UIntNormalized,
                numberOfBuffers: 2,
                size);
            _framePool.FrameArrived += OnFrameArrived;

            _session = _framePool.CreateCaptureSession(_item);
            _session.IsCursorCaptureEnabled = false;
            TrySuppressBorder(_session);
            _session.StartCapture();
            _lastFrameTicks = 0;
            _paused = false;
        }
    }

    public void Pause()
    {
        lock (_gate)
        {
            _paused = true;
            _session?.Dispose(); _session = null;
            _framePool?.Dispose(); _framePool = null;
        }
    }

    public void Resume()
    {
        lock (_gate)
        {
            if (!_paused || _item == null || _winrtDevice == null) return;
            _paused = false;
            var size = _item.Size;
            _framePool = Direct3D11CaptureFramePool.CreateFreeThreaded(
                _winrtDevice,
                DirectXPixelFormat.B8G8R8A8UIntNormalized,
                2,
                size);
            _framePool.FrameArrived += OnFrameArrived;
            _session = _framePool.CreateCaptureSession(_item);
            _session.IsCursorCaptureEnabled = false;
            TrySuppressBorder(_session);
            _session.StartCapture();
            _lastFrameTicks = 0;
        }
    }

    public void SetFps(int fps)
    {
        lock (_gate) { _fps = Math.Clamp(fps, 1, 60); }
    }

    public void Stop()
    {
        lock (_gate)
        {
            _session?.Dispose(); _session = null;
            _framePool?.Dispose(); _framePool = null;
            if (_item != null) _item.Closed -= OnItemClosed;
            _item = null;
            _pool?.Dispose(); _pool = null;
            _d3d?.Dispose(); _d3d = null;
            (_winrtDevice as IDisposable)?.Dispose();
            _winrtDevice = null;
            _paused = false;
        }
    }

    private void OnItemClosed(GraphicsCaptureItem sender, object args)
    {
        Stop();
        TargetClosed?.Invoke();
    }

    private void OnFrameArrived(Direct3D11CaptureFramePool sender, object args)
    {
        // Pacing gate: drop if too soon since last processed frame
        var now = Environment.TickCount64;
        var minInterval = 1000L / _fps;
        if (_lastFrameTicks != 0 && now - _lastFrameTicks < minInterval)
        {
            using var skip = sender.TryGetNextFrame();
            return;
        }
        _lastFrameTicks = now;

        using var frame = sender.TryGetNextFrame();
        if (frame == null) return;
        ProcessFrame(frame);
    }

    private void ProcessFrame(Direct3D11CaptureFrame frame)
    {
        // Snapshot fields into locals to avoid TOCTOU race with Stop() on another thread.
        // If Stop() runs concurrently, the locals may be non-null while the underlying
        // objects are being disposed; D3D11 operations on a disposed device return E_FAIL
        // gracefully, which is caught by the WGC frame-pool's exception handler.
        var d3d = _d3d;
        var pool = _pool;
        if (d3d == null || pool == null) return;

        var sourceTexture = D3D11InteropHelpers.GetTexture(frame.Surface);
        var size = frame.ContentSize;
        if (size.Width <= 0 || size.Height <= 0) return;

        var staging = pool.Get(size.Width, size.Height);
        d3d.Context.CopyResource(staging, sourceTexture);

        // Map(resource, subresource, MapMode, MapFlags, out MappedSubresource)
        d3d.Context.Map(staging, 0, MapMode.Read, MapFlags.None, out var mapped);
        try
        {
            int srcStride = mapped.RowPitch;
            var bytes = new byte[srcStride * size.Height];
            Marshal.Copy(mapped.DataPointer, bytes, 0, bytes.Length);
            FrameReady?.Invoke(new CapturedFrame(bytes, size.Width, size.Height, srcStride));
        }
        finally
        {
            d3d.Context.Unmap(staging, 0);
        }
    }

    private static IDirect3DDevice CreateWinRTDeviceFromD3D11(ID3D11Device d3dDevice)
    {
        // QI the D3D11 device for IDXGIDevice, then wrap it as a WinRT IDirect3DDevice
        using var dxgi = d3dDevice.QueryInterface<Vortice.DXGI.IDXGIDevice>();
        Direct3D11InteropFunctions.CreateDirect3D11DeviceFromDXGIDevice(dxgi.NativePointer, out var ptr);
        return MarshalInterface<IDirect3DDevice>.FromAbi(ptr);
    }

    public void Dispose() => Stop();

    /// <summary>
    /// Suppress the yellow capture border Windows draws around the captured window.
    /// IsBorderRequired was added in Win11 22H2 (WGC contract v11). On older builds
    /// the property is absent and we silently leave the border on.
    /// </summary>
    private static void TrySuppressBorder(GraphicsCaptureSession session)
    {
        try
        {
            // Available since Windows.Graphics.Capture API contract 11
            // (Windows 11 22H2 / build 22621+). Use global:: to avoid clash with
            // our own MiniPreview.Windows namespace.
            if (global::Windows.Foundation.Metadata.ApiInformation.IsPropertyPresent(
                    "Windows.Graphics.Capture.GraphicsCaptureSession", "IsBorderRequired"))
            {
                session.IsBorderRequired = false;
            }
        }
        catch
        {
            // Property exists but app doesn't have permission — leave the border on.
        }
    }
}

internal static class Direct3D11InteropFunctions
{
    [DllImport("d3d11.dll")]
    public static extern int CreateDirect3D11DeviceFromDXGIDevice(IntPtr dxgiDevice, out IntPtr graphicsDevice);
}

internal static class D3D11InteropHelpers
{
    private static readonly Guid IID_ID3D11Texture2D = new("6f15aaf2-d208-4e89-9ab4-489535d34f9c");

    public static ID3D11Texture2D GetTexture(IDirect3DSurface surface)
    {
        // Use IDirect3DDxgiInterfaceAccess to get the underlying D3D11 texture
        var access = surface.As<IDirect3DDxgiInterfaceAccess>();
        var iid = IID_ID3D11Texture2D;
        var ptr = access.GetInterface(ref iid);
        return new ID3D11Texture2D(ptr);
    }
}

[ComImport]
[Guid("A9B3D012-3DF2-4EE3-B8D1-8695F457D3C1")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IDirect3DDxgiInterfaceAccess
{
    IntPtr GetInterface([In] ref Guid iid);
}
