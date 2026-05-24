using ProGPU.Text;

namespace ProGPU.Scene;

public interface ITextLayoutProvider
{
    TtfFont? Font { get; }
    TextLayout? GetOrUpdateLayout(GlyphAtlas atlas);
}
