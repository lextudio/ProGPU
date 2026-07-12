// Algorithm: Sample a textured primitive with the DirectX pixel-stage SRV and sampler bindings.
// Time complexity: O(1) per fragment.
// Space complexity: O(1) local storage with one texture sample.
@group(0) @binding(576) var SourceTexture: texture_2d<f32>;
@group(0) @binding(768) var SourceSampler: sampler;

struct VertexOut {
    @builtin(position) position: vec4<f32>,
    @location(0) uv: vec2<f32>,
};

@fragment
fn fs_main(input: VertexOut) -> @location(0) vec4<f32> {
    return textureSample(SourceTexture, SourceSampler, input.uv);
}
