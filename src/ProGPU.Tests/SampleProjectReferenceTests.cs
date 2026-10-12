using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Xunit;

namespace ProGPU.Tests;

// Resolve the real direct project edges through the SDK without compiling,
// restoring, or reading ambient package assets. The APK workflow owns actual
// Android builds and normal package resolution.
public sealed class SampleProjectReferenceTests
{
    [Theory]
    [InlineData("true")]
    [InlineData("false")]
    [InlineData(null)]
    public async Task SampleMobileSelectionDoesNotCreateSharedDependencyBuildIdentities(string? mobile)
    {
        string root = FindRepositoryRoot();
        string project = Path.Combine(root, "src", "ProGPU.Samples", "ProGPU.Samples.csproj");
        string scratch = Path.Combine(Path.GetTempPath(), "progpu-sample-reference-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(scratch);
        try
        {
            string probe = Path.Combine(scratch, "SampleReferenceProbe.targets");
            await File.WriteAllTextAsync(probe, """
                <Project>
                  <Target Name="RequireSampleOnlyMobileSelection" BeforeTargets="GetTargetPath"
                          Condition="'$(MSBuildProjectName)' != 'ProGPU.Samples'">
                    <Error Condition="'$(ProGpuSamplesMobile)' != ''"
                           Text="Sample-only mobile selection leaked into shared project $(MSBuildProjectName)." />
                  </Target>
                </Project>
                """);
            using JsonDocument result = await ResolveReferences(root, project,
                "SampleMobileReferenceCheck", "android-x64", mobile, probe);
            JsonElement properties = result.RootElement.GetProperty("Properties");
            Assert.Equal(mobile ?? "", properties.GetProperty("ProGpuSamplesMobile").GetString());
            string[] sources = result.RootElement.GetProperty("Items").GetProperty("Compile").EnumerateArray()
                .Select(item => item.GetProperty("Identity").GetString()!.Replace('\\', '/')).ToArray();
            Assert.Equal(mobile != "true", sources.Contains("Pages/MarkdownPage.cs"));
            Assert.NotEmpty(result.RootElement.GetProperty("Items").GetProperty("_ResolvedProjectReferencePaths").EnumerateArray());
        }
        finally
        {
            Directory.Delete(scratch, recursive: true);
        }
    }

    private static async Task<JsonDocument> ResolveReferences(string root, string project, string configuration, string rid,
        string? mobile = "true", string? referenceProbe = null)
    {
        ProcessStartInfo start = new()
        {
            FileName = Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") ?? "dotnet",
            WorkingDirectory = root,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        foreach (string argument in new[]
        {
            "msbuild", project, "-nologo", "-verbosity:quiet", "-nodeReuse:false", "-maxCpuCount:1",
            "-target:ResolveProjectReferences", "-p:BuildProjectReferences=false",
            "-p:SkipResolvePackageAssets=true",
            "-p:Configuration=" + configuration,
            "-p:RuntimeIdentifier=" + rid, "-p:RuntimeIdentifiers=" + rid,
            "-getProperty:RuntimeIdentifier,RuntimeIdentifiers,ProGpuSamplesMobile",
            "-getItem:Analyzer,_ResolvedProjectReferencePaths,Compile"
        })
            start.ArgumentList.Add(argument);
        if (mobile is not null)
            start.ArgumentList.Add("-p:ProGpuSamplesMobile=" + mobile);
        if (referenceProbe is not null)
            start.ArgumentList.Add("-p:CustomAfterMicrosoftCommonTargets=" + referenceProbe);
        start.Environment["DOTNET_CLI_UI_LANGUAGE"] = "en-US";
        start.Environment["MSBUILDDISABLENODEREUSE"] = "1";
        using Process process = Process.Start(start) ?? throw new InvalidOperationException("Could not start dotnet msbuild.");
        Task<string> stdout = process.StandardOutput.ReadToEndAsync();
        Task<string> stderr = process.StandardError.ReadToEndAsync();
        using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(45));
        try
        {
            await process.WaitForExitAsync(timeout.Token);
        }
        catch (OperationCanceledException)
        {
            process.Kill(entireProcessTree: true);
            using CancellationTokenSource cleanupTimeout = new(TimeSpan.FromSeconds(10));
            await process.WaitForExitAsync(cleanupTimeout.Token);
            throw new TimeoutException($"Sample reference resolution exceeded 45 seconds.\n{await stdout}\n{await stderr}");
        }

        string output = await stdout;
        string diagnostics = output + "\n" + await stderr;
        Assert.True(process.ExitCode == 0, diagnostics);
        int jsonStart = output.IndexOf('{');
        Assert.True(jsonStart >= 0, diagnostics);
        return ParseResult(output[jsonStart..]);
    }

    private static JsonDocument ParseResult(string json)
    {
        Utf8JsonReader reader = new(Encoding.UTF8.GetBytes(json));
        return JsonDocument.ParseValue(ref reader);
    }

    private static string FindRepositoryRoot()
    {
        for (DirectoryInfo? directory = new(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "src", "ProGPU.Samples", "ProGPU.Samples.csproj")))
                return directory.FullName;
        throw new DirectoryNotFoundException("Could not locate the ProGPU sample project.");
    }
}
