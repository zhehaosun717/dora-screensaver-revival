using System.Security.AccessControl;
using System.Security.Principal;

namespace DoraSaver.Setup.Engine;

/// <summary>
/// Folders that only SYSTEM and Administrators may change. The elevated installer writes downloads
/// here and runs tools from here, so a non-elevated process cannot swap a file between the hash
/// check and its use.
/// </summary>
internal static class SecureFolder
{
    private static readonly SecurityIdentifier Administrators = new(WellKnownSidType.BuiltinAdministratorsSid, null);
    private static readonly SecurityIdentifier LocalSystem = new(WellKnownSidType.LocalSystemSid, null);

    /// <summary>Creates the folder (or re-secures an existing one) with an admin-only, non-inherited ACL.</summary>
    public static string Ensure(string path)
    {
        Directory.CreateDirectory(path);
        var security = new DirectorySecurity();
        security.SetOwner(Administrators);
        security.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);
        const InheritanceFlags inherit = InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit;
        security.AddAccessRule(new FileSystemAccessRule(Administrators, FileSystemRights.FullControl, inherit, PropagationFlags.None, AccessControlType.Allow));
        security.AddAccessRule(new FileSystemAccessRule(LocalSystem, FileSystemRights.FullControl, inherit, PropagationFlags.None, AccessControlType.Allow));
        new DirectoryInfo(path).SetAccessControl(security);
        return path;
    }

    /// <summary>A fresh, uniquely named admin-only folder; nothing in it predates this run.</summary>
    public static string CreateFresh(string parent, string prefix)
    {
        Ensure(parent);
        string path = Path.Combine(parent, prefix + Guid.NewGuid().ToString("N"));
        if (Directory.Exists(path))
        {
            throw new IOException($"Work folder unexpectedly exists: {path}");
        }

        Directory.CreateDirectory(path);
        return path;
    }
}
