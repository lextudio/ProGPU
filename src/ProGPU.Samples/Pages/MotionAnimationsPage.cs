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

public static class MotionAnimationsPage
{
        public static FrameworkElement Create()
        {
            var grid = new Microsoft.UI.Xaml.Controls.Grid { Margin = new Thickness(12) };
            grid.RowDefinitions.Add(new GridLength(50, GridUnitType.Absolute));   // Header description
            grid.RowDefinitions.Add(new GridLength(1, GridUnitType.Star));       // Showcase cards
    
            var descText = new RichTextBlock { Font = AppState._font, FontSize = 12f, Margin = new Thickness(0, 0, 0, 10) };
            descText.Inlines.Add(new Run("This page showcases modern high-performance GPU-accelerated motion and composition animations, including keyframe loops, spring wobbles, and dynamic expressions."));
            grid.AddChild(descText);
            Microsoft.UI.Xaml.Controls.Grid.SetRow(descText, 0);
    
            var cardsGrid = new Microsoft.UI.Xaml.Controls.Grid();
            cardsGrid.ColumnDefinitions.Add(new GridLength(1f, GridUnitType.Star));
            cardsGrid.ColumnDefinitions.Add(new GridLength(1f, GridUnitType.Star));
            cardsGrid.ColumnDefinitions.Add(new GridLength(1f, GridUnitType.Star));
    
            var keyframeCard = new KeyframeShowcaseCard(AppState._font!);
            cardsGrid.AddChild(keyframeCard);
            Microsoft.UI.Xaml.Controls.Grid.SetColumn(keyframeCard, 0);
    
            var springCard = new SpringWobbleShowcaseCard(AppState._font!);
            cardsGrid.AddChild(springCard);
            Microsoft.UI.Xaml.Controls.Grid.SetColumn(springCard, 1);
    
            var expressionCard = new ExpressionTrackingShowcaseCard(AppState._font!);
            cardsGrid.AddChild(expressionCard);
            Microsoft.UI.Xaml.Controls.Grid.SetColumn(expressionCard, 2);
    
            grid.AddChild(cardsGrid);
            Microsoft.UI.Xaml.Controls.Grid.SetRow(cardsGrid, 1);
    
            return grid;
        }
}
