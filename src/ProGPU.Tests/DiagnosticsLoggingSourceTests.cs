namespace ProGPU.Tests;

using Xunit;

public class DiagnosticsLoggingSourceTests
{
    [Theory]
    [InlineData("src", "ProGPU.Backend", "WgpuContext.cs", "ProGpuBackendDiagnostics.WriteLine(", "Configuring SwapChain", "Console.WriteLine($\"[WebGPU Context] Configuring SwapChain")]
    [InlineData("src", "ProGPU.Text", "TextLayout.cs", "ProGpuTextDiagnostics.WriteLine(", "Loaded system fallback font", "Console.WriteLine($\"[TextLayout]")]
    [InlineData("src", "ProGPU.Text", "GlyphAtlas.cs", "ProGpuTextDiagnostics.WriteLine(", "GlyphAtlas", "Console.WriteLine(\"[GlyphAtlas]")]
    [InlineData("src", "ProGPU.Vector", "PathAtlas.cs", "ProGpuVectorDiagnostics.WriteLine(", "PathAtlas", "Console.WriteLine(\"[PathAtlas]")]
    public void NormalLifecycleDiagnosticsStayOptIn(
        string root,
        string project,
        string fileName,
        string expectedDiagnosticsCall,
        string diagnosticMessage,
        string forbiddenDirectConsoleCall)
    {
        string source = File.ReadAllText(FindRepoFile(root, project, fileName));

        Assert.Contains(expectedDiagnosticsCall, source, StringComparison.Ordinal);
        Assert.Contains(diagnosticMessage, source, StringComparison.Ordinal);
        Assert.DoesNotContain(forbiddenDirectConsoleCall, source, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("src", "ProGPU.Backend", "ProGpuBackendDiagnostics.cs", "PROGPU_BACKEND_DIAGNOSTICS")]
    [InlineData("src", "ProGPU.Text", "ProGpuTextDiagnostics.cs", "PROGPU_TEXT_DIAGNOSTICS")]
    [InlineData("src", "ProGPU.Vector", "ProGpuVectorDiagnostics.cs", "PROGPU_VECTOR_DIAGNOSTICS")]
    public void DiagnosticsHelpersKeepConsoleOutputBehindEnvironmentSwitch(
        string root,
        string project,
        string fileName,
        string environmentVariable)
    {
        string source = File.ReadAllText(FindRepoFile(root, project, fileName));

        Assert.Contains(environmentVariable, source, StringComparison.Ordinal);
        Assert.Contains("Environment.GetEnvironmentVariable", source, StringComparison.Ordinal);
        Assert.Contains("Console.WriteLine(message)", source, StringComparison.Ordinal);
    }

    private static string FindRepoFile(params string[] pathParts)
    {
        for (DirectoryInfo? directory = new(AppContext.BaseDirectory);
             directory != null;
             directory = directory.Parent)
        {
            string candidate = Path.Combine(new[] { directory.FullName }.Concat(pathParts).ToArray());
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        throw new FileNotFoundException($"Could not locate {Path.Combine(pathParts)}.");
    }
}
