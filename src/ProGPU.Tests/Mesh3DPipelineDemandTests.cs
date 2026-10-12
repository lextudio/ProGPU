using System.Globalization;
using System.Numerics;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Media3D;
using ProGPU.Scene.Extensions;
using ProGPU.Tests.Headless;
using Xunit;

namespace ProGPU.Tests;

[Collection("Pipeline diagnostics environment")]
public sealed class Mesh3DPipelineDemandTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void LateMaterialAndWireframeModesRetainPixelsAndCreateOnlyDemandedPipelines(bool wireframeFirst)
    {
        string? previous = Environment.GetEnvironmentVariable("PROGPU_BACKEND_DIAGNOSTICS");
        TextWriter originalOutput = Console.Out;
        using var output = new StringWriter(CultureInfo.InvariantCulture);
        try
        {
            Environment.SetEnvironmentVariable("PROGPU_BACKEND_DIAGNOSTICS", "1");
            Console.SetOut(output);
            using var window = new HeadlessWindow(96, 64);
            var material = new DiffuseMaterial
            {
                Color = new Vector4(0.2f, 0.7f, 0.9f, 1f),
                SelfIllumination = 1f
            };
            var mesh = new MeshGeometry3D
            {
                Positions = [new(-0.8f, -0.8f, 0), new(0.8f, -0.8f, 0), new(0, 0.8f, 0)],
                Normals = [-Vector3.UnitZ, -Vector3.UnitZ, -Vector3.UnitZ],
                TriangleIndices = [0, 2, 1],
                Edges = [new MeshEdge3D(new(-0.8f, -0.8f, 0), new(0.8f, -0.8f, 0),
                    -Vector3.UnitZ, Vector3.Zero, MeshEdgeTopology3D.Boundary)]
            };
            var model = new GeometryModel3D { Geometry = mesh, Material = material };
            var viewport = new Viewport3D
            {
                EnableRetainedSceneCache = true,
                Camera = new OrthographicCamera
                {
                    Position = new(0, 0, -3), LookDirection = Vector3.UnitZ, Width = 2.5f
                },
                ShadingMode = ShadingMode3D.Flat,
                RenderMode = wireframeFirst ? RenderMode3D.Wireframe : RenderMode3D.Solid
            };
            viewport.Children.Add(new ModelVisual3D { Content = model });
            window.Content = viewport;
            try
            {
                byte[] first = DrawAndReplay(1, 1);
                Assert.True(first.Where((_, i) => i % 4 == 2).Count(value => value > 100) > 20);

                viewport.RenderMode = wireframeFirst ? RenderMode3D.Solid : RenderMode3D.Wireframe;
                byte[] second = DrawAndReplay(2, 1);
                byte[] solid = wireframeFirst ? second : first;

                viewport.RenderMode = RenderMode3D.Solid;
                model.Material = null;
                model.BackMaterial = material;
                mesh.TriangleIndices = [0, 1, 2];
                mesh.Normals = [Vector3.UnitZ, Vector3.UnitZ, Vector3.UnitZ];
                viewport.InvalidateScene();
                Assert.Equal(solid, DrawAndReplay(3, 1));

                model.Material = material;
                mesh.TriangleIndices = [0, 2, 1];
                mesh.Normals = [-Vector3.UnitZ, -Vector3.UnitZ, -Vector3.UnitZ];
                viewport.InvalidateScene();
                DrawAndReplay(3, 2);

                // Edge draws still need the solid bindings in a wireframe pass.
                // Grow the record buffer while that mode is active, then return
                // to solid: neither mode may reuse bindings to retired buffers.
                viewport.RenderMode = RenderMode3D.SolidWireframe;
                viewport.EdgeStyle = new Mesh3DEdgeStyle(
                    Mesh3DEdgeDisplay.Boundary | Mesh3DEdgeDisplay.Occluded,
                    Vector4.One, new Vector4(0, 1, 0, 1), 2, 30, 5, 3);
                for (int i = 0; i < 20; i++)
                    viewport.Children.Add(new ModelVisual3D { Content = model });
                viewport.InvalidateScene();
                DrawAndReplay(5, 44);

                viewport.Children.Clear();
                viewport.Children.Add(new ModelVisual3D { Content = model });
                model.BackMaterial = null;
                viewport.EdgeStyle = Mesh3DEdgeStyle.Disabled;
                viewport.RenderMode = wireframeFirst ? RenderMode3D.Wireframe : RenderMode3D.Solid;
                viewport.InvalidateScene();
                Assert.Equal(first, DrawAndReplay(5, 1));

                byte[] DrawAndReplay(int expectedPipelines, int expectedDraws)
                {
                    window.Render();
                    byte[] cold = window.ReadPixels();
                    Assert.Equal(expectedDraws, viewport.LastMesh3DFrameMetrics.DrawCallCount);
                    viewport.Invalidate();
                    window.Render();
                    Assert.Equal(cold, window.ReadPixels());
                    Assert.Equal(expectedDraws, viewport.LastMesh3DFrameMetrics.DrawCallCount);
                    Assert.Equal(0UL, viewport.LastMesh3DFrameMetrics.RecordUploadBytes);
                    Assert.Equal(0UL, viewport.LastMesh3DFrameMetrics.GeometryVertexUploadBytes);
                    string[] created = output.ToString().Split('\n').Where(line =>
                        line.StartsWith("[PIPELINE] begin kind=render; key=Mesh3D", StringComparison.Ordinal)).ToArray();
                    Assert.Equal(expectedPipelines, created.Length);
                    Assert.Equal(created.Length, created.Distinct().Count());
                    return cold;
                }
            }
            finally
            {
                window.Content = null;
            }
        }
        finally
        {
            Console.SetOut(originalOutput);
            Environment.SetEnvironmentVariable("PROGPU_BACKEND_DIAGNOSTICS", previous);
        }
    }
}
