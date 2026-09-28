using System.Reflection;
using System.Runtime.InteropServices;

namespace DoraSaver.Core;

/// <summary>
/// Finds the WebView2 assemblies and native loader. The .scr files sit in System32 on their own,
/// so everything they depend on is looked up in the install folder instead.
/// </summary>
internal static class DependencyResolver
{
    public static string ExecutableDirectory => Path.GetDirectoryName(Application.ExecutablePath) ?? ".";

    public static string ProgramFilesFolder => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "DoraSaver");

    public static IEnumerable<string> SearchFolders() =>
    [
        ExecutableDirectory,
        Path.Combine(ExecutableDirectory, PlayerPaths.DataFolderName),
        ProgramFilesFolder,
    ];

    public static Assembly? Resolve(string assemblyFullName)
    {
        string fileName = new AssemblyName(assemblyFullName).Name + ".dll";
        foreach (string folder in SearchFolders())
        {
            string candidate = Path.Combine(folder, fileName);
            if (File.Exists(candidate))
            {
                return Assembly.LoadFrom(candidate);
            }
        }

        return null;
    }

    /// <summary>Folder holding WebView2Loader.dll for this process's CPU architecture.</summary>
    public static string LoaderFolder(string assetFolder) =>
        Path.Combine(assetFolder, "runtimes", "win-" + ArchitectureName(RuntimeInformation.ProcessArchitecture), "native");

    public static string ArchitectureName(Architecture architecture) => architecture switch
    {
        Architecture.X86 => "x86",
        Architecture.Arm64 => "arm64",
        _ => "x64",
    };
}
