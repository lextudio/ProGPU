// Algorithm: Evaluate ambient plus Lambert diffuse lighting from the interpolated normal.
// Time complexity: O(1) per fragment.
// Space complexity: O(1) local storage.
struct VertexOut {
    @builtin(position) position: vec4<f32>,
    @location(0) color: vec4<f32>,
    @location(1) normal: vec3<f32>,
};

struct Camera {
    worldViewProjection: mat4x4<f32>,
    lightDirection: vec4<f32>,
};

@group(0) @binding(0) var<uniform> CameraData: Camera;

@fragment
fn fs_main(input: VertexOut) -> @location(0) vec4<f32> {
    let normal = normalize(input.normal);
    let light = normalize(CameraData.lightDirection.xyz);
    let diffuse = max(dot(normal, light), 0.0);
    let shaded = input.color.rgb * (0.25 + diffuse * 0.75);
    return vec4<f32>(shaded, input.color.a);
}
