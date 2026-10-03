namespace BimBam.Core.Interfaces;

/// <summary>
/// One-time Windows Firewall setup that lets other laptops on the same local network (and only
/// there) reach this laptop when it hosts a session.
/// </summary>
public interface IFirewallSetupService
{
    bool IsConfigured();

    /// <summary>Asks Windows for administrator rights and adds the rule.</summary>
    /// <returns>True when the rule is in place afterwards.</returns>
    Task<bool> ConfigureAsync();
}
