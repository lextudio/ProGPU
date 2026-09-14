namespace ProGPU.GameEngine.Assets;

/// <summary>Package-relative immutable content. Reads may decompress and allocate; call during preparation, never drawing.</summary>
public interface IAssetSource
{
    IEnumerable<string> Paths { get; }
    bool TryGet(string path, out ReadOnlyMemory<byte> bytes);
    ReadOnlyMemory<byte> Get(string path);
}
