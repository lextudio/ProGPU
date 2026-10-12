using Microsoft.UI.Xaml;
using System.Runtime.CompilerServices;

namespace ProGPU.WinUI.Themes.Fluent;

/// <summary>
/// Entry point for the compiled, unchanged Microsoft UI XAML Fluent resource dictionary.
/// Referencing this type loads the assembly whose module initializer registers the URI.
/// </summary>
// Microsoft Fluent source: 25d2cb1c6e4086dd14b387a4a149cce0649dbe17
// generic.xaml SHA-256: 4c4085838721c0afcb1a9ee17591c0655cdddadb26d330788e08bcd7f1af8285
// CompiledFluentResources.cs preserves the existing dictionary construction.
public static class FluentThemeResources
{
    public const string ResourcePath =
        "ProGPU.WinUI.Themes.Fluent/Themes/Generic.xaml";

    public static ResourceDictionary CreateDictionary()
    {
        // Browser WebAssembly AOT can defer a referenced assembly's module constructor
        // until after this public entry point is reached. Root and run the retained,
        // idempotent provider registration before resolving the resource URI.
        RuntimeHelpers.RunModuleConstructor(typeof(FluentThemeResources).Module.ModuleHandle);
        return XamlResourceProviderRegistry.Create(
            new Uri(ResourcePath, UriKind.Relative));
    }

    public static ResourceDictionary Apply(Application application)
    {
        ArgumentNullException.ThrowIfNull(application);
        var dictionary = CreateDictionary();
        application.Resources.MergedDictionaries.Add(dictionary);
        return dictionary;
    }
}
