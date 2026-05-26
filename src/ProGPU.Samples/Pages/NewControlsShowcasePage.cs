using Thickness = Microsoft.UI.Xaml.Thickness;
using System;
using System.Collections.Generic;
using System.Numerics;
using ProGPU.Backend;
using ProGPU.Scene;
using ProGPU.Layout;
using ProGPU.Vector;
using ProGPU.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;

using StackPanel = Microsoft.UI.Xaml.Controls.StackPanel;

namespace ProGPU.Samples;

public static class NewControlsShowcasePage
{
    private static string _implicitSelected = "Single Pane";
    private static string _explicitSelected = "Dynamic HSL Mica";
    private static double _rating1 = 3.0;
    private static double _rating2 = 7.0;
    private static string _passwordText = "Antigravity100%";

    public static FrameworkElement Create()
    {
        var scrollViewer = new ScrollViewer
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };

        var stack = new StackPanel
        {
            Orientation = Orientation.Vertical,
            Margin = new Thickness(20),
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        scrollViewer.Content = stack;

        // Page Header
        var title = new RichTextBlock { Font = AppState._font, FontSize = 22f, Margin = new Thickness(0, 0, 0, 6) };
        title.Inlines.Add(new Bold(new Run("New WinUI 3 Controls Showcase")));
        stack.AddChild(title);

        var description = new RichTextBlock { Font = AppState._font, FontSize = 12f, Margin = new Thickness(0, 0, 0, 24) };
        description.Inlines.Add(new Run("This page showcases the newly implemented high-performance, Fluent-styled WinUI 3 controls. These include RadioButton with implicit and explicit grouping, RatingControl with polar vector stars, and PasswordBox with clipboard secure mask entry."));
        stack.AddChild(description);

        // ==========================================
        // 1. RADIO BUTTON SECTION
        // ==========================================
        var radioCard = CreateShowcaseCard("RadioButton (Mutually Exclusive Grouping)");
        var radioStack = new StackPanel { Orientation = Orientation.Vertical };

        var radioDesc = new RichTextBlock { Font = AppState._font, FontSize = 12f, Margin = new Thickness(0, 0, 0, 16) };
        radioDesc.Inlines.Add(new Run("Radio buttons are grouped to present mutually exclusive choices. Use the "));
        radioDesc.Inlines.Add(new Bold(new Run("Arrow Keys")));
        radioDesc.Inlines.Add(new Run(" to navigate and auto-select buttons in the group, and "));
        radioDesc.Inlines.Add(new Bold(new Run("Tab")));
        radioDesc.Inlines.Add(new Run(" to skip the group."));
        radioStack.AddChild(radioDesc);

        // Group A: Sibling Implicit Grouping
        var implicitTitle = new RichTextBlock { Font = AppState._font, FontSize = 13f, Margin = new Thickness(0, 0, 0, 10) };
        implicitTitle.Inlines.Add(new Bold(new Run("Implicit Group (Same Parent Sibling Scope):")));
        radioStack.AddChild(implicitTitle);

        var implicitContainer = new StackPanel { Orientation = Orientation.Vertical, Margin = new Thickness(10, 0, 0, 15) };
        
        var rb1 = new RadioButton { IsChecked = true };
        var rb1Label = new RichTextBlock { Font = AppState._font, FontSize = 12f };
        rb1Label.Inlines.Add(new Run("Single Pane"));
        rb1.Content = rb1Label;

        var rb2 = new RadioButton();
        var rb2Label = new RichTextBlock { Font = AppState._font, FontSize = 12f };
        rb2Label.Inlines.Add(new Run("Split Vertical"));
        rb2.Content = rb2Label;

        var rb3 = new RadioButton();
        var rb3Label = new RichTextBlock { Font = AppState._font, FontSize = 12f };
        rb3Label.Inlines.Add(new Run("Split Horizontal"));
        rb3.Content = rb3Label;

        var implicitStatus = new RichTextBlock { Font = AppState._font, FontSize = 12f, Margin = new Thickness(0, 8, 0, 16) };
        implicitStatus.Inlines.Add(new Run("Selected Layout: "));
        implicitStatus.Inlines.Add(new Bold(new Run(_implicitSelected)) { Foreground = new ThemeResourceBrush("SystemAccentColor") });

        rb1.Checked += (s, e) => UpdateImplicitStatus(implicitStatus, "Single Pane");
        rb2.Checked += (s, e) => UpdateImplicitStatus(implicitStatus, "Split Vertical");
        rb3.Checked += (s, e) => UpdateImplicitStatus(implicitStatus, "Split Horizontal");

        implicitContainer.AddChild(rb1);
        implicitContainer.AddChild(rb2);
        implicitContainer.AddChild(rb3);
        radioStack.AddChild(implicitContainer);
        radioStack.AddChild(implicitStatus);

        // Group B: Explicit GroupName Grouping
        var explicitTitle = new RichTextBlock { Font = AppState._font, FontSize = 13f, Margin = new Thickness(0, 0, 0, 10) };
        explicitTitle.Inlines.Add(new Bold(new Run("Explicit Group (Visual Tree GroupName Scope):")));
        radioStack.AddChild(explicitTitle);

        var explicitContainer = new StackPanel { Orientation = Orientation.Vertical, Margin = new Thickness(10, 0, 0, 15) };
        
        var erb1 = new RadioButton { GroupName = "ThemeGroup", IsChecked = true };
        var erb1Label = new RichTextBlock { Font = AppState._font, FontSize = 12f };
        erb1Label.Inlines.Add(new Run("Dynamic HSL Mica"));
        erb1.Content = erb1Label;

        var erb2 = new RadioButton { GroupName = "ThemeGroup" };
        var erb2Label = new RichTextBlock { Font = AppState._font, FontSize = 12f };
        erb2Label.Inlines.Add(new Run("Solid Acrylic"));
        erb2.Content = erb2Label;

        var erb3 = new RadioButton { GroupName = "ThemeGroup" };
        var erb3Label = new RichTextBlock { Font = AppState._font, FontSize = 12f };
        erb3Label.Inlines.Add(new Run("High Contrast Dark"));
        erb3.Content = erb3Label;

        var explicitStatus = new RichTextBlock { Font = AppState._font, FontSize = 12f, Margin = new Thickness(0, 8, 0, 8) };
        explicitStatus.Inlines.Add(new Run("Selected Design Theme: "));
        explicitStatus.Inlines.Add(new Bold(new Run(_explicitSelected)) { Foreground = new ThemeResourceBrush("SystemAccentColor") });

        erb1.Checked += (s, e) => UpdateExplicitStatus(explicitStatus, "Dynamic HSL Mica");
        erb2.Checked += (s, e) => UpdateExplicitStatus(explicitStatus, "Solid Acrylic");
        erb3.Checked += (s, e) => UpdateExplicitStatus(explicitStatus, "High Contrast Dark");

        explicitContainer.AddChild(erb1);
        explicitContainer.AddChild(erb2);
        explicitContainer.AddChild(erb3);
        radioStack.AddChild(explicitContainer);
        radioStack.AddChild(explicitStatus);

        radioCard.Child = radioStack;
        stack.AddChild(radioCard);

        // ==========================================
        // 2. RATING CONTROL SECTION
        // ==========================================
        var ratingCard = CreateShowcaseCard("RatingControl (Vector 5-Point Stars)");
        var ratingStack = new StackPanel { Orientation = Orientation.Vertical };

        var ratingDesc = new RichTextBlock { Font = AppState._font, FontSize = 12f, Margin = new Thickness(0, 0, 0, 16) };
        ratingDesc.Inlines.Add(new Run("RatingControl renders interactive polar vector stars. Move the mouse to hover, click to select, or focus and use "));
        ratingDesc.Inlines.Add(new Bold(new Run("Arrow Keys")));
        ratingDesc.Inlines.Add(new Run(" to increment/decrement rating value."));
        ratingStack.AddChild(ratingDesc);

        // Rating A: Standard 5-star
        var rc1Title = new RichTextBlock { Font = AppState._font, FontSize = 13f, Margin = new Thickness(0, 0, 0, 8) };
        rc1Title.Inlines.Add(new Bold(new Run("Standard 5-Star Interactive Rating:")));
        ratingStack.AddChild(rc1Title);

        var rc1Container = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 20) };
        var rc1 = new RatingControl { Value = _rating1, MaxRating = 5 };
        var rc1Status = new RichTextBlock { Font = AppState._font, FontSize = 12f, Margin = new Thickness(15, 6, 0, 0) };
        rc1Status.Inlines.Add(new Run($"Rating: {rc1.Value:F1} / 5.0"));

        rc1.ValueChanged += (s, val) =>
        {
            _rating1 = val;
            rc1Status.Inlines.Clear();
            rc1Status.Inlines.Add(new Run($"Rating: {_rating1:F1} / 5.0"));
            rc1Status.Invalidate();
        };

        rc1Container.AddChild(rc1);
        rc1Container.AddChild(rc1Status);
        ratingStack.AddChild(rc1Container);

        // Rating B: 10-star rating
        var rc2Title = new RichTextBlock { Font = AppState._font, FontSize = 13f, Margin = new Thickness(0, 0, 0, 8) };
        rc2Title.Inlines.Add(new Bold(new Run("High Capacity 10-Star Rating:")));
        ratingStack.AddChild(rc2Title);

        var rc2Container = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 20) };
        var rc2 = new RatingControl { Value = _rating2, MaxRating = 10 };
        var rc2Status = new RichTextBlock { Font = AppState._font, FontSize = 12f, Margin = new Thickness(15, 6, 0, 0) };
        rc2Status.Inlines.Add(new Run($"Score: {rc2.Value:F1} / 10.0"));

        rc2.ValueChanged += (s, val) =>
        {
            _rating2 = val;
            rc2Status.Inlines.Clear();
            rc2Status.Inlines.Add(new Run($"Score: {_rating2:F1} / 10.0"));
            rc2Status.Invalidate();
        };

        rc2Container.AddChild(rc2);
        rc2Container.AddChild(rc2Status);
        ratingStack.AddChild(rc2Container);

        // Rating C: Read-only and placeholder
        var rc3Title = new RichTextBlock { Font = AppState._font, FontSize = 13f, Margin = new Thickness(0, 0, 0, 8) };
        rc3Title.Inlines.Add(new Bold(new Run("Read-only fractional rating with Placeholder stars:")));
        ratingStack.AddChild(rc3Title);

        var rc3 = new RatingControl { IsReadOnly = true, PlaceholderValue = 3.5, Value = 0.0, Margin = new Thickness(0, 0, 0, 8) };
        var rc3Desc = new RichTextBlock { Font = AppState._font, FontSize = 11f, Margin = new Thickness(0, 0, 0, 8) };
        rc3Desc.Inlines.Add(new Run("Shows PlaceholderValue = 3.5. Disallows mouse clicks and keyboard focus."));

        ratingStack.AddChild(rc3);
        ratingStack.AddChild(rc3Desc);

        ratingCard.Child = ratingStack;
        stack.AddChild(ratingCard);

        // ==========================================
        // 3. PASSWORD BOX SECTION
        // ==========================================
        var passwordCard = CreateShowcaseCard("PasswordBox (Masked Entry & Reveal Button)");
        var passwordStack = new StackPanel { Orientation = Orientation.Vertical };

        var passDesc = new RichTextBlock { Font = AppState._font, FontSize = 12f, Margin = new Thickness(0, 0, 0, 16) };
        passDesc.Inlines.Add(new Run("PasswordBox provides secure masking for credentials. It completely blocks Clipboard Copy and Cut operations to prevent leakage, and includes an interactive eye toggle icon to reveal plain text."));
        passwordStack.AddChild(passDesc);

        // Demo A: Standard PasswordBox
        var pb1Title = new RichTextBlock { Font = AppState._font, FontSize = 13f, Margin = new Thickness(0, 0, 0, 8) };
        pb1Title.Inlines.Add(new Bold(new Run("Standard Password Input Box:")));
        passwordStack.AddChild(pb1Title);

        var pb1 = new PasswordBox { Password = _passwordText, WidthConstraint = 300f, Margin = new Thickness(0, 0, 0, 8) };
        var pb1Status = new RichTextBlock { Font = AppState._font, FontSize = 12f, Margin = new Thickness(0, 0, 0, 20) };
        pb1Status.Inlines.Add(new Run("Actual plain password state: "));
        pb1Status.Inlines.Add(new Bold(new Run(_passwordText)) { Foreground = new ThemeResourceBrush("SystemAccentColor") });

        pb1.PasswordChanged += (s, e) =>
        {
            _passwordText = pb1.Password;
            pb1Status.Inlines.Clear();
            pb1Status.Inlines.Add(new Run("Actual plain password state: "));
            pb1Status.Inlines.Add(new Bold(new Run(_passwordText)) { Foreground = new ThemeResourceBrush("SystemAccentColor") });
            pb1Status.Invalidate();
        };

        passwordStack.AddChild(pb1);
        passwordStack.AddChild(pb1Status);

        // Demo B: Custom Asterisk Mask and Disabled Box
        var pb2Title = new RichTextBlock { Font = AppState._font, FontSize = 13f, Margin = new Thickness(0, 0, 0, 8) };
        pb2Title.Inlines.Add(new Bold(new Run("Custom Asterisk Mask (*) & Disabled state:")));
        passwordStack.AddChild(pb2Title);

        var customPassBox = new PasswordBox { Password = "SecureUserPass", PasswordChar = '*', WidthConstraint = 300f, Margin = new Thickness(0, 0, 0, 10) };
        var disabledPassBox = new PasswordBox { Password = "DisabledPassWord", IsEnabled = false, WidthConstraint = 300f, Margin = new Thickness(0, 0, 0, 8) };

        passwordStack.AddChild(customPassBox);
        passwordStack.AddChild(disabledPassBox);

        passwordCard.Child = passwordStack;
        stack.AddChild(passwordCard);

        return scrollViewer;
    }

    private static void UpdateImplicitStatus(RichTextBlock statusBlock, string text)
    {
        _implicitSelected = text;
        statusBlock.Inlines.Clear();
        statusBlock.Inlines.Add(new Run("Selected Layout: "));
        statusBlock.Inlines.Add(new Bold(new Run(text)) { Foreground = new ThemeResourceBrush("SystemAccentColor") });
        statusBlock.Invalidate();
    }

    private static void UpdateExplicitStatus(RichTextBlock statusBlock, string text)
    {
        _explicitSelected = text;
        statusBlock.Inlines.Clear();
        statusBlock.Inlines.Add(new Run("Selected Design Theme: "));
        statusBlock.Inlines.Add(new Bold(new Run(text)) { Foreground = new ThemeResourceBrush("SystemAccentColor") });
        statusBlock.Invalidate();
    }

    private static Border CreateShowcaseCard(string headerText)
    {
        var border = new Border
        {
            Background = new ThemeResourceBrush("CardBackground"),
            BorderBrush = new ThemeResourceBrush("ControlBorder"),
            BorderThickness = new Thickness(1f),
            CornerRadius = 8f,
            Padding = new Thickness(20),
            Margin = new Thickness(0, 0, 0, 24),
            HorizontalAlignment = HorizontalAlignment.Stretch
        };

        var mainStack = new StackPanel { Orientation = Orientation.Vertical };
        var header = new RichTextBlock { Font = AppState._font, FontSize = 15f, Margin = new Thickness(0, 0, 0, 16) };
        header.Inlines.Add(new Bold(new Run(headerText)));
        
        mainStack.AddChild(header);
        
        // Horizontal divider stripe
        var divider = new Border
        {
            Height = 1f,
            Background = new ThemeResourceBrush("ControlBorder"),
            Margin = new Thickness(0, 0, 0, 16),
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        mainStack.AddChild(divider);

        // Content placeholder panel
        var contentPanel = new Border { HorizontalAlignment = HorizontalAlignment.Stretch };
        mainStack.AddChild(contentPanel);

        border.Child = mainStack;

        // Custom content wrapper
        border.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(Border.Child) && border.Child != mainStack)
            {
                var content = border.Child;
                border.Child = mainStack;
                contentPanel.Child = content;
            }
        };

        return border;
    }
}
