using System.Windows;
using Xunit;
using WpfBrushMappingMode = System.Windows.Media.BrushMappingMode;
using WpfColorInterpolationMode = System.Windows.Media.ColorInterpolationMode;
using WpfColors = System.Windows.Media.Colors;
using WpfGradientSpreadMethod = System.Windows.Media.GradientSpreadMethod;
using WpfGradientStop = System.Windows.Media.GradientStop;
using WpfGradientStopCollection = System.Windows.Media.GradientStopCollection;
using WpfLinearGradientBrush = System.Windows.Media.LinearGradientBrush;
using WpfRadialGradientBrush = System.Windows.Media.RadialGradientBrush;
using VectorColorInterpolationMode = ProGPU.Vector.GradientColorInterpolationMode;
using VectorGradientSpreadMethod = ProGPU.Vector.GradientSpreadMethod;
using VectorLinearGradientBrush = ProGPU.Vector.LinearGradientBrush;
using VectorRadialGradientBrush = ProGPU.Vector.RadialGradientBrush;

namespace ProGPU.Tests;

public sealed class WpfGradientBrushShimTests
{
    [Fact]
    public void LinearGradientBrushLowersWpfStateToNativeGradientBrush()
    {
        var brush = new WpfLinearGradientBrush
        {
            StartPoint = new Point(0.25, 0.5),
            EndPoint = new Point(1, 0.75),
            Opacity = 0.5,
            MappingMode = WpfBrushMappingMode.Absolute,
            SpreadMethod = WpfGradientSpreadMethod.Repeat,
            ColorInterpolationMode = WpfColorInterpolationMode.ScRgbLinearInterpolation,
            GradientStops = new WpfGradientStopCollection
            {
                new(WpfColors.Red, 0),
                new(WpfColors.Blue, 1)
            }
        };

        var native = Assert.IsType<VectorLinearGradientBrush>(brush.ToNative());

        Assert.Equal(0.25f, native.StartPoint.X);
        Assert.Equal(0.5f, native.StartPoint.Y);
        Assert.Equal(1f, native.EndPoint.X);
        Assert.Equal(0.75f, native.EndPoint.Y);
        Assert.Equal(0.5f, native.Opacity);
        Assert.Equal(VectorGradientSpreadMethod.Repeat, native.SpreadMethod);
        Assert.Equal(VectorColorInterpolationMode.ScRgbLinearInterpolation, native.ColorInterpolationMode);
        Assert.Equal(2, native.Stops.Length);
        Assert.Equal(0f, native.Stops[0].Offset);
        Assert.Equal(1f, native.Stops[0].Color.X);
        Assert.Equal(1f, native.Stops[1].Offset);
        Assert.Equal(1f, native.Stops[1].Color.Z);
    }

    [Fact]
    public void RadialGradientBrushLowersWpfStateToNativeGradientBrush()
    {
        var brush = new WpfRadialGradientBrush
        {
            Center = new Point(0.4, 0.6),
            GradientOrigin = new Point(0.3, 0.2),
            RadiusX = 0.7,
            RadiusY = 0.8,
            SpreadMethod = WpfGradientSpreadMethod.Reflect,
            GradientStops = new WpfGradientStopCollection
            {
                new(WpfColors.Yellow, 0),
                new(WpfColors.Transparent, 1)
            }
        };

        var native = Assert.IsType<VectorRadialGradientBrush>(brush.ToNative());

        Assert.Equal(0.4f, native.Center.X);
        Assert.Equal(0.6f, native.Center.Y);
        Assert.Equal(0.3f, native.GradientOrigin.X);
        Assert.Equal(0.2f, native.GradientOrigin.Y);
        Assert.Equal(0.7f, native.RadiusX);
        Assert.Equal(0.8f, native.RadiusY);
        Assert.Equal(VectorGradientSpreadMethod.Reflect, native.SpreadMethod);
        Assert.Equal(2, native.Stops.Length);
    }

    [Fact]
    public void GradientStopCollectionBubblesChildChangesForRetainedInvalidation()
    {
        var stop = new WpfGradientStop(WpfColors.Red, 0);
        var collection = new WpfGradientStopCollection { stop };
        var changedCount = 0;
        collection.Changed += (_, _) => changedCount++;
        var version = collection.ChangeVersion;

        stop.Offset = 0.25;

        Assert.True(collection.ChangeVersion > version);
        Assert.Equal(1, changedCount);
    }
}
