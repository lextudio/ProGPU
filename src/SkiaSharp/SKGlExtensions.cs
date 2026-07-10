namespace SkiaSharp;

public static class SKGlExtensions
{
    public static uint ToGlSizedFormat(this SKColorType colorType)
    {
        return colorType switch
        {
            SKColorType.Bgra8888 => 0x93A1,
            SKColorType.Rgb565 => 0x8D62,
            SKColorType.RgbaF16 => 0x881A,
            _ => 0x8058
        };
    }
}
