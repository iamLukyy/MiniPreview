using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceProcess;

namespace MiniPreview.Bootstrap;

public sealed class FeatureChecker
{
    private readonly List<(string Name, Func<ProbeResult> Probe)> _probes = new();

    public void AddProbe(string name, Func<ProbeResult> probe)
        => _probes.Add((name, probe));

    public ProbeResult[] RunProbes()
    {
        var results = new ProbeResult[_probes.Count];
        for (int i = 0; i < _probes.Count; i++)
        {
            try
            {
                results[i] = _probes[i].Probe();
            }
            catch (Exception ex)
            {
                results[i] = new ProbeResult(_probes[i].Name, Ok: false, Detail: ex.Message, FixCommand: null);
            }
        }
        return results;
    }

    public static bool AllOk(IEnumerable<ProbeResult> results) => results.All(r => r.Ok);

    public static FeatureChecker CreateDefault()
    {
        var checker = new FeatureChecker();
        checker.AddProbe("WGC", ProbeWgc);
        checker.AddProbe("Audio Service", () => ProbeService("Audiosrv",
            "Set-Service -Name Audiosrv -StartupType Automatic; Start-Service Audiosrv"));
        checker.AddProbe("DWM Service", () => ProbeService("uxsms",
            "Set-Service -Name uxsms -StartupType Automatic; Start-Service uxsms"));
        return checker;
    }

    private static ProbeResult ProbeWgc()
    {
        try
        {
            var ok = Windows.Graphics.Capture.GraphicsCaptureSession.IsSupported();
            return ok
                ? new ProbeResult("WGC", true, "Windows.Graphics.Capture dostupné", null)
                : new ProbeResult("WGC", false, "WGC nepodporováno na tomto systému (vyžaduje Win10 1903+)", null);
        }
        catch (Exception ex)
        {
            return new ProbeResult("WGC", false, ex.Message, null);
        }
    }

    private static ProbeResult ProbeService(string serviceName, string fixCommand)
    {
        try
        {
            using var sc = new ServiceController(serviceName);
            var running = sc.Status == ServiceControllerStatus.Running;
            return running
                ? new ProbeResult(serviceName, true, $"Služba {serviceName} běží", null)
                : new ProbeResult(serviceName, false, $"Služba {serviceName} neběží ({sc.Status})", fixCommand);
        }
        catch (Exception ex)
        {
            return new ProbeResult(serviceName, false, ex.Message, fixCommand);
        }
    }
}
