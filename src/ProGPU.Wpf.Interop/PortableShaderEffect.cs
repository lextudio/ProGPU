namespace ProGPU.Wpf.Interop;

public interface IPortablePixelShaderSource
{
    bool TryGetPortablePixelShader(out PortablePixelShader pixelShader);
}

public interface IPortableShaderEffectSource
{
    bool TryGetPortableShaderEffect(out PortableShaderEffect effect);
}

public enum PortableShaderSamplingMode
{
    NearestNeighbor = 0,
    Bilinear = 1,
    Auto = 2
}

public sealed class PortablePixelShader
{
    public PortablePixelShader(
        string? uriSource,
        string? absoluteUri,
        byte[]? bytecode,
        short majorVersion,
        short minorVersion)
    {
        UriSource = string.IsNullOrWhiteSpace(uriSource) ? null : uriSource;
        AbsoluteUri = string.IsNullOrWhiteSpace(absoluteUri) ? null : absoluteUri;
        Bytecode = bytecode is { Length: > 0 } ? (byte[])bytecode.Clone() : Array.Empty<byte>();
        MajorVersion = majorVersion;
        MinorVersion = minorVersion;
    }

    public string? UriSource { get; }

    public string? AbsoluteUri { get; }

    public byte[] Bytecode { get; }

    public short MajorVersion { get; }

    public short MinorVersion { get; }
}

public sealed class PortableShaderSampler
{
    public PortableShaderSampler(
        int registerIndex,
        object brush,
        PortableShaderSamplingMode samplingMode)
    {
        RegisterIndex = registerIndex;
        Brush = brush ?? throw new ArgumentNullException(nameof(brush));
        SamplingMode = samplingMode;
    }

    public int RegisterIndex { get; }

    public object Brush { get; }

    public PortableShaderSamplingMode SamplingMode { get; }
}

public sealed class PortableShaderEffect
{
    public PortableShaderEffect(
        string? effectTypeFullName,
        string? effectTypeName,
        PortablePixelShader? pixelShader,
        float[]? floatConstants,
        PortableShaderSampler[]? samplers,
        uint intConstantCount,
        uint boolConstantCount,
        double paddingTop,
        double paddingBottom,
        double paddingLeft,
        double paddingRight,
        int ddxUvDdyUvRegisterIndex)
    {
        EffectTypeFullName = string.IsNullOrWhiteSpace(effectTypeFullName) ? null : effectTypeFullName;
        EffectTypeName = string.IsNullOrWhiteSpace(effectTypeName) ? null : effectTypeName;
        PixelShader = pixelShader;
        FloatConstants = floatConstants is { Length: > 0 } ? (float[])floatConstants.Clone() : Array.Empty<float>();
        Samplers = samplers is { Length: > 0 } ? (PortableShaderSampler[])samplers.Clone() : Array.Empty<PortableShaderSampler>();
        IntConstantCount = intConstantCount;
        BoolConstantCount = boolConstantCount;
        PaddingTop = SanitizeNonNegative(paddingTop);
        PaddingBottom = SanitizeNonNegative(paddingBottom);
        PaddingLeft = SanitizeNonNegative(paddingLeft);
        PaddingRight = SanitizeNonNegative(paddingRight);
        DdxUvDdyUvRegisterIndex = ddxUvDdyUvRegisterIndex;
    }

    public string? EffectTypeFullName { get; }

    public string? EffectTypeName { get; }

    public PortablePixelShader? PixelShader { get; }

    public float[] FloatConstants { get; }

    public PortableShaderSampler[] Samplers { get; }

    public uint IntConstantCount { get; }

    public uint BoolConstantCount { get; }

    public double PaddingTop { get; }

    public double PaddingBottom { get; }

    public double PaddingLeft { get; }

    public double PaddingRight { get; }

    public int DdxUvDdyUvRegisterIndex { get; }

    public double MaxPadding
    {
        get
        {
            return Math.Max(
                Math.Max(PaddingTop, PaddingBottom),
                Math.Max(PaddingLeft, PaddingRight));
        }
    }

    private static double SanitizeNonNegative(double value)
    {
        return double.IsFinite(value) && value > 0.0 ? value : 0.0;
    }
}
