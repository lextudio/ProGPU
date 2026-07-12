// Algorithm: Estimate local height derivatives, classify the nearest contour, and anti-alias its thresholded coverage.
// Time complexity: O(1) per fragment with a fixed neighboring-height footprint.
// Space complexity: O(1) local storage with three height reads and one mask sample.
struct ContourParams {
    zMin: f32,
    zMax: f32,
    zStep: f32,
    strokeThickness: f32,
    opacity: f32,
    heightTextureWidth: f32,
    heightTextureHeight: f32,
    _pad0: f32,
    color: vec4<f32>,
};

@group(0) @binding(512) var<uniform> Contour: ContourParams;
@group(0) @binding(576) var<storage, read> Heights: array<f32>;
@group(0) @binding(577) var ContourTexture: texture_2d<f32>;
@group(0) @binding(768) var SourceSampler: sampler;

struct VertexOut {
    @builtin(position) position: vec4<f32>,
    @location(0) uv: vec2<f32>,
};

fn load_height(coord: vec2<i32>, heightSize: vec2<i32>) -> f32 {
    let clampedCoord = clamp(coord, vec2<i32>(0, 0), heightSize - vec2<i32>(1, 1));
    let heightIndex = u32(clampedCoord.y * heightSize.x + clampedCoord.x);
    return Heights[heightIndex];
}

@fragment
fn fs_main(input: VertexOut) -> @location(0) vec4<f32> {
    let heightSize = vec2<i32>(i32(Contour.heightTextureWidth), i32(Contour.heightTextureHeight));
    let centerCoord = clamp(
        vec2<i32>(input.uv * vec2<f32>(heightSize)),
        vec2<i32>(0, 0),
        heightSize - vec2<i32>(1, 1));
    let height = load_height(centerCoord, heightSize);
    if (height < Contour.zMin || height > Contour.zMax) {
        discard;
    }

    let contourIndex = round((height - Contour.zMin) / Contour.zStep);
    let contourHeight = Contour.zMin + contourIndex * Contour.zStep;
    if (contourHeight < Contour.zMin || contourHeight > Contour.zMax) {
        discard;
    }

    let heightDx = abs(load_height(centerCoord + vec2<i32>(1, 0), heightSize) - height);
    let heightDy = abs(load_height(centerCoord + vec2<i32>(0, 1), heightSize) - height);
    let heightPerPixel = max(max(heightDx, heightDy), Contour.zStep * 0.001);
    let threshold = heightPerPixel * max(Contour.strokeThickness, 1.0);
    let distanceToContour = abs(height - contourHeight);
    var lineAlpha = 1.0 - smoothstep(threshold, threshold * 2.0, distanceToContour);
    let contourMask = textureSample(ContourTexture, SourceSampler, input.uv);
    lineAlpha = lineAlpha * clamp(Contour.opacity, 0.0, 1.0) * Contour.color.a * contourMask.a;
    if (lineAlpha <= 0.001) {
        discard;
    }

    return vec4<f32>(Contour.color.rgb, lineAlpha);
}
