// Algorithm: Retained painter-ordered sprite quads, with optional nine-slice piecewise coordinate remapping and periodic center/edge sampling; texels premultiply before bilinear interpolation.
// Time complexity: O(N + F), six vertices per sprite N and four texture loads per covered fragment F. Nine-slice adds fixed scalar branches/modulo per axis, never loops over repeated tiles. Adjacent image runs share a draw without reordering.
// Space complexity: 80-byte uniform per batch, 48-byte storage record per sprite (at most 65,536 per batch), four RGBA texels plus two vec4 axis mappings and bounded scalar/vector temporaries per fragment; shared RGBA8 images are capped at 32 MiB across eight batches.
// Coordinates are logical pixels; source rectangles/borders are integral. Nine-slice extents are at most 65,536 source pixels to bound f32 local-coordinate precision.
// The compositor supplies projection, rectangular scissoring and target MSAA. Source-over uses premultiplied output.
// Ordinary sprites clamp to their frame. Nine-slice corners clamp within their patch, middle axes wrap inside the center strip; no cross-patch or cross-frame texture taps.
// Nine-slice keeps border sizes in source pixels and repeats partial final tiles. Destinations must fit both borders. No mipmap or perspective minification contract.
struct Frame { matrix: mat4x4<f32>, opacity: vec4<f32> };
struct Sprite { destination: vec4<f32>, source: vec4<f32>, nine_slice: vec4<f32> };
@group(0) @binding(0) var<uniform> frame: Frame;
@group(0) @binding(1) var<storage, read> sprites: array<Sprite>;
@group(1) @binding(0) var artwork: texture_2d<f32>;
struct Varying {
    @builtin(position) position: vec4<f32>,
    @location(0) pixel: vec2<f32>,
    @location(1) @interpolate(flat) source: vec4<f32>,
    @location(2) @interpolate(flat) sizing: vec4<f32>
};
@vertex fn vs_main(@builtin(vertex_index) vertex: u32, @builtin(instance_index) instance: u32) -> Varying {
    var corners = array<vec2<f32>,6>(vec2(0.,0.),vec2(1.,0.),vec2(1.,1.),vec2(0.,0.),vec2(1.,1.),vec2(0.,1.));
    let uv = corners[vertex]; let sprite = sprites[instance];
    var result: Varying;
    result.position = frame.matrix * vec4(sprite.destination.xy + uv * sprite.destination.zw, 0., 1.);
    result.source = sprite.source; result.sizing = vec4(0.);
    result.pixel = sprite.source.xy + uv * sprite.source.zw - .5;
    if sprite.nine_slice.x > 0. {
        let extent = sprite.destination.zw / sprite.nine_slice.zw;
        result.sizing = vec4(sprite.nine_slice.xy, extent);
        result.pixel = uv * extent;
    }
    return result;
}
// Return source coordinate, inclusive patch lower/upper texels and periodic flag.
fn nine_axis(position: f32, total: f32, extent: f32, border: f32, origin: f32) -> vec4<f32> {
    if position < border { return vec4(origin + position - .5, origin, origin + border - 1., 0.); }
    if position >= total - border {
        return vec4(origin + extent - (total - position) - .5, origin + extent - border, origin + extent - 1., 0.);
    }
    let period = extent - 2. * border;
    let local = position - border;
    return vec4(origin + border + local - floor(local / period) * period - .5,
        origin + border, origin + extent - border - 1., 1.);
}
fn patch_texel(point: i32, axis: vec4<f32>) -> i32 {
    let lo = i32(axis.y); let hi = i32(axis.z);
    if axis.w == 0. { return clamp(point, lo, hi); }
    let count = hi - lo + 1;
    // Bilinear taps can lie one texel below the patch; use positive modulo.
    return lo + ((point - lo) % count + count) % count;
}
fn texel(point: vec2<i32>, x: vec4<f32>, y: vec4<f32>) -> vec4<f32> {
    let color = textureLoad(artwork, vec2(patch_texel(point.x, x), patch_texel(point.y, y)), 0);
    return vec4(color.rgb * color.a, color.a);
}
@fragment fn fs_main(input: Varying) -> @location(0) vec4<f32> {
    var x = vec4(input.pixel.x, input.source.x, input.source.x + input.source.z - 1., 0.);
    var y = vec4(input.pixel.y, input.source.y, input.source.y + input.source.w - 1., 0.);
    if input.sizing.x > 0. {
        x = nine_axis(input.pixel.x, input.sizing.z, input.source.z, input.sizing.x, input.source.x);
        y = nine_axis(input.pixel.y, input.sizing.w, input.source.w, input.sizing.y, input.source.y);
    }
    let pixel = vec2(x.x, y.x); let base = vec2<i32>(floor(pixel)); let phase = fract(pixel);
    let upper = mix(texel(base, x, y), texel(base + vec2(1,0), x, y), phase.x);
    let lower = mix(texel(base + vec2(0,1), x, y), texel(base + vec2(1,1), x, y), phase.x);
    return mix(upper, lower, phase.y) * frame.opacity.x;
}
