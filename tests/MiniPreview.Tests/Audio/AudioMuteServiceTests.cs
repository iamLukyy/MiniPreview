using MiniPreview.Audio;
using NAudio.Wave;
using Xunit;

namespace MiniPreview.Tests.Audio;

[Trait("Category", "RequiresDesktop")]
public class AudioMuteServiceTests
{
    [Fact]
    public void ToggleMute_FlipsState_ForCurrentProcess()
    {
        // Play short silent stream to register audio session
        using var waveOut = new WaveOutEvent();
        var generator = new SilenceProvider(new WaveFormat(44100, 1));
        waveOut.Init(generator);
        waveOut.Play();
        // wait for session to register
        Thread.Sleep(500);

        var pid = (uint)Environment.ProcessId;
        using var mute = new AudioMuteService();

        if (!mute.HasAudioSession(pid))
        {
            // session not detected; flag explicitly via test failure
            throw new SkipException("Audio session not detected for test process (perhaps headless environment)");
        }

        var initial = mute.IsMuted(pid);
        var toggled = mute.ToggleMute(pid);
        Assert.NotEqual(initial, toggled);
        var toggledBack = mute.ToggleMute(pid);
        Assert.Equal(initial, toggledBack);

        waveOut.Stop();
    }
}

internal sealed class SilenceProvider : IWaveProvider
{
    public SilenceProvider(WaveFormat fmt) { WaveFormat = fmt; }
    public WaveFormat WaveFormat { get; }
    public int Read(byte[] buffer, int offset, int count)
    {
        Array.Clear(buffer, offset, count);
        return count;
    }
}

internal sealed class SkipException : Exception { public SkipException(string m) : base(m) { } }
