﻿#pragma once

#include "physics/kepler_orbit.hpp"

#include "geometry/rotation.hpp"
#include "numerics/root_finders.hpp"
#include "quantities/elementary_functions.hpp"

namespace principia {

using geometry::Bivector;
using numerics::Bisect;
using quantities::Pow;
using quantities::Sqrt;
using quantities::Time;

namespace physics {

template<typename Frame>
KeplerOrbit<Frame>::KeplerOrbit(
    MassiveBody const& primary,
    Body const& secondary,
    Instant const& epoch,
    KeplerianElements<Frame> const& elements_at_epoch)
    : primary_gravitational_parameter_(
          primary.gravitational_parameter()),
      secondary_gravitational_parameter_(
          secondary.is_massless()
              ? GravitationalParameter{}
              : dynamic_cast<MassiveBody const&>(secondary).
                    gravitational_parameter()),
      elements_at_epoch_(elements_at_epoch),
      epoch_(epoch) {}

// TODO(egg): the calculations reducing the primocentric and barycentric state
// vectors to the case of a test particle should be documented in a separate
// file.

template<typename Frame>
RelativeDegreesOfFreedom<Frame>
KeplerOrbit<Frame>::PrimocentricStateVectors(Instant const& t) const {
  KeplerianElements<Frame> primocentric_elements = elements_at_epoch_;

  // Update the mean anomaly for |t|.
  Length const a = primocentric_elements.semimajor_axis;
  GravitationalParameter const μ = primary_gravitational_parameter_ +
                                   secondary_gravitational_parameter_;
  AngularFrequency const mean_motion = Sqrt(μ / Pow<3>(a)) * Radian;
  primocentric_elements.mean_anomaly = elements_at_epoch_.mean_anomaly +
                                       mean_motion * (t - epoch_);

  return TestParticleStateVectors(primocentric_elements, μ);
}

template<typename Frame>
RelativeDegreesOfFreedom<Frame>
KeplerOrbit<Frame>::BarycentricStateVectors(Instant const& t) const {
  KeplerianElements<Frame> barycentric_elements = elements_at_epoch_;

  // Change the semimajor axis to get elements describing the orbit of the
  // secondary around the barycentre, rather than around the primary.
  Length const a_primocentric = barycentric_elements.semimajor_axis;
  GravitationalParameter const μ1 = primary_gravitational_parameter_;
  GravitationalParameter const μ2 = secondary_gravitational_parameter_;
  barycentric_elements.semimajor_axis = a_primocentric * μ1 / (μ1 + μ2);
  Length const a = barycentric_elements.semimajor_axis;

  // μ is such that the mean motion (and thus the period) is the same as for the
  // primocentric orbit, μ/a^3 = (μ1 + μ2)/(a_primocentric)^3.
  GravitationalParameter const μ = Pow<3>(μ1) / Pow<2>(μ1 + μ2);

  // Update the mean anomaly for |t|.
  AngularFrequency const mean_motion = Sqrt(μ / Pow<3>(a)) * Radian;
  barycentric_elements.mean_anomaly = elements_at_epoch_.mean_anomaly +
                                      mean_motion * (t - epoch_);

  return TestParticleStateVectors(barycentric_elements, μ);
}

template<typename Frame>
RelativeDegreesOfFreedom<Frame>
KeplerOrbit<Frame>::TestParticleStateVectors(
    KeplerianElements<Frame> const& elements,
    GravitationalParameter const& gravitational_parameter) {
  GravitationalParameter const μ = gravitational_parameter;
  double const eccentricity = elements.eccentricity;
  Length const a = elements.semimajor_axis;
  Angle const i = elements.inclination;
  Angle const Ω = elements.longitude_of_ascending_node;
  Angle const ω = elements.argument_of_periapsis;
  Angle const mean_anomaly = elements.mean_anomaly;
  if (eccentricity < 1) {
    // Elliptic case.
    auto const kepler_equation =
        [eccentricity, mean_anomaly](Angle const& eccentric_anomaly) -> Angle {
          return mean_anomaly -
                     (eccentric_anomaly -
                      eccentricity * Sin(eccentric_anomaly) * Radian);
        };
    Angle const eccentric_anomaly =
        eccentricity == 0
            ? mean_anomaly
            : Bisect(kepler_equation,
                     mean_anomaly - eccentricity * Radian,
                     mean_anomaly + eccentricity * Radian);
    Angle const true_anomaly =
       2 * ArcTan(Sqrt(1 + eccentricity) * Sin(eccentric_anomaly / 2),
                  Sqrt(1 - eccentricity) * Cos(eccentric_anomaly / 2));
    Bivector<double, Frame> const x({1, 0, 0});
    Bivector<double, Frame> const y({0, 1, 0});
    Bivector<double, Frame> const z({0, 0, 1});
    // It would be nice to have a local frame, rather than make this a rotation
    // Frame -> Frame.
    // TODO(egg): Constructor for |Rotation| using Euler angles.
    Rotation<Frame, Frame> const from_orbit_plane =
        (Rotation<Frame, Frame>(Ω, z) *
         Rotation<Frame, Frame>(i, x) *
         Rotation<Frame, Frame>(ω, z));
    Length const distance = a * (1 - eccentricity * Cos(eccentric_anomaly));
    Displacement<Frame> const r =
        distance * from_orbit_plane(Vector<double, Frame>({Cos(true_anomaly),
                                                           Sin(true_anomaly),
                                                           0}));
    Velocity<Frame> const v =
        Sqrt(μ * a) / distance *
        from_orbit_plane(Vector<double, Frame>(
            {-Sin(eccentric_anomaly),
             Sqrt(1 - Pow<2>(eccentricity)) * Cos(eccentric_anomaly),
             0}));
    return {r, v};
  } else if (eccentricity == 1) {
    // Parabolic case.
    LOG(FATAL) << "not yet implemented";
    base::noreturn();
  } else {
    // Hyperbolic case.
    LOG(FATAL) << "not yet implemented";
    base::noreturn();
  }
}

}  // namespace physics
}  // namespace principia
