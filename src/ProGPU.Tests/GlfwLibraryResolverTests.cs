using System.Runtime.InteropServices;
using ProGPU.Backend;
using Xunit;

namespace ProGPU.Tests;

public sealed class GlfwLibraryResolverTests
{
    [Theory]
    [InlineData(Architecture.X64, "linux-x64")]
    [InlineData(Architecture.Arm64, "linux-arm64")]
    public void BareNameResolvesToThePortableLinuxRuntimesFolder(Architecture architecture, string rid)
    {
        string app = Path.Combine(Path.GetTempPath(), "app");
        string expected = Path.Combine(app, "runtimes", rid, "native", "libglfw.so.3");

        string[] candidates = GlfwLibraryResolver.EnumerateCandidates(
            "libglfw.so.3", architecture, path => path == expected, app).ToArray();

        Assert.Equal(new[] { expected }, candidates);
    }

    [Fact]
    public void OnlyExistingFilesAreOfferedAndEachDirectoryOnce()
    {
        string app = Path.Combine(Path.GetTempPath(), "app");
        string other = Path.Combine(Path.GetTempPath(), "other");
        var probed = new List<string>();

        string[] candidates = GlfwLibraryResolver.EnumerateCandidates(
            "libglfw.so.3",
            Architecture.X64,
            path =>
            {
                probed.Add(path);
                return path.StartsWith(other, StringComparison.Ordinal);
            },
            app, null, "", app, other).ToArray();

        Assert.Equal(new[] { Path.Combine(other, "runtimes", "linux-x64", "native", "libglfw.so.3") }, candidates);
        Assert.Equal(2, probed.Count);
    }

    [Theory]
    [InlineData("/usr/lib/libglfw.so.3")]
    [InlineData("lib/libglfw.so.3")]
    [InlineData("")]
    public void RootedRelativeOrEmptyNamesAreLeftToSilk(string name)
    {
        string[] candidates = GlfwLibraryResolver.EnumerateCandidates(
            name, Architecture.X64, _ => true, Path.GetTempPath()).ToArray();

        Assert.Empty(candidates);
    }

    [Fact]
    public void AnArchitectureWithoutAPortableLinuxRidOffersNothing()
    {
        string[] candidates = GlfwLibraryResolver.EnumerateCandidates(
            "libglfw.so.3", Architecture.Wasm, _ => true, Path.GetTempPath()).ToArray();

        Assert.Empty(candidates);
    }
}
