namespace ProGPU.Backend;

public enum GpuHitTestExecutionPreference
{
    Automatic,
    SinglePass,
    OrderedStages
}

/// <summary>Actual device limits. A zero/default snapshot does not admit staged queries.</summary>
public readonly record struct WgpuComputeLimits(
    ulong MaxStorageBufferBindingSize,
    uint MaxStorageBuffersPerShaderStage,
    uint MaxComputeInvocationsPerWorkgroup,
    uint MaxComputeWorkgroupSizeX,
    uint MaxComputeWorkgroupsPerDimension)
{
    public bool AdmitsOrderedHitQuery(uint referenceCount, ulong maxBufferSize)
    {
        ulong bytes = 32UL + 8UL * referenceCount;
        return referenceCount != 0 && referenceCount != uint.MaxValue &&
            bytes <= uint.MaxValue && bytes <= maxBufferSize &&
            bytes <= MaxStorageBufferBindingSize && MaxStorageBuffersPerShaderStage >= 7 &&
            MaxComputeInvocationsPerWorkgroup >= 64 && MaxComputeWorkgroupSizeX >= 64 &&
            (referenceCount + 63UL) / 64UL <= MaxComputeWorkgroupsPerDimension;
    }
}

public static class GpuHitTestExecutionPolicy
{
    public static GpuHitTestExecutionPreference ReadEnvironmentPreference() =>
        Environment.GetEnvironmentVariable("PROGPU_HIT_TEST_EXECUTION")?.Trim().ToLowerInvariant() switch
        {
            null or "" or "auto" => GpuHitTestExecutionPreference.Automatic,
            "single-pass" => GpuHitTestExecutionPreference.SinglePass,
            "ordered-stages" => GpuHitTestExecutionPreference.OrderedStages,
            _ => throw new ArgumentException("PROGPU_HIT_TEST_EXECUTION must be auto, single-pass or ordered-stages.")
        };

    // Automatic remains on the qualified existing path until the full staged
    // application/package and device-limit gates close. Never silently fall back.
    public static GpuHitTestExecutionPreference Resolve(GpuHitTestExecutionPreference preference) => preference switch
    {
        GpuHitTestExecutionPreference.Automatic or GpuHitTestExecutionPreference.SinglePass => GpuHitTestExecutionPreference.SinglePass,
        GpuHitTestExecutionPreference.OrderedStages => GpuHitTestExecutionPreference.OrderedStages,
        _ => throw new ArgumentOutOfRangeException(nameof(preference))
    };
}
