#pragma once

#include <algorithm>
#include <cstdint>
#include <memory>
#include <vector>

namespace progpu::native {

// O(1) amortized publication, O(B) periodic retirement for B live batches.
// Active recording leases cannot retire, including across split submissions.
template<class Resource>
class submission_resource_retention final {
public:
    struct batch {
        Resource resources;
        std::uint64_t required_completion = 0U;
        bool recording = true;
    };

    batch& begin() {
        auto value = std::make_unique<batch>();
        auto& result = *value;
        batches_.push_back(std::move(value));
        return result;
    }

    void seal(batch& value, std::uint64_t required_completion) noexcept {
        value.required_completion = required_completion;
        value.recording = false;
    }

    void cancel(batch& value) noexcept {
        std::erase_if(batches_, [&value](const auto& item) {
            return item.get() == &value;
        });
    }

    void retire(std::uint64_t completed) noexcept {
        std::erase_if(batches_, [completed](const auto& item) {
            return !item->recording && item->required_completion <= completed;
        });
    }

    void clear() noexcept { batches_.clear(); }
    [[nodiscard]] std::size_t size() const noexcept { return batches_.size(); }

private:
    std::vector<std::unique_ptr<batch>> batches_;
};

} // namespace progpu::native
