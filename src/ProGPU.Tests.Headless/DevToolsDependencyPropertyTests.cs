using System.IO;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using ProGPU.Vector;
using Xunit;

namespace ProGPU.Tests.Headless;

public class DevToolsDependencyPropertyTests
{
    [Fact]
    public void DependencyPropertyEnumerationIncludesInheritedProperties()
    {
        var properties = DependencyProperty.GetRegisteredProperties(typeof(Button));

        Assert.Contains(FrameworkElement.WidthProperty, properties);
        Assert.Contains(Control.BackgroundProperty, properties);
    }

    [Fact]
    public void PropertyItemUpdatesDependencyPropertyValues()
    {
        var button = new Button();
        var widthItem = new PropertyItem(FrameworkElement.WidthProperty, button);
        var backgroundItem = new PropertyItem(Control.BackgroundProperty, button);

        widthItem.Value = "123.5";
        backgroundItem.Value = "#FF112233";

        Assert.Equal(123.5f, button.Width);
        Assert.IsType<SolidColorBrush>(button.Background);
        Assert.Equal("#FF112233", backgroundItem.Value);
    }

    [Fact]
    public void DevToolsSourceDoesNotUseClrPropertyReflection()
    {
        string source = File.ReadAllText(FindRepoFile("src/ProGPU.WinUI/Controls/DevTools.cs"));

        Assert.DoesNotContain("System.Reflection", source);
        Assert.DoesNotContain("PropertyInfo", source);
        Assert.DoesNotContain("BindingFlags", source);
        Assert.DoesNotContain("GetProperties(", source);
        Assert.DoesNotContain("SetValue(_element", source);
    }

    private static string FindRepoFile(string relativePath)
    {
        DirectoryInfo? current = new(Directory.GetCurrentDirectory());
        while (current != null)
        {
            string candidate = Path.Combine(current.FullName, relativePath);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            current = current.Parent;
        }

        throw new FileNotFoundException($"Could not find {relativePath} from {Directory.GetCurrentDirectory()}.");
    }
}
