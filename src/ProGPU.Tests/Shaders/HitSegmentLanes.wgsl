// Algorithm: Compare four production segment lanes with ProGPU's pre-batch scalar predicate.
// Time complexity: O(N) for N independent segment-pair batches, one invocation per batch.
// Space complexity: O(N) input/output, O(1) private state per invocation.
// Reference provenance: ProGPU GpuHitTesting.wgsl at a1409ae8, segments_intersect.
struct SegmentLaneCase {
    a: vec2<f32>, b: vec2<f32>,
    cx: vec4<f32>, cy: vec4<f32>, dx: vec4<f32>, dy: vec4<f32>,
    comparison: vec4<u32>,
};
@group(0) @binding(6) var<storage, read_write> lane_cases: array<SegmentLaneCase>;

fn reference_segments_intersect(a: vec2<f32>, b: vec2<f32>, c: vec2<f32>, d: vec2<f32>) -> bool {
    let ab = b - a;
    let cd = d - c;
    let denominator = cross2(ab, cd);
    let ca = c - a;
    if (abs(denominator) <= 0.000001) {
        if (abs(cross2(ca, ab)) > 0.0001) { return false; }
        return intersects_bounds(min(a, b), max(a, b), min(c, d), max(c, d));
    }
    let t = cross2(ca, cd) / denominator;
    let u = cross2(ca, ab) / denominator;
    return t >= 0.0 && t <= 1.0 && u >= 0.0 && u <= 1.0;
}

@compute @workgroup_size(1)
fn compare_segment_lanes(@builtin(global_invocation_id) id: vec3<u32>) {
    let value = lane_cases[id.x];
    let actual = segments_intersect4(value.a, value.b, value.cx, value.cy, value.dx, value.dy);
    var expected: vec4<bool>;
    for (var lane = 0u; lane < 4u; lane++) {
        expected[lane] = reference_segments_intersect(value.a, value.b,
            vec2<f32>(value.cx[lane], value.cy[lane]), vec2<f32>(value.dx[lane], value.dy[lane]));
    }
    lane_cases[id.x].comparison = select(vec4<u32>(0u), vec4<u32>(1u), actual) +
        select(vec4<u32>(0u), vec4<u32>(2u), expected);
}
