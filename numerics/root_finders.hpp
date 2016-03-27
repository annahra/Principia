﻿
#pragma once

#include <set>

#include "quantities/quantities.hpp"

namespace principia {

using quantities::Exponentiation;
using quantities::Quotient;

namespace numerics {

// Approximates a root of |f| between |lower_bound| and |upper_bound| by
// bisection.  The result is less than one ULP from a root of any continuous
// function agreeing with |f| on the values of |Argument|.
// |f(lower_bound)| and |f(upper_bound)| must be nonzero and of opposite signs.
template<typename Argument, typename Function>
Argument Bisect(Function f,
                Argument const& lower_bound,
                Argument const& upper_bound);

// Returns the solutions of a quadratic equation.  The result may have 0, 1 or 2
// values.  |a2| is the coefficient of the 2nd degree term and similarly for the
// others.
template <typename Argument, typename Result>
std::set<Argument> SolveQuadraticEquation(
    Quotient<Result, Exponentiation<Argument, 2>> const& a2,
    Quotient<Result, Argument> const& a1,
    Result const& a0);

}  // namespace numerics
}  // namespace principia

#include "numerics/root_finders_body.hpp"
