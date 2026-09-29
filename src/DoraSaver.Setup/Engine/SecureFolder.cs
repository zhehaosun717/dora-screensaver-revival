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

    /// <summary>
    /// Replaces the ACLs of a folder tree with the ones inherited from its parent (for Program Files:
    /// everyone may read, only administrators may change).
    /// </summary>
    public static void ResetToInherited(string path)
    {
        string icacls = Path.Combine(Environment.SystemDirectory, "icacls.exe");
        var info = new System.Diagnostics.ProcessStartInfo(icacls, $"\"{path}\" /reset /T /C /Q")
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = Environment.SystemDirectory,
        };
        using System.Diagnostics.Process process = System.Diagnostics.Process.Start(info)
            ?? throw new InvalidOperationException("icacls did not start");
        if (!process.WaitForExit(120_000) || process.ExitCode != 0)
        {
            throw new UnauthorizedAccessException($"Resetting permissions on {path} failed (icacls exit {(process.HasExited ? process.ExitCode : -1)})");
        }
    }

    /// <summary>Whether BUILTIN\Users is granted read access on the folder itself.</summary>
    public static bool UsersCanRead(string path)
    {
        var users = new SecurityIdentifier(WellKnownSidType.BuiltinUsersSid, null);
        AuthorizationRuleCollection rules = new DirectoryInfo(path).GetAccessControl()
            .GetAccessRules(includeExplicit: true, includeInherited: true, typeof(SecurityIdentifier));
        return rules.Cast<FileSystemAccessRule>().Any(r =>
            r.IdentityReference.Equals(users)
            && r.AccessControlType == AccessControlType.Allow
            && (r.FileSystemRights & FileSystemRights.ReadAndExecute) == FileSystemRights.ReadAndExecute);
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
