using System.Runtime.CompilerServices;

// These public types moved to ProGPU.Text.Shaping; forward them so assemblies compiled against
// earlier ProGPU.Text packages keep loading.
[assembly: TypeForwardedTo(typeof(ProGPU.Text.ArabicFallbackPlan))]
[assembly: TypeForwardedTo(typeof(ProGPU.Text.CpuOpenTypeShaper))]
[assembly: TypeForwardedTo(typeof(ProGPU.Text.OpenTypeFeatureSetting))]
[assembly: TypeForwardedTo(typeof(ProGPU.Text.OpenTypeScriptResolver))]
[assembly: TypeForwardedTo(typeof(ProGPU.Text.OpenTypeTextShaper))]
[assembly: TypeForwardedTo(typeof(ProGPU.Text.ShapedGlyph))]
[assembly: TypeForwardedTo(typeof(ProGPU.Text.TextShapingOptions))]
[assembly: TypeForwardedTo(typeof(ProGPU.Text.UnicodeNormalizationPlan))]
[assembly: TypeForwardedTo(typeof(ProGPU.Text.UnicodeShapingProperties))]
