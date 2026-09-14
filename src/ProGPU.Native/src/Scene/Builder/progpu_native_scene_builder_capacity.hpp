#pragma once

#include <algorithm>
#include <stdexcept>
#include <vector>

namespace progpu::native::scene_builder_detail {

// Keep allocation preflights before publishing related records, without making
// an append sequence quadratic by reserving exactly size() + additional.
template<class T, class Allocator>
void reserve_append(std::vector<T, Allocator>& values, std::size_t additional) {
    const auto maximum = values.max_size();
    if (additional > maximum - values.size()) {
        throw std::length_error("scene builder append exceeds vector capacity");
    }
    const auto required = values.size() + additional;
    const auto current = values.capacity();
    if (required <= current) return;
    const auto doubled = current > maximum / 2U ? maximum : current * 2U;
    const auto initial = std::min<std::size_t>(8U, maximum);
    values.reserve(std::max(required, std::max(initial, doubled)));
}

} // namespace progpu::native::scene_builder_detail
