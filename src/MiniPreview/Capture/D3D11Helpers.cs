using Vortice.Direct3D;
using Vortice.Direct3D11;
using Vortice.DXGI;

namespace MiniPreview.Capture;

internal sealed class D3D11Device : IDisposable
{
    public ID3D11Device Device { get; }
    public ID3D11DeviceContext Context { get; }

    public D3D11Device()
    {
        var flags = DeviceCreationFlags.BgraSupport;
        var levels = new[] { FeatureLevel.Level_11_1, FeatureLevel.Level_11_0, FeatureLevel.Level_10_1, FeatureLevel.Level_10_0 };
        var result = Vortice.Direct3D11.D3D11.D3D11CreateDevice(
            null,
            DriverType.Hardware,
            flags,
            levels,
            out var device,
            out _,
            out var context);
        if (result.Failure) throw new InvalidOperationException("D3D11CreateDevice selhal: " + result.Description);
        Device = device!;
        Context = context!;
    }

    public void Dispose()
    {
        Context?.Dispose();
        Device?.Dispose();
    }
}

internal sealed class StagingTexturePool : IDisposable
{
    private readonly ID3D11Device _device;
    private ID3D11Texture2D? _staging;
    private int _width, _height;

    public StagingTexturePool(ID3D11Device device) { _device = device; }

    public ID3D11Texture2D Get(int width, int height)
    {
        if (_staging != null && _width == width && _height == height) return _staging;

        _staging?.Dispose();
        var desc = new Texture2DDescription
        {
            Width = width,
            Height = height,
            MipLevels = 1,
            ArraySize = 1,
            Format = Format.B8G8R8A8_UNorm,
            SampleDescription = new SampleDescription(1, 0),
            Usage = ResourceUsage.Staging,
            BindFlags = BindFlags.None,
            CPUAccessFlags = CpuAccessFlags.Read,
            MiscFlags = ResourceOptionFlags.None
        };
        _staging = _device.CreateTexture2D(desc);
        _width = width;
        _height = height;
        return _staging;
    }

    public void Dispose() { _staging?.Dispose(); _staging = null; }
}
