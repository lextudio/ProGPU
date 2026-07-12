// Algorithm: Transform SciChart 3D vertices and forward normal/color attributes for lighting.
// Time complexity: O(1) per vertex.
// Space complexity: O(1) local storage.
struct Camera {
    worldViewProjection: mat4x4<f32>,
    lightDirection: vec4<f32>,
};

@group(0) @binding(0) var<uniform> CameraData: Camera;

struct VertexIn {
    @location(0) position: vec3<f32>,
    @location(1) normal: vec3<f32>,
    @location(2) color: vec4<f32>,
};

struct VertexOut {
    @builtin(position) position: vec4<f32>,
    @location(0) color: vec4<f32>,
    @location(1) normal: vec3<f32>,
};

@vertex
fn vs_main(input: VertexIn) -> VertexOut {
    var output: VertexOut;
    output.position = CameraData.worldViewProjection * vec4<f32>(input.position, 1.0);
    output.color = input.color;
    output.normal = input.normal;
    return output;
}
