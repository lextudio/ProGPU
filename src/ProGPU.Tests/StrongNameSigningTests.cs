using System.Reflection;
using ProGPU.Backend;
using ProGPU.DirectX;
using ProGPU.Scene;
using ProGPU.Text;
using ProGpuPathGeometry = ProGPU.Vector.PathGeometry;
using WpfBrush = System.Windows.Media.Brush;
using WpfPoint = System.Windows.Point;
using Xunit;

namespace ProGPU.Tests;

public sealed class StrongNameSigningTests
{
    private static readonly Type[] PublicAssemblyAnchorTypes =
    [
        typeof(WgpuContext),
        typeof(ProGpuDirectXDevice),
        typeof(Compositor),
        typeof(TtfFont),
        typeof(ProGpuPathGeometry),
        typeof(WpfPoint),
        typeof(WpfBrush)
    ];

    private static readonly byte[] ExpectedPublicKeyToken =
    [
        0xc2, 0x9c, 0x97, 0x52, 0x85, 0x5e, 0xe1, 0x83
    ];

    [Fact]
    public void ProGpuCoreAssembliesAreStrongNameSigned()
    {
        foreach (Type publicType in PublicAssemblyAnchorTypes)
        {
            var token = publicType.Assembly.GetName().GetPublicKeyToken();

            Assert.NotNull(token);
            Assert.Equal(ExpectedPublicKeyToken, token);
        }
    }
}
