using NAudio.CoreAudioApi;

namespace MiniPreview.Audio;

public sealed class AudioMuteService : IDisposable
{
    private readonly MMDeviceEnumerator _enumerator;
    private bool _disposed;

    public AudioMuteService()
    {
        _enumerator = new MMDeviceEnumerator();
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
        var device = _enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
        var sessions = device.AudioSessionManager.Sessions;
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
        _enumerator.Dispose();
        _disposed = true;
    }
}
