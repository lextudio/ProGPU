using Thickness = Microsoft.UI.Xaml.Thickness;
using System;
using System.Numerics;
using ProGPU.Backend;
using ProGPU.Layout;
using ProGPU.Scene;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Markup;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Documents;
using StackPanel = Microsoft.UI.Xaml.Controls.StackPanel;

namespace ProGPU.Samples;

public static class SettingsPage
{
    public static FrameworkElement Create()
    {
        var stack = new StackPanel { Orientation = Orientation.Vertical, Margin = new Thickness(10) };

        var title = new RichTextBlock { Font = AppState._font, FontSize = 18f, Margin = new Thickness(0, 0, 0, 10) };
        title.Inlines.Add(new Bold(new Run("Application Settings")));
        stack.AddChild(title);

        var description = new RichTextBlock { Font = AppState._font, FontSize = 12f, Margin = new Thickness(0, 0, 0, 20) };
        description.Inlines.Add(new Run("Configure global engine options, display modes, and runtime rendering optimizations dynamically."));
        stack.AddChild(description);

        // 1. VSync setting (ToggleSwitch)
        var vsyncGroup = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 15) };
        
        var isVSyncOn = WgpuContext.Current?.VSync ?? false;
        var vsyncToggle = new ToggleSwitch { IsOn = isVSyncOn };
        
        var vsyncLabel = new RichTextBlock { Font = AppState._font, FontSize = 12f };
        vsyncLabel.Inlines.Add(new Run("Enable Vertical Synchronization (VSync)"));
        vsyncToggle.Content = vsyncLabel;

        var vsyncStatus = new RichTextBlock { Font = AppState._font, FontSize = 11f, Margin = new Thickness(20, 4, 0, 0) };
        vsyncStatus.Inlines.Add(new Run(isVSyncOn ? "State: Active (Capped FPS)" : "State: Inactive (Uncapped FPS)"));

        vsyncToggle.Toggled += (s, e) =>
        {
            bool nextVal = vsyncToggle.IsOn;
            
            // Update VSync for all active WebGPU contexts and their GLFW windows globally
            foreach (var context in WgpuContext.ActiveContexts)
            {
                context.VSync = nextVal;
                if (context.Window != null)
                {
                    context.Window.VSync = nextVal;
                }
            }
            
            vsyncStatus.Inlines.Clear();
            vsyncStatus.Inlines.Add(new Run(nextVal ? "State: Active (Capped FPS)" : "State: Inactive (Uncapped FPS)"));
            vsyncStatus.Invalidate();
        };

        vsyncGroup.AddChild(vsyncToggle);
        vsyncGroup.AddChild(vsyncStatus);
        stack.AddChild(vsyncGroup);

        // 2. Layered Visual Caching setting (CacheAsLayer)
        var cacheGroup = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 15) };
        
        var isCacheOn = Compositor.IsCacheAsLayerEnabled;
        var cacheToggle = new ToggleSwitch { IsOn = isCacheOn };
        
        var cacheLabel = new RichTextBlock { Font = AppState._font, FontSize = 12f };
        cacheLabel.Inlines.Add(new Run("Enable Layered High-DPI Visual Caching (CacheAsLayer)"));
        cacheToggle.Content = cacheLabel;

        var cacheStatus = new RichTextBlock { Font = AppState._font, FontSize = 11f, Margin = new Thickness(20, 4, 0, 0) };
        cacheStatus.Inlines.Add(new Run(isCacheOn ? "State: Active (Visual Caching)" : "State: Inactive (Frame-by-Frame Redraw)"));

        cacheToggle.Toggled += (s, e) =>
        {
            bool nextVal = cacheToggle.IsOn;
            Compositor.IsCacheAsLayerEnabled = nextVal;
            
            cacheStatus.Inlines.Clear();
            cacheStatus.Inlines.Add(new Run(nextVal ? "State: Active (Visual Caching)" : "State: Inactive (Frame-by-Frame Redraw)"));
            cacheStatus.Invalidate();
        };

        cacheGroup.AddChild(cacheToggle);
        cacheGroup.AddChild(cacheStatus);
        stack.AddChild(cacheGroup);

        // 3. Diagnostics Overlay setting
        var diagGroup = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 15) };
        
        var diagnosticsToggle = new ToggleSwitch { IsOn = DevToolsService.IsDevToolsActive };
        var diagLabel = new RichTextBlock { Font = AppState._font, FontSize = 12f };
        diagLabel.Inlines.Add(new Run("Show DevTools Diagnostic Overlay"));
        diagnosticsToggle.Content = diagLabel;

        diagnosticsToggle.Toggled += (s, e) =>
        {
            DevToolsService.IsDevToolsActive = diagnosticsToggle.IsOn;
        };

        diagGroup.AddChild(diagnosticsToggle);
        stack.AddChild(diagGroup);

        return stack;
    }
}
