#pragma once

#include <type_traits>

#include "ksp_plugin/plugin.hpp"

// DLL-exported functions for interfacing with Platform Invocation Services.

#if defined(CDECL)
#  error "CDECL already defined"
#else
// Architecture macros from http://goo.gl/ZypnO8.
// We use cdecl on x86, the calling convention is unambiguous on x86-64.
#  if defined(__i386) || defined(_M_IX86)
#    if defined(_MSC_VER) || defined(__clang__)
#      define CDECL __cdecl
#    elif defined(__GNUC__) || defined(__INTEL_COMPILER)
#      define CDECL __attribute__((cdecl))
#    else
#      error "Get a real compiler"
#    endif
#  elif defined(_M_X64) || defined(__x86_64__)
#    define CDECL
#  else
#    error "Have you tried a Cray-1?"
#  endif
#endif

#if defined(DLLEXPORT)
#  error "DLLEXPORT already defined"
#else
#  if defined(_WIN32) || defined(_WIN64)
#    define DLLEXPORT __declspec(dllexport)
#  else
#    define DLLEXPORT __attribute__((visibility("default")))
#  endif
#endif

namespace principia {
namespace ksp_plugin {

extern "C"
struct XYZ {
  double x, y, z;
};

static_assert(std::is_standard_layout<XYZ>::value,
              "XYZ is used for interfacing");

extern "C"
struct XYZSegment {
  XYZ begin, end;
};

static_assert(std::is_standard_layout<XYZSegment>::value,
              "XYZSegment is used for interfacing");

struct LineAndIterator {
  explicit LineAndIterator(RenderedTrajectory<World> const& rendered_trajectory)
      : rendered_trajectory(rendered_trajectory) {}
  RenderedTrajectory<World> const rendered_trajectory;
  RenderedTrajectory<World>::const_iterator it;
};

// Sets stderr to log INFO, and redirects stderr, which Unity does not log, to
// "<KSP directory>/stderr.log".  This provides an easily accessible file
// containing a sufficiently verbose log of the latest session, instead of
// requiring users to dig in the archive of all past logs at all severities.
// This archive is written to
// "<KSP directory>/glog/Principia/<SEVERITY>.<date>-<time>.<pid>",
// where date and time are in ISO 8601 basic format.
extern "C" DLLEXPORT
void CDECL InitGoogleLogging();

// Exports |LOG(SEVERITY) << message| for fast logging from the C# adapter.
// This will always evaluate its argument even if the corresponding log severity
// is disabled, so it is less efficient than LOG(INFO).  It will not report the
// line and file of the caller.
extern "C" DLLEXPORT
void CDECL LogInfo(char const* message);
extern "C" DLLEXPORT
void CDECL LogWarning(char const* message);
extern "C" DLLEXPORT
void CDECL LogError(char const* message);
extern "C" DLLEXPORT
void CDECL LogFatal(char const* message);

// Returns a pointer to a plugin constructed with the arguments given.
// The caller takes ownership of the result.
extern "C" DLLEXPORT
Plugin* CDECL NewPlugin(double const initial_time,
                        int const sun_index,
                        double const sun_gravitational_parameter,
                        double const planetarium_rotation_in_degrees);

// Deletes and nulls |*plugin|.
// |plugin| must not be null.  No transfer of ownership of |*plugin|, takes
// ownership of |**plugin|.
extern "C" DLLEXPORT
void CDECL DeletePlugin(Plugin const** const plugin);

// Calls |plugin->InsertCelestial| with the arguments given.
// |plugin| must not be null.  No transfer of ownership.
extern "C" DLLEXPORT
void CDECL InsertCelestial(Plugin* const plugin,
                           int const celestial_index,
                           double const gravitational_parameter,
                           int const parent_index,
                           XYZ const from_parent_position,
                           XYZ const from_parent_velocity);

// Calls |plugin->UpdateCelestialHierarchy| with the arguments given.
// |plugin| must not be null.  No transfer of ownership.
extern "C" DLLEXPORT
void CDECL UpdateCelestialHierarchy(Plugin const* const plugin,
                                    int const celestial_index,
                                    int const parent_index);

// Calls |plugin->EndInitialization|.
// |plugin| must not be null.  No transfer of ownership.
extern "C" DLLEXPORT
void CDECL EndInitialization(Plugin* const plugin);

// Calls |plugin->InsertOrKeepVessel| with the arguments given.
// |plugin| must not be null.  No transfer of ownership.
extern "C" DLLEXPORT
bool CDECL InsertOrKeepVessel(Plugin* const plugin,
                              char const* vessel_guid,
                              int const parent_index);

// Calls |plugin->SetVesselStateOffset| with the arguments given.
// |plugin| must not be null.  No transfer of ownership.
extern "C" DLLEXPORT
void CDECL SetVesselStateOffset(Plugin* const plugin,
                                char const* vessel_guid,
                                XYZ const from_parent_position,
                                XYZ const from_parent_velocity);

extern "C" DLLEXPORT
void CDECL AdvanceTime(Plugin* const plugin,
                       double const t,
                       double const planetarium_rotation);

// Calls |plugin->VesselDisplacementFromParent| with the arguments given.
// |plugin| must not be null.  No transfer of ownership.
extern "C" DLLEXPORT
XYZ CDECL VesselDisplacementFromParent(Plugin const* const plugin,
                                       char const* vessel_guid);

// Calls |plugin->VesselParentRelativeVelocity| with the arguments given.
// |plugin| must not be null.  No transfer of ownership.
extern "C" DLLEXPORT
XYZ CDECL VesselParentRelativeVelocity(Plugin const* const plugin,
                                       char const* vessel_guid);

// Calls |plugin->CelestialDisplacementFromParent| with the arguments given.
// |plugin| must not be null.  No transfer of ownership.
extern "C" DLLEXPORT
XYZ CDECL CelestialDisplacementFromParent(Plugin const* const plugin,
                                          int const celestial_index);

// Calls |plugin->CelestialParentRelativeVelocity| with the arguments given.
// |plugin| must not be null.  No transfer of ownership.
extern "C" DLLEXPORT
XYZ CDECL CelestialParentRelativeVelocity(Plugin const* const plugin,
                                          int const celestial_index);

// Calls |plugin->NewBodyCentredNonRotatingFrame| with the arguments given.
// |plugin| must not be null.  No transfer of ownership.
extern "C" DLLEXPORT
BodyCentredNonRotatingFrame const* CDECL NewBodyCentredNonRotatingFrame(
    Plugin const* const plugin,
    int const reference_body_index);

// Calls |plugin->NewBarycentricRotatingFrame| with the arguments given.
// |plugin| must not be null.  No transfer of ownership.
extern "C" DLLEXPORT
BarycentricRotatingFrame const* CDECL NewBarycentricRotatingFrame(
    Plugin const* const plugin,
    int const primary_index,
    int const secondary_index);

// Deletes and nulls |*frame|.
// |frame| must not be null.  No transfer of ownership of |*frame|, takes
// ownership of |**frame|.
extern "C" DLLEXPORT
void CDECL DeleteRenderingFrame(RenderingFrame const** const frame);

// Returns the result of |plugin->RenderedVesselTrajectory| called with the
// arguments given, together with an iterator to its beginning.
// |plugin| must not be null.  No transfer of ownership of |plugin|.  The caller
// gets ownership of the result.  |frame| must not be null.  No transfer of
// ownership of |frame|.
extern "C" DLLEXPORT
LineAndIterator* CDECL RenderedVesselTrajectory(Plugin const* const plugin,
                                                char const* vessel_guid,
                                                RenderingFrame const* frame,
                                                XYZ const sun_world_position);

// Returns |line_and_iterator->rendered_trajectory.size()|.
// |line_and_iterator| must not be null.  No transfer of ownership.
extern "C" DLLEXPORT
int CDECL NumberOfSegments(LineAndIterator const* line_and_iterator);

// Returns the |XYZSegment| corresponding to the |LineSegment|
// |*line_and_iterator->it|, then increments |line_and_iterator->it|.
// |line_and_iterator| must not be null.  |line_and_iterator->it| must not be
// the end of |line_and_iterator->rendered_trajectory|.  No transfer of
// ownership.
extern "C" DLLEXPORT
XYZSegment CDECL FetchAndIncrement(LineAndIterator* const line_and_iterator);

// Returns |true| if and only if |line_and_iterator->it| is the end of
// |line_and_iterator->rendered_trajectory|.
// |line_and_iterator| must not be null.  No transfer of ownership.
extern "C" DLLEXPORT
bool CDECL AtEnd(LineAndIterator* const line_and_iterator);

// Deletes and nulls |*line_and_iterator|.
// |line_and_iterator| must not be null.  No transfer of ownership of
// |*line_and_iterator|, takes ownership of |**line_and_iterator|.
extern "C" DLLEXPORT
void CDECL DeleteLineAndIterator(
    LineAndIterator const** const line_and_iterator);

// Says hello, convenient for checking that calls to the DLL work.
extern "C" DLLEXPORT
char const* CDECL SayHello();

}  // namespace ksp_plugin
}  // namespace principia

#undef CDECL
#undef DLLEXPORT