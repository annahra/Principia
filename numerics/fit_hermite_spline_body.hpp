#pragma once

#include <list>
#include <type_traits>

#include "base/ranges.hpp"
#include "numerics/hermite3.hpp"

namespace principia {
namespace numerics {
namespace internal_fit_hermite_spline {

using base::Range;
using geometry::Normed;
using quantities::Derivative;

template<typename Samples,
         typename GetArgument,
         typename GetValue,
         typename GetDerivative,
         typename ErrorType>
std::list<typename Samples::const_iterator> FitHermiteSpline(
    Samples const& samples,
    GetArgument get_argument,
    GetValue get_value,
    GetDerivative get_derivative,
    ErrorType tolerance) {
  using Iterator = typename Samples::const_iterator;
  using Argument = std::remove_const_t<
      std::remove_reference_t<decltype(get_argument(*samples.begin()))>>;
  using Value = std::remove_const_t<
      std::remove_reference_t<decltype(get_value(*samples.begin()))>>;
  using Derivative1 = std::remove_const_t<
      std::remove_reference_t<decltype(get_derivative(*samples.begin()))>>;
  static_assert(
      std::is_same<Derivative1, Derivative<Value, Argument>>::value,
      "Inconsistent types for |get_argument|, |get_value|, and "
      "|get_derivative|");
  std::ptrdiff_t const size = samples.end() - samples.begin();
  auto interpolate_range =
      [get_argument, get_value, get_derivative](Iterator begin, Iterator end) {
        auto const last = --end;
        return Hermite3<Argument, Value>(
            {get_argument(*begin), get_argument(*last)},
            {get_value(*begin), get_value(*last)},
            {get_derivative(*begin), get_derivative(*last)});
      };
  static_assert(
      std::is_same<ErrorType, typename Normed<Value>::NormType>::value,
      "|tolerance| must have the same type as a distance between values");
  if (size < 3) {
    // With 0 or 1 points there is nothing to interpolate, with 2 we cannot
    // estimate the error.
    return {};
  }
  if (interpolate_range(samples.begin(), samples.end())
          .LInfinityError(Range(samples.begin(), samples.end()),
                          get_argument,
                          get_value) < tolerance) {
    // A single polynomial fits the entire range, so we have no way of knowing
    // whether it is the largest polynomial that will fit the range.
    return {};
  } else {
    auto lower = samples.begin();
    auto upper = samples.end();
    for (;;) {
      auto const middle = lower + (upper - lower) / 2;
      if (middle == lower) {
        break;
      }
      if (interpolate_range(lower, middle).LInfinityError(Range(lower, middle),
                                                          get_argument,
                                                          get_value) <
          tolerance) {
        lower = middle;
      } else {
        upper = middle;
      }
    }
    std::list<Iterator> tail = FitHermiteSpline(Range(lower, samples.end()),
                                                get_argument,
                                                get_value,
                                                get_derivative,
                                                tolerance);
    tail.push_front(lower);
    return tail;
  }
}

}  // namespace internal_fit_hermite_spline
}  // namespace numerics
}  // namespace principia
