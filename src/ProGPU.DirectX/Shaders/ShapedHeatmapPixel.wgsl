// Algorithm: Map a height-buffer sample through a gradient texture and apply the interpolated shape color.
// Time complexity: O(1) per fragment.
// Space complexity: O(1) local storage with one height read and one texture sample.
struct HeatmapParams {
    colorMapMin: f32,
    colorMapInvRange: f32,
    heightTextureWidth: f32,
    heightTextureHeight: f32,
};

@group(0) @binding(512) var<uniform> Heatmap: HeatmapParams;
@group(0) @binding(576) var<storage, read> Heights: array<f32>;
@group(0) @binding(577) var GradientTexture: texture_2d<f32>;
@group(0) @binding(768) var SourceSampler: sampler;

struct VertexOut {
    @builtin(position) position: vec4<f32>,
    @location(0) uv: vec2<f32>,
    @location(1) color: vec4<f32>,
};

@fragment
fn fs_main(input: VertexOut) -> @location(0) vec4<f32> {
    let heightSize = vec2<i32>(i32(Heatmap.heightTextureWidth), i32(Heatmap.heightTextureHeight));
    let heightCoord = clamp(
        vec2<i32>(input.uv * vec2<f32>(heightSize)),
        vec2<i32>(0, 0),
        heightSize - vec2<i32>(1, 1));
    let heightIndex = u32(heightCoord.y * heightSize.x + heightCoord.x);
    let height = Heights[heightIndex];
    let gradientU = clamp((height - Heatmap.colorMapMin) * Heatmap.colorMapInvRange, 0.0, 1.0);
    return textureSample(GradientTexture, SourceSampler, vec2<f32>(gradientU, 0.5)) * input.color;
}
