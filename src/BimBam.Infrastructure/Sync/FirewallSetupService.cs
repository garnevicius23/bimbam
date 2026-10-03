using System.ComponentModel;
using System.Diagnostics;
using BimBam.Core.Interfaces;

namespace BimBam.Infrastructure.Sync;

/// <summary>
/// Adds one inbound Windows Firewall rule for this BimBam executable, limited to the local
/// subnet (remoteip=localsubnet) and applied to every network profile, because Windows treats a
/// phone hotspot as a Public network. Any existing rules for the executable are removed first:
/// if a user ever dismissed Windows' own "allow access" prompt, Windows created a Block rule,
/// and Block rules override Allow rules.
/// </summary>
public sealed class FirewallSetupService : IFirewallSetupService
{
    private const string RuleName = "BimBam";

    private static string ExecutablePath => Environment.ProcessPath ?? throw new InvalidOperationException("Nežinomas programos kelias.");

    public bool IsConfigured()
    {
        try
        {
            var output = Run("netsh", $"advfirewall firewall show rule name=\"{RuleName}\" verbose");
            return output.Contains(ExecutablePath, StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException)
        {
            return false;
        }
    }

    public async Task<bool> ConfigureAsync()
    {
        var exe = ExecutablePath;
        var commands =
            $"netsh advfirewall firewall delete rule name=all program=\"{exe}\" & " +
            $"netsh advfirewall firewall add rule name=\"{RuleName}\" dir=in action=allow program=\"{exe}\" " +
            "enable=yes profile=any remoteip=localsubnet";

        try
        {
            using var process = Process.Start(new ProcessStartInfo("cmd.exe", $"/c {commands}")
            {
                UseShellExecute = true,
                Verb = "runas",
                WindowStyle = ProcessWindowStyle.Hidden
            });
            if (process is not null)
            {
                await process.WaitForExitAsync();
            }
        }
        catch (Win32Exception)
        {
            // The user declined the administrator prompt.
            return false;
        }

        return IsConfigured();
    }

    private static string Run(string fileName, string arguments)
    {
        using var process = Process.Start(new ProcessStartInfo(fileName, arguments)
        {
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true
        }) ?? throw new InvalidOperationException();
        var output = process.StandardOutput.ReadToEnd();
        process.WaitForExit();
        return output;
    }
}
