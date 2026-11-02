using System.Runtime.InteropServices;
using Silk.NET.Core.Loader;

namespace ProGPU.Backend;

/// <summary>
/// Lets Silk.NET find the GLFW library that NuGet restores under <c>runtimes/linux-&lt;arch&gt;/native</c> when an
/// application is built without a runtime identifier: every Debug build, <c>dotnet run</c>, and test projects.
/// </summary>
/// <remarks>
/// Silk.NET 2.23 probes <c>runtimes/&lt;rid&gt;/native</c> only for the RIDs it derives from the machine's own RID
/// (<c>ubuntu.24.04-x64</c> on Ubuntu). Without a RID graph its fallback maps a distribution prefix to
/// <c>linux-&lt;arch&gt;</c> only for the distributions it lists, and Ubuntu is not one of them, so on Ubuntu (GitHub's
/// runners included) it never looks in <c>runtimes/linux-x64/native</c>: GLFW fails to load, Silk reports
/// "GlfwPlatform - not applicable" and no window can be created. A RID-specific publish copies the library next to
/// the application, which is why packaged builds were unaffected. Each windowing entry point calls
/// <see cref="EnsureRegistered"/> before its first GLFW use.
/// </remarks>
public static class GlfwLibraryResolver
{
    private static readonly object s_gate = new();
    private static bool s_registered;

    /// <summary>
    /// Adds the portable <c>runtimes/linux-&lt;arch&gt;/native</c> folders to Silk.NET's default library search, after
    /// its own candidates. Does nothing outside Linux. The resolver is added once; a call that could not add it
    /// leaves the next call free to try again.
    /// </summary>
    public static void EnsureRegistered()
    {
        if (!OperatingSystem.IsLinux())
            return;

        lock (s_gate)
        {
            if (s_registered || PathResolver.Default is not DefaultPathResolver resolver)
                return;

            string? assemblyDirectory = Path.GetDirectoryName(typeof(GlfwLibraryResolver).Assembly.Location);
            resolver.Resolvers.Add(name => EnumerateCandidates(
                name,
                RuntimeInformation.ProcessArchitecture,
                File.Exists,
                AppContext.BaseDirectory,
                assemblyDirectory));
            s_registered = true;
        }
    }

    /// <summary>
    /// The existing <c>runtimes/linux-&lt;arch&gt;/native/&lt;name&gt;</c> files under the given directories, for a bare
    /// library name. Silk.NET applies every resolver to each candidate it already has, so rooted paths produced by
    /// its other resolvers are passed over.
    /// </summary>
    internal static IEnumerable<string> EnumerateCandidates(
        string libraryName,
        Architecture architecture,
        Func<string, bool> exists,
        params string?[] baseDirectories)
    {
        string? arch = architecture switch
        {
            Architecture.X64 => "x64",
            Architecture.Arm64 => "arm64",
            Architecture.Arm => "arm",
            Architecture.X86 => "x86",
            _ => null
        };
        if (arch is null || string.IsNullOrWhiteSpace(libraryName) || Path.IsPathRooted(libraryName)
            || !string.IsNullOrEmpty(Path.GetDirectoryName(libraryName)))
            yield break;

        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (string? directory in baseDirectories)
        {
            if (string.IsNullOrWhiteSpace(directory))
                continue;
            string candidate = Path.Combine(directory, "runtimes", "linux-" + arch, "native", libraryName);
            if (seen.Add(candidate) && exists(candidate))
                yield return candidate;
        }
    }
}
