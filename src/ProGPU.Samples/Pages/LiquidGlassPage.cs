using Thickness = Microsoft.UI.Xaml.Thickness;
using System;
using System.Collections.Generic;
using System.Numerics;
using Silk.NET.Windowing;
using Silk.NET.Input;
using ProGPU.Backend;
using ProGPU.Scene;
using ProGPU.Layout;
using ProGPU.Vector;
using ProGPU.Text;
using ProGPU.Compute;
using ProGPU.Virtualization;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Markup;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Documents;
using Button = Microsoft.UI.Xaml.Controls.Button;
using StackPanel = Microsoft.UI.Xaml.Controls.StackPanel;

namespace ProGPU.Samples;

public static class LiquidGlassPage
{
    private static RichTextBlock CreateLabel(string text)
    {
        return new RichTextBlock
        {
            Font = AppState._font,
            FontSize = 11f,
            Margin = new Thickness(0, 4, 0, 4)
        };
    }

    public static FrameworkElement Create()
    {
        var mainGrid = new Microsoft.UI.Xaml.Controls.Grid();
        mainGrid.ColumnDefinitions.Add(new GridLength(1f, GridUnitType.Star));      // LEFT: Showroom
        mainGrid.ColumnDefinitions.Add(new GridLength(320, GridUnitType.Absolute)); // RIGHT: Studio Settings

        // Active effect states
        var fluidColor = new Vector4(0f, 0.75f, 1.0f, 0.85f); // Neon Cyan default
        var glassColor = new Vector4(1f, 1f, 1f, 0.2f);      // Clear Crystal default
        float progress = 0.55f;
        float refraction = 0.45f;
        float shininess = 48f;

        // Create the compute-accelerated effects
        var cardEffect = new LiquidGlassEffect(progress, glassColor, fluidColor) { Refraction = refraction, Shininess = shininess };
        var bubble1Effect = new LiquidGlassEffect(progress + 0.1f, glassColor, fluidColor) { Refraction = refraction + 0.15f, Shininess = shininess * 1.5f };
        var bubble2Effect = new LiquidGlassEffect(progress - 0.15f, glassColor, fluidColor) { Refraction = refraction - 0.1f, Shininess = shininess * 0.8f };

        // Keep track of effects to update them in unison
        var activeEffects = new List<LiquidGlassEffect> { cardEffect, bubble1Effect, bubble2Effect };

        // ================= LEFT: SHOWROOM =================
        var showroomScroll = new ScrollViewer { Background = new ThemeResourceBrush("PageBackground") };
        var showroomStack = new StackPanel { Orientation = Orientation.Vertical, Margin = new Thickness(24) };
        showroomScroll.Content = showroomStack;

        // Title and description
        var showroomTitle = new RichTextBlock { Font = AppState._font, FontSize = 22f, Margin = new Thickness(0, 0, 0, 8) };
        showroomTitle.Inlines.Add(new Bold(new Run("macOS 26 Tahoe Liquid Glass Studio") { Foreground = new ThemeResourceBrush("SystemAccentColor") }));
        showroomStack.AddChild(showroomTitle);

        var showroomDesc = new RichTextBlock { Font = AppState._font, FontSize = 12f, Margin = new Thickness(0, 0, 0, 24) };
        showroomDesc.Inlines.Add(new Run("Experience stunning hardware-accelerated 3D vector refraction and organic cellular wave dynamics. The compute shader estimates smooth beveled surface normals from any shape's alpha channel in real-time, refracts what lies behind, and fills it with colored glass fluid."));
        showroomStack.AddChild(showroomDesc);

        // Grid for showcase cards
        var showcaseGrid = new Microsoft.UI.Xaml.Controls.Grid();
        showcaseGrid.ColumnDefinitions.Add(new GridLength(1f, GridUnitType.Star));
        showcaseGrid.ColumnDefinitions.Add(new GridLength(1f, GridUnitType.Star));

        // ----- COLUMN 1: Glass Credit Card -----
        var col1 = new StackPanel { Orientation = Orientation.Vertical, Margin = new Thickness(0, 0, 12, 0) };
        
        var cardSectionTitle = new RichTextBlock { Font = AppState._font, FontSize = 14f, Margin = new Thickness(0, 0, 0, 12) };
        cardSectionTitle.Inlines.Add(new Bold(new Run("Interactive Credit Card Glassmorphism")));
        col1.AddChild(cardSectionTitle);

        // Container card (acts as background with a rich multi-stop gradient and chip to refract)
        var creditCard = new Border
        {
            Width = 280f,
            Height = 176f,
            CornerRadius = 16f,
            BorderThickness = new Thickness(1f),
            BorderBrush = new SolidColorBrush(new Vector4(1f, 1f, 1f, 0.15f)),
            Background = new SolidColorBrush(new Vector4(1f, 1f, 1f, 0.03f)) // Silhouette background for the effect!
        };

        // Render card content inside (layered under the glass shader refraction!)
        var cardContent = new Microsoft.UI.Xaml.Controls.Grid { Margin = new Thickness(16) };
        
        // Chip visual
        var chip = new Border
        {
            Width = 32f,
            Height = 24f,
            CornerRadius = 4f,
            Background = new SolidColorBrush(new Vector4(0.85f, 0.65f, 0.15f, 0.9f)),
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top
        };
        cardContent.AddChild(chip);

        // Card Title/Logo
        var logoText = new RichTextBlock
        {
            Font = AppState._font,
            FontSize = 14f,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Top
        };
        logoText.Inlines.Add(new Bold(new Run("Tahoe Metal") { Foreground = new SolidColorBrush(new Vector4(1f, 1f, 1f, 0.95f)) }));
        cardContent.AddChild(logoText);

        // Card Number
        var numberText = new RichTextBlock
        {
            Font = AppState._font,
            FontSize = 15f,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 30, 0, 0)
        };
        numberText.Inlines.Add(new Run("4000  8826  0912  2026") { Foreground = new SolidColorBrush(new Vector4(1f, 1f, 1f, 0.85f)) });
        cardContent.AddChild(numberText);

        // Holder Name
        var holderText = new RichTextBlock
        {
            Font = AppState._font,
            FontSize = 11f,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Bottom
        };
        holderText.Inlines.Add(new Run("LIQUID GLASS LABS") { Foreground = new SolidColorBrush(new Vector4(1f, 1f, 1f, 0.7f)) });
        cardContent.AddChild(holderText);

        // Visa brand circles
        var brandGrid = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Bottom
        };
        var circleRed = new Border { Width = 20f, Height = 20f, CornerRadius = 10f, Background = new SolidColorBrush(new Vector4(1f, 0.2f, 0.2f, 0.75f)), Margin = new Thickness(0, 0, -8, 0) };
        var circleYellow = new Border { Width = 20f, Height = 20f, CornerRadius = 10f, Background = new SolidColorBrush(new Vector4(1f, 0.75f, 0f, 0.75f)) };
        brandGrid.AddChild(circleRed);
        brandGrid.AddChild(circleYellow);
        cardContent.AddChild(brandGrid);

        creditCard.Child = cardContent;
        creditCard.Effect = cardEffect;
        col1.AddChild(creditCard);
        
        // Add a secondary floating label below it
        var labelText = new RichTextBlock { Font = AppState._font, FontSize = 11f, Margin = new Thickness(4, 12, 0, 0) };
        labelText.Inlines.Add(new Run("Note: Specular beveled edges and glass refraction automatically recalculate as you interact with the sliders. Text and vectors behind are physically distorted by the estimated 3D normal field.") { Foreground = new ThemeResourceBrush("TextSecondary") });
        col1.AddChild(labelText);

        showcaseGrid.AddChild(col1);
        Microsoft.UI.Xaml.Controls.Grid.SetColumn(col1, 0);

        // ----- COLUMN 2: Glass Sphere Bubbles -----
        var col2 = new StackPanel { Orientation = Orientation.Vertical, Margin = new Thickness(12, 0, 0, 0) };
        
        var bubbleTitle = new RichTextBlock { Font = AppState._font, FontSize = 14f, Margin = new Thickness(0, 0, 0, 12) };
        bubbleTitle.Inlines.Add(new Bold(new Run("Dynamic Spherical Fluid Bubbles")));
        col2.AddChild(bubbleTitle);

        var bubblesCanvas = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 20) };

        // Bubble 1: Large
        var bubble1 = new Border
        {
            Width = 110f,
            Height = 110f,
            CornerRadius = 55f, // Perfect circle!
            Background = new SolidColorBrush(new Vector4(1f, 1f, 1f, 0.03f)),
            BorderThickness = new Thickness(1f),
            BorderBrush = new SolidColorBrush(new Vector4(1f, 1f, 1f, 0.2f)),
            Margin = new Thickness(0, 0, 24, 0),
            Effect = bubble1Effect
        };
        bubblesCanvas.AddChild(bubble1);

        // Bubble 2: Medium
        var bubble2 = new Border
        {
            Width = 80f,
            Height = 80f,
            CornerRadius = 40f, // Perfect circle!
            Background = new SolidColorBrush(new Vector4(1f, 1f, 1f, 0.03f)),
            BorderThickness = new Thickness(1f),
            BorderBrush = new SolidColorBrush(new Vector4(1f, 1f, 1f, 0.2f)),
            VerticalAlignment = VerticalAlignment.Center,
            Effect = bubble2Effect
        };
        bubblesCanvas.AddChild(bubble2);

        col2.AddChild(bubblesCanvas);

        // Slider control with macOS Tahoe aesthetic
        var sliderTitle = new RichTextBlock { Font = AppState._font, FontSize = 13f, Margin = new Thickness(0, 10, 0, 8) };
        sliderTitle.Inlines.Add(new Bold(new Run("Chunky Tahoe macOS Slider Control")));
        col2.AddChild(sliderTitle);

        var tahoeSlider = new Slider
        {
            Minimum = 0,
            Maximum = 100,
            Value = 55,
            Width = 260f,
            RequestedThemeFamily = VisualThemeFamily.macOS // Force macOS Tahoe look!
        };
        col2.AddChild(tahoeSlider);

        showcaseGrid.AddChild(col2);
        Microsoft.UI.Xaml.Controls.Grid.SetColumn(col2, 1);

        showroomStack.AddChild(showcaseGrid);
        mainGrid.AddChild(showroomScroll);
        Microsoft.UI.Xaml.Controls.Grid.SetColumn(showroomScroll, 0);


        // ================= RIGHT: STUDIO ADJUSTMENTS PANEL =================
        var rightBorder = new Border
        {
            Background = new ThemeResourceBrush("CardBackground"),
            BorderBrush = new ThemeResourceBrush("ControlBorder"),
            BorderThickness = new Thickness(1f, 0, 0, 0),
            Padding = new Thickness(16, 24, 16, 24)
        };
        
        var rightStack = new StackPanel { Orientation = Orientation.Vertical };
        rightBorder.Child = rightStack;

        var studioTitle = new RichTextBlock { Font = AppState._font, FontSize = 16f, Margin = new Thickness(0, 0, 0, 4) };
        studioTitle.Inlines.Add(new Bold(new Run("Tahoe Glass Studio") { Foreground = new ThemeResourceBrush("TextPrimary") }));
        rightStack.AddChild(studioTitle);

        var studioDesc = new RichTextBlock { Font = AppState._font, FontSize = 11f, Margin = new Thickness(0, 0, 0, 20) };
        studioDesc.Inlines.Add(new Run("Configure the GPU compute shader lighting, refraction index, and wave progress in real-time.") { Foreground = new ThemeResourceBrush("TextSecondary") });
        rightStack.AddChild(studioDesc);

        // 1. LIQUID PROGRESS (FILL LEVEL)
        var progressLabel = new RichTextBlock { Font = AppState._font, FontSize = 12f, Margin = new Thickness(0, 8, 0, 4) };
        progressLabel.Inlines.Add(new Bold(new Run($"Fluid Fill Level: {(progress * 100f):F0}%")));
        rightStack.AddChild(progressLabel);

        var progressSlider = new Microsoft.UI.Xaml.Controls.Slider { Minimum = 0f, Maximum = 1f, Value = progress, Width = 280f, Margin = new Thickness(0, 0, 0, 16) };
        rightStack.AddChild(progressSlider);

        // 2. REFRACTION INDEX
        var refractionLabel = new RichTextBlock { Font = AppState._font, FontSize = 12f, Margin = new Thickness(0, 8, 0, 4) };
        refractionLabel.Inlines.Add(new Bold(new Run($"Refraction Index: {refraction:F2}")));
        rightStack.AddChild(refractionLabel);

        var refractionSlider = new Microsoft.UI.Xaml.Controls.Slider { Minimum = 0f, Maximum = 1.5f, Value = refraction, Width = 280f, Margin = new Thickness(0, 0, 0, 16) };
        rightStack.AddChild(refractionSlider);

        // 3. SHININESS (SPECULAR SIZE)
        var shininessLabel = new RichTextBlock { Font = AppState._font, FontSize = 12f, Margin = new Thickness(0, 8, 0, 4) };
        shininessLabel.Inlines.Add(new Bold(new Run($"Specular Shininess: {shininess:F0}")));
        rightStack.AddChild(shininessLabel);

        var shininessSlider = new Microsoft.UI.Xaml.Controls.Slider { Minimum = 8f, Maximum = 128f, Value = shininess, Width = 280f, Margin = new Thickness(0, 0, 0, 20) };
        rightStack.AddChild(shininessSlider);

        // 4. FLUID COLOR SELECTOR
        var fluidColorLabel = new RichTextBlock { Font = AppState._font, FontSize = 12f, Margin = new Thickness(0, 8, 0, 8) };
        fluidColorLabel.Inlines.Add(new Bold(new Run("Neon Fluid Colors:")));
        rightStack.AddChild(fluidColorLabel);

        var fluidBtnRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 20) };
        
        var cyanBtn = new Button { Width = 60f, Height = 28f, CornerRadius = 6f, Margin = new Thickness(0, 0, 8, 0), Background = new SolidColorBrush(new Vector4(0f, 0.75f, 1f, 1f)) };
        var cyanText = new RichTextBlock { Font = AppState._font, FontSize = 9f }; cyanText.Inlines.Add(new Bold(new Run("Cyan"))); cyanBtn.Content = cyanText;
        fluidBtnRow.AddChild(cyanBtn);

        var pinkBtn = new Button { Width = 60f, Height = 28f, CornerRadius = 6f, Margin = new Thickness(0, 0, 8, 0), Background = new SolidColorBrush(new Vector4(1f, 0.4f, 0.5f, 1f)) };
        var pinkText = new RichTextBlock { Font = AppState._font, FontSize = 9f }; pinkText.Inlines.Add(new Bold(new Run("Rose"))); pinkBtn.Content = pinkText;
        fluidBtnRow.AddChild(pinkBtn);

        var greenBtn = new Button { Width = 60f, Height = 28f, CornerRadius = 6f, Margin = new Thickness(0, 0, 8, 0), Background = new SolidColorBrush(new Vector4(0.1f, 0.8f, 0.4f, 1f)) };
        var greenText = new RichTextBlock { Font = AppState._font, FontSize = 9f }; greenText.Inlines.Add(new Bold(new Run("Lime"))); greenBtn.Content = greenText;
        fluidBtnRow.AddChild(greenBtn);

        var orangeBtn = new Button { Width = 60f, Height = 28f, CornerRadius = 6f, Background = new SolidColorBrush(new Vector4(1f, 0.6f, 0.1f, 1f)) };
        var orangeText = new RichTextBlock { Font = AppState._font, FontSize = 9f }; orangeText.Inlines.Add(new Bold(new Run("Sunset"))); orangeBtn.Content = orangeText;
        fluidBtnRow.AddChild(orangeBtn);

        rightStack.AddChild(fluidBtnRow);

        // 5. GLASS CONTAINER STYLES
        var glassLabel = new RichTextBlock { Font = AppState._font, FontSize = 12f, Margin = new Thickness(0, 8, 0, 8) };
        glassLabel.Inlines.Add(new Bold(new Run("Glass Preset Styles:")));
        rightStack.AddChild(glassLabel);

        var glassBtnRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 20) };

        var crystalBtn = new Button { Width = 85f, Height = 28f, CornerRadius = 6f, Margin = new Thickness(0, 0, 8, 0), Background = new SolidColorBrush(new Vector4(1f, 1f, 1f, 0.15f)) };
        var crystalText = new RichTextBlock { Font = AppState._font, FontSize = 9f }; crystalText.Inlines.Add(new Bold(new Run("Clear Crystal"))); crystalBtn.Content = crystalText;
        glassBtnRow.AddChild(crystalBtn);

        var obsidianBtn = new Button { Width = 85f, Height = 28f, CornerRadius = 6f, Margin = new Thickness(0, 0, 8, 0), Background = new SolidColorBrush(new Vector4(0.1f, 0.1f, 0.15f, 0.35f)) };
        var obsidianText = new RichTextBlock { Font = AppState._font, FontSize = 9f }; obsidianText.Inlines.Add(new Bold(new Run("Dark Obsidian"))); obsidianBtn.Content = obsidianText;
        glassBtnRow.AddChild(obsidianBtn);

        var emeraldBtn = new Button { Width = 85f, Height = 28f, CornerRadius = 6f, Background = new SolidColorBrush(new Vector4(0f, 0.25f, 0.2f, 0.25f)) };
        var emeraldText = new RichTextBlock { Font = AppState._font, FontSize = 9f }; emeraldText.Inlines.Add(new Bold(new Run("Frozen Teal"))); emeraldBtn.Content = emeraldText;
        glassBtnRow.AddChild(emeraldBtn);

        rightStack.AddChild(glassBtnRow);

        mainGrid.AddChild(rightBorder);
        Microsoft.UI.Xaml.Controls.Grid.SetColumn(rightBorder, 1);


        // ================= SLIDERS & BUTTONS EVENT BINDINGS =================

        progressSlider.ValueChanged += (s, e) =>
        {
            float val = progressSlider.Value;
            progressLabel.Inlines.Clear();
            progressLabel.Inlines.Add(new Bold(new Run($"Fluid Fill Level: {(val * 100f):F0}%")));
            progressLabel.Invalidate();

            // Synchronize showcase effects
            cardEffect.Progress = val;
            bubble1Effect.Progress = val + 0.1f;
            bubble2Effect.Progress = val - 0.15f;

            // Synchronize the Tahoe Slider
            tahoeSlider.Value = val * 100f;

            creditCard.Invalidate();
            bubble1.Invalidate();
            bubble2.Invalidate();
        };

        refractionSlider.ValueChanged += (s, e) =>
        {
            float val = refractionSlider.Value;
            refractionLabel.Inlines.Clear();
            refractionLabel.Inlines.Add(new Bold(new Run($"Refraction Index: {val:F2}")));
            refractionLabel.Invalidate();

            foreach (var fx in activeEffects)
            {
                fx.Refraction = val;
            }

            creditCard.Invalidate();
            bubble1.Invalidate();
            bubble2.Invalidate();
        };

        shininessSlider.ValueChanged += (s, e) =>
        {
            float val = shininessSlider.Value;
            shininessLabel.Inlines.Clear();
            shininessLabel.Inlines.Add(new Bold(new Run($"Specular Shininess: {val:F0}")));
            shininessLabel.Invalidate();

            foreach (var fx in activeEffects)
            {
                fx.Shininess = val;
            }

            creditCard.Invalidate();
            bubble1.Invalidate();
            bubble2.Invalidate();
        };

        // Fluid color buttons
        cyanBtn.Click += (s, e) =>
        {
            var color = new Vector4(0f, 0.75f, 1f, 0.85f);
            foreach (var fx in activeEffects) fx.FluidColor = color;
            creditCard.Invalidate(); bubble1.Invalidate(); bubble2.Invalidate();
        };

        pinkBtn.Click += (s, e) =>
        {
            var color = new Vector4(1f, 0.4f, 0.5f, 0.85f);
            foreach (var fx in activeEffects) fx.FluidColor = color;
            creditCard.Invalidate(); bubble1.Invalidate(); bubble2.Invalidate();
        };

        greenBtn.Click += (s, e) =>
        {
            var color = new Vector4(0.1f, 0.8f, 0.4f, 0.85f);
            foreach (var fx in activeEffects) fx.FluidColor = color;
            creditCard.Invalidate(); bubble1.Invalidate(); bubble2.Invalidate();
        };

        orangeBtn.Click += (s, e) =>
        {
            var color = new Vector4(1f, 0.6f, 0.1f, 0.85f);
            foreach (var fx in activeEffects) fx.FluidColor = color;
            creditCard.Invalidate(); bubble1.Invalidate(); bubble2.Invalidate();
        };

        // Glass preset buttons
        crystalBtn.Click += (s, e) =>
        {
            var color = new Vector4(1f, 1f, 1f, 0.2f);
            foreach (var fx in activeEffects) fx.GlassColor = color;
            creditCard.Invalidate(); bubble1.Invalidate(); bubble2.Invalidate();
        };

        obsidianBtn.Click += (s, e) =>
        {
            var color = new Vector4(0.1f, 0.1f, 0.15f, 0.35f);
            foreach (var fx in activeEffects) fx.GlassColor = color;
            creditCard.Invalidate(); bubble1.Invalidate(); bubble2.Invalidate();
        };

        emeraldBtn.Click += (s, e) =>
        {
            var color = new Vector4(0f, 0.25f, 0.2f, 0.25f);
            foreach (var fx in activeEffects) fx.GlassColor = color;
            creditCard.Invalidate(); bubble1.Invalidate(); bubble2.Invalidate();
        };

        // Bind Tahiti slider value change to update the liquid progress in reverse!
        tahoeSlider.ValueChanged += (s, e) =>
        {
            float normVal = tahoeSlider.Value / 100f;
            progressSlider.Value = normVal;
        };

        return mainGrid;
    }
}
