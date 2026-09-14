namespace ProGPU.Backend.Native;

/// <summary>
/// Native engine-owned logical storage. Borrowed views, backend-retained released
/// resources, pipelines, allocator padding and driver residency are not owned
/// byte totals. Opaque texture formats are counted but explicitly unquantified.
/// </summary>
public partial struct NativeGpuMemorySnapshot
{
    public readonly bool HasCompleteTextureByteCount => UnquantifiedTextureCount == 0;
    public readonly ulong TotalKnownOwnedBytes => checked(OwnedBufferBytes + OwnedTextureBytes);
}
