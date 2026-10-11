#pragma once

#include "progpu_native_direct2d_source_coverage_reference.hpp"
#include "progpu_native_sheared_source_identity.hpp"
#include <algorithm>
#include <cmath>
#include <span>

namespace progpu::native::tests {

// An independent raw GPU observation from the same live device as the replay.
// Default/missing observations never authorize an exception.
struct source_unorm_observation final {
    float input{}, measured{}, shader_byte{};
    std::uint8_t straight{}, direct{}, premultiplied{}, quantized{};
    std::uint8_t half_observed{}, half_reference{};
    bool uniform{};
};

} // namespace progpu::native::tests

namespace progpu::native::direct2d::tests {

enum class sheared_pixel_match { rejected, exact, approved_conversion };

inline sheared_pixel_match compare_original_sheared_pixels(
    std::span<const std::byte> scene, bool half, bool reverse,
    std::span<const std::uint8_t> cold, std::span<const std::uint8_t> warm,
    const progpu::native::tests::source_unorm_observation& observed, bool eligible_platform)
{
    static const std::array originals{original_sheared_glyph_coverage_pixels(false),
        original_sheared_glyph_coverage_pixels(true)};
    const auto& original = originals[half ? 1U : 0U];
    if (cold.size() != original.size() || warm.size() != original.size() ||
        !std::equal(cold.begin(), cold.end(), warm.begin())) return sheared_pixel_match::rejected;
    if (std::equal(cold.begin(), cold.end(), original.begin())) return sheared_pixel_match::exact;
    if (!eligible_platform) return sheared_pixel_match::rejected;
    const auto source = original_sheared_source_scene(half, reverse);
    if (scene.size() != source.size() || !std::equal(scene.begin(), scene.end(), source.begin()))
        return sheared_pixel_match::rejected;

    // Owner-approved #363 scope: the original snapped coverage is exactly this
    // rational. This is not a general tolerance or an adapter-name allowlist.
    constexpr float coverage = 112867.0F / 113984.0F;
    constexpr double byte_value = 112867.0 / 113984.0 * 255.0;
    static_assert(byte_value - 252.0 < 0.6 && 253.0 - byte_value < 0.6);
    if (!observed.uniform || observed.input != coverage || observed.measured != coverage ||
        observed.shader_byte != 253.0F || observed.straight != 252U || observed.direct != 252U ||
        observed.premultiplied != 252U || observed.quantized != 253U ||
        (half && (observed.half_observed != 126U || observed.half_reference != 127U)))
        return sheared_pixel_match::rejected;

    constexpr std::size_t offset = (28U * 64U + 40U) * 4U;
    for (std::size_t i = 0U; i < original.size(); ++i) {
        const auto expected = i == offset ? (half ? 126U : 252U) : original[i];
        if (cold[i] != expected) return sheared_pixel_match::rejected;
    }
    return sheared_pixel_match::approved_conversion;
}

// Exercise the boundary independently of the device's actual conversion choice.
// Synthetic observations are confined to these rejection controls, never replay.
template<class Require>
void verify_sheared_unorm_exception_controls(Require require)
{
    using progpu::native::tests::source_unorm_observation;
    const source_unorm_observation diagnosed{112867.0F / 113984.0F, 112867.0F / 113984.0F,
        253.0F, 252U, 252U, 252U, 253U, 126U, 127U, true};
    constexpr std::size_t offset = (28U * 64U + 40U) * 4U;
    for (const bool half : {false, true}) for (const bool reverse : {false, true}) {
        const auto source = original_sheared_source_scene(half, reverse);
        const auto original = original_sheared_glyph_coverage_pixels(half);
        auto altered = original;
        altered[offset] = half ? 126U : 252U;
        const auto matches = [&](const auto& scene, const auto& cold, const auto& warm, const auto& proof) {
            return compare_original_sheared_pixels(scene, half, reverse, cold, warm, proof, true);
        };
        require(matches(source, original, original, source_unorm_observation{}) == sheared_pixel_match::exact,
            "unchanged original bytes require no conversion exception");
        require(matches(source, altered, altered, diagnosed) == sheared_pixel_match::approved_conversion,
            "the sole diagnosed conversion is within the owner-approved boundary");
        require(compare_original_sheared_pixels(source, half, reverse, altered, altered, diagnosed, false) ==
            sheared_pixel_match::rejected, "the exception is confined to Intel macOS");
        require(matches(source, altered, altered, source_unorm_observation{}) == sheared_pixel_match::rejected &&
            matches(source, altered, original, diagnosed) == sheared_pixel_match::rejected,
            "missing GPU evidence or a cold/warm change cannot inherit the exception");
        for (std::size_t field = 0U; field < 10U; ++field) {
            auto wrong = diagnosed;
            switch (field) {
            case 0: wrong.input = std::nextafter(wrong.input, 0.0F); break;
            case 1: wrong.measured = std::nextafter(wrong.measured, 0.0F); break;
            case 2: wrong.shader_byte = 252.0F; break;
            case 3: wrong.straight = 253U; break;
            case 4: wrong.direct = 253U; break;
            case 5: wrong.premultiplied = 253U; break;
            case 6: wrong.quantized = 252U; break;
            case 7: wrong.uniform = false; break;
            case 8: wrong.half_observed = 127U; break;
            case 9: wrong.half_reference = 126U; break;
            }
            if (!half && field >= 8U) continue;
            require(matches(source, altered, altered, wrong) == sheared_pixel_match::rejected,
                "changed conversion evidence must reject the exception");
        }
        auto changed_source = std::vector<std::byte>(source.begin(), source.end());
        for (std::size_t i = 0U; i < changed_source.size(); ++i) {
            changed_source[i] ^= std::byte{1};
            require(matches(changed_source, altered, altered, diagnosed) == sheared_pixel_match::rejected,
                "every original serialized source byte guards conversion admission");
            changed_source[i] ^= std::byte{1};
        }
        changed_source.push_back(std::byte{});
        require(matches(changed_source, altered, altered, diagnosed) == sheared_pixel_match::rejected,
            "changed source extent must reject the exception");
        for (std::size_t i = 0U; i < altered.size(); ++i) {
            if (i == offset) continue;
            altered[i] ^= 1U;
            require(matches(source, altered, altered, diagnosed) == sheared_pixel_match::rejected,
                "every other pixel and channel remains exact");
            altered[i] ^= 1U;
        }
        altered[offset] = half ? 125U : 251U;
        require(matches(source, altered, altered, diagnosed) == sheared_pixel_match::rejected,
            "a different value at the diagnosed coordinate is rejected");
        altered = original;
        altered[offset + 4U] ^= 1U;
        require(matches(source, altered, altered, diagnosed) == sheared_pixel_match::rejected,
            "a moved difference cannot inherit the exception");
        altered.pop_back();
        require(matches(source, altered, altered, diagnosed) == sheared_pixel_match::rejected,
            "changed frame extent cannot inherit the exception");
    }
}

} // namespace progpu::native::direct2d::tests
