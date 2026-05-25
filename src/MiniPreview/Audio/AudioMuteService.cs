using NAudio.CoreAudioApi;

namespace MiniPreview.Audio;

public sealed class AudioMuteService : IDisposable
{
    private readonly MMDeviceEnumerator _enumerator;
    // Cached once in ctor to avoid leaking one COM IMMDevice wrapper per FindSessions call.
    // Trade-off: if the default audio device changes at runtime we won't pick it up
    // until the next process restart — acceptable for v1.
    private readonly MMDevice _device;
    private bool _disposed;

    public AudioMuteService()
    {
        _enumerator = new MMDeviceEnumerator();
        _device = _enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
    }

    /// <summary>Toggle mute state for all sessions belonging to the given PID. Returns new state (true = muted).</summary>
    public bool ToggleMute(uint pid)
    {
        var sessions = FindSessions(pid);
        if (sessions.Count == 0) return false;

        var newState = !sessions[0].SimpleAudioVolume.Mute;
        foreach (var s in sessions)
            s.SimpleAudioVolume.Mute = newState;
        return newState;
    }

    public bool IsMuted(uint pid)
    {
        var sessions = FindSessions(pid);
        return sessions.Count > 0 && sessions[0].SimpleAudioVolume.Mute;
    }

    public bool HasAudioSession(uint pid) => FindSessions(pid).Count > 0;

    private List<AudioSessionControl> FindSessions(uint pid)
    {
        // Use the cached device; do NOT dispose the AudioSessionControl items returned
        // here — NAudio's SessionCollection owns them and double-disposing would be a bug.
        var sessions = _device.AudioSessionManager.Sessions;
        var matches = new List<AudioSessionControl>();
        for (int i = 0; i < sessions.Count; i++)
        {
            var s = sessions[i];
            if (s.GetProcessID == pid)
                matches.Add(s);
        }
        return matches;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _device.Dispose();
        _enumerator.Dispose();
        _disposed = true;
    }
}
