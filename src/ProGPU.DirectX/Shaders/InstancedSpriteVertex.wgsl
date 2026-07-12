// Algorithm: Expand each sprite instance rectangle from a unit quad and forward UV/color attributes.
// Time complexity: O(1) per vertex.
// Space complexity: O(1) local storage with fixed instance reads.
struct VertexIn {
    @location(0) corner: vec2<f32>,
    @location(1) rect: vec4<f32>,
    @location(2) color: vec4<f32>,
};

struct VertexOut {
    @builtin(position) position: vec4<f32>,
    @location(0) uv: vec2<f32>,
    @location(1) color: vec4<f32>,
};

@vertex
fn vs_main(input: VertexIn) -> VertexOut {
    var output: VertexOut;
    let position = input.rect.xy + ((input.rect.zw - input.rect.xy) * input.corner);
    output.position = vec4<f32>(position, 0.0, 1.0);
    output.uv = input.corner;
    output.color = input.color;
    return output;
}
