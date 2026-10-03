using System.Security.Cryptography;
using System.Text;

namespace BimBam.Core.Services;

/// <summary>
/// Builds the same GUID from the same inputs on every laptop, so a line created by importing a
/// file gets an identical ID wherever the import operation is replayed.
/// </summary>
public static class DeterministicGuid
{
    public static Guid Create(Guid scope, string name)
    {
        var bytes = MD5.HashData(Encoding.UTF8.GetBytes($"{scope:N}|{name}"));
        return new Guid(bytes);
    }
}
