using System.Collections;
using System.Numerics;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using ProGPU.Scene;
using ProGPU.Text;
using ProGPU.Vector;
using ProGPU.Virtualization;
using Xunit;

namespace ProGPU.Tests;

public sealed class SamplePerformanceRegressionTests
{
    [Fact]
    public void NavigationContentSwitchDoesNotRebuildPaneItems()
    {
        var navigation = new NavigationView();
        var first = new NavigationViewItem("First", "", () => new Border());
        var second = new NavigationViewItem("Second", "", () => new Border());
        navigation.MenuItems.Add(first);
        navigation.MenuItems.Add(second);
        navigation.SelectedItem = first;

        var pane = Assert.IsAssignableFrom<Visual>(first.Parent);
        var paneVersion = pane.ChangeVersion;

        navigation.SelectedItem = second;
        navigation.SelectedItem = first;

        Assert.Same(pane, first.Parent);
        Assert.Same(pane, second.Parent);
        Assert.InRange(pane.ChangeVersion - paneVersion, 1, 8);
    }

    [Fact]
    public void ReparentingWithinTheSameThemeDoesNotInvalidateTheSubtree()
    {
        var firstParent = new Grid();
        var secondParent = new Grid();
        var child = new ThemeChangeCounter();
        child.AddChild(new ThemeChangeCounter());
        firstParent.AddChild(child);
        var initialCount = child.TotalThemeChanges;

        secondParent.AddChild(child);

        Assert.Equal(initialCount, child.TotalThemeChanges);

        secondParent.RemoveChild(child);
        secondParent.RequestedTheme = ThemeManager.CurrentTheme == ElementTheme.Dark
            ? ElementTheme.Light
            : ElementTheme.Dark;
        secondParent.AddChild(child);

        Assert.Equal(initialCount + 2, child.TotalThemeChanges);
    }

    [Fact]
    public void IndexedItemsSourceRemainsLazyForVirtualizedControls()
    {
        var items = new ThrowingIndexedList(65_535);
        var control = new ItemsControl
        {
            ItemsPanel = new UniformVirtualizingGridPanel(),
            ItemsSource = items,
        };

        Assert.Equal(65_535, control.ItemCount);
        Assert.Empty(control.Items);
        Assert.Equal(42, control.GetItemAt(42));
        Assert.Equal(0, items.EnumerationCount);
    }

    [Fact]
    public void LayoutPropertyChangesInvalidateTheRetainedVisualTree()
    {
        var root = new Grid();
        var child = new Border();
        root.AddChild(child);
        root.Measure(new Vector2(400f, 300f));
        root.Arrange(new Rect(0f, 0f, 400f, 300f));
        root.IsDirty = false;
        child.IsDirty = false;
        var version = root.ChangeVersion;

        child.Padding = new Thickness(12f);

        Assert.True(child.IsDirty);
        Assert.True(root.IsDirty);
        Assert.True(root.ChangeVersion > version);
    }

    [Fact]
    public void MotionMarkSchedulesBeforeRenderAndReusesOfficialPathGeometry()
    {
        var previousFont = ProGPU.Samples.AppState._font;
        ProGPU.Samples.AppState._font = null;
        try
        {
            var visual = new ProGPU.Samples.MotionMarkShowcaseVisual();
            visual.Measure(new Vector2(900f, 620f));
            visual.Arrange(new Rect(0f, 0f, 900f, 620f));

            var firstContext = new DrawingContext();
            var versionBeforeRender = visual.ChangeVersion;
            visual.OnRender(firstContext);
            Assert.Equal(versionBeforeRender, visual.ChangeVersion);

            var firstPaths = firstContext.Commands
                .Where(static command => command.Type == RenderCommandType.DrawPath)
                .Select(static command => command.Path)
                .ToArray();
            Assert.NotEmpty(firstPaths);
            Assert.True(firstPaths.Length < visual.ElementCount);
            Assert.DoesNotContain(firstContext.Commands, static command =>
                command.Type is RenderCommandType.DrawLine or RenderCommandType.DrawBezier or RenderCommandType.DrawCubicBezier);

            var secondContext = new DrawingContext();
            visual.OnRender(secondContext);
            var secondPaths = secondContext.Commands
                .Where(static command => command.Type == RenderCommandType.DrawPath)
                .Select(static command => command.Path)
                .ToArray();
            Assert.Equal(firstPaths.Length, secondPaths.Length);
            for (var index = 0; index < firstPaths.Length; index++)
            {
                Assert.Same(firstPaths[index], secondPaths[index]);
            }

            var versionBeforeAdvance = visual.ChangeVersion;
            Assert.IsAssignableFrom<ProGPU.Samples.IAnimatedElement>(visual).Update(1f / 60f);
            Assert.True(visual.ChangeVersion > versionBeforeAdvance);
        }
        finally
        {
            ProGPU.Samples.AppState._font = previousFont;
        }
    }

    [Fact]
    public void MutableRunInvalidatesAndRelayoutsItsOwningTextBlock()
    {
        var font = LoadTestFont();
        var run = new Microsoft.UI.Xaml.Documents.Run("A");
        var text = new RichTextBlock { Font = font, FontSize = 18f };
        text.Inlines.Add(new Microsoft.UI.Xaml.Documents.Bold(run));
        text.Measure(new Vector2(300f, 100f));
        text.Arrange(new Rect(0f, 0f, 300f, 100f));
        Assert.Single(text.PositionedChars);
        var version = text.ChangeVersion;

        run.Text = "AAAA";

        Assert.True(text.ChangeVersion > version);
        text.Measure(new Vector2(300f, 100f));
        text.Arrange(new Rect(0f, 0f, 300f, 100f));
        Assert.Equal(4, text.PositionedChars.Count);
    }

    [Fact]
    public void RichEditCaretChangesReuseExistingTextLayout()
    {
        var editor = new RichEditBox { Font = LoadTestFont(), Text = "Caret layout remains retained." };
        editor.Measure(new Vector2(400f, 160f));
        editor.Arrange(new Rect(0f, 0f, 400f, 160f));
        var scrollViewer = Assert.IsType<ScrollViewer>(Assert.Single(editor.Children));
        var text = Assert.IsType<RichTextBlock>(scrollViewer.Content);
        var firstPositionedCharacter = Assert.IsType<PositionedRichChar>(text.PositionedChars[0]);

        editor.CaretIndex = 5;
        editor.Measure(new Vector2(400f, 160f));
        editor.Arrange(new Rect(0f, 0f, 400f, 160f));

        Assert.Same(firstPositionedCharacter, text.PositionedChars[0]);
    }

    [Fact]
    public void VirtualizedVisibleRebindPreservesRealizedVisuals()
    {
        var bindCount = 0;
        var panel = new VirtualizingScrollPanel
        {
            ItemsCount = 1_000,
            ItemHeight = 24f,
            CreateVisualFactory = static () => new Border(),
            BindVisualCallback = (_, _) => bindCount++
        };
        panel.Measure(new Vector2(320f, 120f));
        panel.Arrange(new Rect(0f, 0f, 320f, 120f));
        var realized = panel.Children.ToArray();
        var initialBindCount = bindCount;

        panel.RebindVisibleItems();

        Assert.True(bindCount > initialBindCount);
        Assert.Equal(realized.Length, panel.Children.Count);
        for (var index = 0; index < realized.Length; index++)
        {
            Assert.Same(realized[index], panel.Children[index]);
        }
    }

    [Fact]
    public void FontIconUsesRetainedOutlineWithOfficialPathTransform()
    {
        var path = new[]
        {
            "/System/Library/Fonts/Supplemental/Arial.ttf",
            "/Library/Fonts/Arial.ttf",
            "/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf",
        }.FirstOrDefault(File.Exists);
        Assert.NotNull(path);

        var font = new TtfFont(path!);
        var glyph = font.GetGlyphIndex('A');
        var outline = font.GetGlyphOutline(glyph);
        Assert.NotNull(outline);

        var icon = new FontIcon
        {
            Font = font,
            GlyphIndex = glyph,
            FontSize = 48f,
        };
        icon.Measure(new Vector2(80f, 80f));
        icon.Arrange(new Rect(0f, 0f, 80f, 80f));

        var context = new DrawingContext();
        icon.OnRender(context);

        var command = Assert.Single(context.Commands, static command => command.Type == RenderCommandType.DrawPath);
        Assert.Same(outline, command.Path);
        Assert.NotEqual(default, command.Transform);
        Assert.Contains(outline.Figures.SelectMany(static figure => figure.Segments), static segment =>
            segment is CubicBezierSegment or QuadraticBezierSegment or LineSegment);
    }

    private static TtfFont LoadTestFont()
    {
        var path = new[]
        {
            "/System/Library/Fonts/Supplemental/Arial.ttf",
            "/Library/Fonts/Arial.ttf",
            "/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf",
        }.FirstOrDefault(File.Exists);
        Assert.NotNull(path);
        return new TtfFont(path!);
    }

    private sealed class ThrowingIndexedList : IList
    {
        public ThrowingIndexedList(int count)
        {
            Count = count;
        }

        public int EnumerationCount { get; private set; }
        public int Count { get; }
        public bool IsFixedSize => true;
        public bool IsReadOnly => true;
        public bool IsSynchronized => false;
        public object SyncRoot => this;
        public object? this[int index]
        {
            get => index >= 0 && index < Count ? index : throw new ArgumentOutOfRangeException(nameof(index));
            set => throw new NotSupportedException();
        }

        public IEnumerator GetEnumerator()
        {
            EnumerationCount++;
            throw new InvalidOperationException("Indexed sources must not be enumerated eagerly.");
        }

        public bool Contains(object? value) => value is int index && index >= 0 && index < Count;
        public int IndexOf(object? value) => value is int index && index >= 0 && index < Count ? index : -1;
        public void CopyTo(Array array, int index) => throw new NotSupportedException();
        public int Add(object? value) => throw new NotSupportedException();
        public void Clear() => throw new NotSupportedException();
        public void Insert(int index, object? value) => throw new NotSupportedException();
        public void Remove(object? value) => throw new NotSupportedException();
        public void RemoveAt(int index) => throw new NotSupportedException();
    }

    private sealed class ThemeChangeCounter : Grid
    {
        private int _themeChanges;

        public int TotalThemeChanges => _themeChanges + Children.OfType<ThemeChangeCounter>().Sum(static child => child.TotalThemeChanges);

        protected override void OnThemeChanged()
        {
            _themeChanges++;
            base.OnThemeChanged();
        }
    }
}
