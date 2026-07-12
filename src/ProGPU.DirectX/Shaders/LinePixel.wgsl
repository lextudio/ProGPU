// Algorithm: Return interpolated line color.
// Time complexity: O(1) per fragment.
// Space complexity: O(1) local storage.
struct VertexOut {
    @builtin(position) position: vec4<f32>,
    @location(0) color: vec4<f32>,
};

@fragment
fn fs_main(input: VertexOut) -> @location(0) vec4<f32> {
    return input.color;
}
