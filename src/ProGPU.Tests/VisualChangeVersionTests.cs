using System.Numerics;
using ProGPU.Backend;
using ProGPU.Scene;
using ProGPU.Tests.Headless;
using Silk.NET.WebGPU;
using Xunit;

namespace ProGPU.Tests;

public sealed class VisualChangeVersionTests
{
    [Fact]
    public void PropertyChangeIncrementsChangeVersionEvenWhenAlreadyDirty()
    {
        var visual = new Visual();
        var initialVersion = visual.ChangeVersion;

        visual.Offset = new Vector2(10f, 20f);

        Assert.True(visual.IsDirty);
        Assert.True(visual.ChangeVersion > initialVersion);
    }

    [Fact]
    public void ClearingDirtyDoesNotIncrementChangeVersion()
    {
        var visual = new Visual
        {
            Offset = new Vector2(1f, 2f)
        };
        var changedVersion = visual.ChangeVersion;

        visual.IsDirty = false;

        Assert.False(visual.IsDirty);
        Assert.Equal(changedVersion, visual.ChangeVersion);
    }

    [Fact]
    public void SettingDirtyDirectlyIncrementsChangeVersion()
    {
        var visual = new Visual();
        visual.IsDirty = false;
        var cleanVersion = visual.ChangeVersion;

        visual.IsDirty = true;

        Assert.True(visual.IsDirty);
        Assert.True(visual.ChangeVersion > cleanVersion);
    }

    [Fact]
    public void ChildInvalidationIncrementsParentChangeVersion()
    {
        var parent = new ContainerVisual();
        var child = new Visual();
        parent.AddChild(child);
        parent.IsDirty = false;
        child.IsDirty = false;
        var parentVersion = parent.ChangeVersion;

        child.Opacity = 0.5f;

        Assert.True(child.IsDirty);
        Assert.True(parent.IsDirty);
        Assert.True(parent.ChangeVersion > parentVersion);
    }

    [Fact]
    public void ChildCollectionChangesIncrementParentChangeVersion()
    {
        var parent = new ContainerVisual();
        var initialVersion = parent.ChangeVersion;
        var child = new Visual();

        parent.AddChild(child);
        var addVersion = parent.ChangeVersion;
        parent.RemoveChild(child);

        Assert.True(addVersion > initialVersion);
        Assert.True(parent.ChangeVersion > addVersion);
    }

    [Fact]
    public void RenderOffscreenDoesNotMutateOffsetOrChangeVersion()
    {
        using var window = new HeadlessWindow(64, 64);
        using var target = new GpuTexture(
            window.Context,
            64,
            64,
            TextureFormat.Rgba8Unorm,
            TextureUsage.RenderAttachment | TextureUsage.TextureBinding,
            "RenderOffscreen ChangeVersion Test");
        var visual = new DrawingVisual
        {
            Offset = new Vector2(10f, 20f),
            Size = new Vector2(16f, 16f)
        };
        visual.IsDirty = false;
        var version = visual.ChangeVersion;
        var offset = visual.Offset;

        window.Compositor.RenderOffscreen(
            visual,
            width: 64,
            height: 64,
            targetTexture: target,
            padding: 4f,
            dpiScale: 1f);

        Assert.Equal(offset, visual.Offset);
        Assert.Equal(version, visual.ChangeVersion);
        Assert.False(visual.IsDirty);
    }
}
