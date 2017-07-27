﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace principia {
namespace ksp_plugin_adapter {

internal static class GLLines {
  public enum Style {
    SOLID,
    DASHED,
    FADED,
  }

  public static void Draw(Action line_vertices) {
    try {
      UnityEngine.GL.PushMatrix();
      line_material.SetPass(0);
      UnityEngine.GL.LoadPixelMatrix();
      UnityEngine.GL.Begin(UnityEngine.GL.LINES);
      rendering_lines_ = true;

      Vector3d camera = ScaledSpace.ScaledToLocalSpace(
          PlanetariumCamera.Camera.transform.position);
      // Only consider bodies with an angular radius greater than arcsin 1e-3.
      // From the Earth, this would consider the Moon and the Sun, but ignore
      // Jupiter.  The map view camera is wide-angle, so this is probably
      // overkill.
      // In any case we just want to do that in native code reasonably soon, so
      // this does the trick for now.
      hiding_bodies_ =
        (from body in FlightGlobals.Bodies
         where body.Radius * body.Radius >
               (body.position - camera).sqrMagnitude * 1e-6
         select body).ToArray();

      line_vertices();

      hiding_bodies_ = null;

      rendering_lines_ = false;
      UnityEngine.GL.End();
      UnityEngine.GL.PopMatrix();
    } catch (Exception e) {
      Log.Fatal("Exception while drawing lines: " + e.ToString());
    }
  }

  private static bool IsHidden(Vector3d point) {
    Vector3d camera = ScaledSpace.ScaledToLocalSpace(
        PlanetariumCamera.Camera.transform.position);
    foreach (CelestialBody body in hiding_bodies_) {
      Vector3d camera_to_point = point - camera;
      Vector3d camera_to_body = body.position - camera;
      double inner_product = Vector3d.Dot(camera_to_point, camera_to_body);
      double r_squared = body.Radius * body.Radius;
      // The projections on the camera-body axis of |point| and of the horizon
      // have lengths |inner_product| / d and d - r^2/d, where d is the distance
      // between the camera and the body and r is the body's radius, thus if
      // |inner_product| < d^2 - r^2, |point| is above the plane passing
      // through the horizon.
      // Otherwise, we check whether |point| is within the cone hidden from the
      // camera, by comparing the squared cosines multiplied by
      // d^2|camera_to_point|^2.
      // In addition, we check whether we're inside the body (this covers the
      // cap above the horizon plane and below the surface of the body, which
      // would otherwise be displayed).
      double d_squared_minus_r_squared =
          camera_to_body.sqrMagnitude - r_squared;
      if ((body.position - point).sqrMagnitude < r_squared ||
          (inner_product > d_squared_minus_r_squared &&
           inner_product * inner_product >
               camera_to_point.sqrMagnitude * d_squared_minus_r_squared)) {
        return true;
      }
    }
    return false;
  }

  public static void AddSegment(Vector3d world_begin,
                                Vector3d world_end,
                                bool hide_behind_bodies,
                                bool trace) {
    if (!rendering_lines_) {
      Log.Fatal("|AddSegment| outside of |DrawLines|");
    }
    if (hide_behind_bodies && (IsHidden(world_begin) || IsHidden(world_end))) {
      return;
    }
    var begin = WorldToMapScreen(world_begin);
    var end = WorldToMapScreen(world_end);
    if (begin.z > 0 && end.z > 0) {
      if (trace) { Log.Info("O:" + end.x + " " + end.y); }
      UnityEngine.GL.Vertex3(begin.x, begin.y, 0);
      UnityEngine.GL.Vertex3(end.x, end.y, 0);
    }
  }

  public static void RenderAndDeleteTrajectory(IntPtr trajectory_iterator,
                                               UnityEngine.Color colour,
                                               Style style) {
        bool trace = colour == XKCDColors.AcidGreen;
    try {
      Vector3d? previous_point = null;

      UnityEngine.GL.Color(colour);
      int size = trajectory_iterator.IteratorSize();

      for (int i = 0;
           !trajectory_iterator.IteratorAtEnd();
           trajectory_iterator.IteratorIncrement(), ++i) {
        Vector3d current_point =
            (Vector3d)trajectory_iterator.IteratorGetDiscreteTrajectoryXYZ();
        if (previous_point.HasValue) {
          if (style == Style.FADED) {
            colour.a = (float)(4 * i + size) / (float)(5 * size);
            UnityEngine.GL.Color(colour);
          }
          if (style != Style.DASHED || i % 2 == 1) {
            AddSegment(previous_point.Value,
                       current_point,
                       hide_behind_bodies : true,
                       trace:trace);
          }
        }
        previous_point = current_point;
      }
    } finally {
      Interface.IteratorDelete(ref trajectory_iterator);
    }
  }

  public static IntPtr NewPlanetarium(IntPtr plugin,
                                       XYZ sun_world_position) {
    UnityEngine.Camera camera = PlanetariumCamera.Camera;
    UnityEngine.Vector3 opengl_camera_x_in_world =
        camera.cameraToWorldMatrix.MultiplyVector(
            new UnityEngine.Vector3(1, 0, 0));
    UnityEngine.Vector3 opengl_camera_y_in_world =
        camera.cameraToWorldMatrix.MultiplyVector(
            new UnityEngine.Vector3(0, 1, 0));
    UnityEngine.Vector3 opengl_camera_z_in_world =
        camera.cameraToWorldMatrix.MultiplyVector(
            new UnityEngine.Vector3(0, 0, 1));
    UnityEngine.Vector3 camera_position_in_world =
        ScaledSpace.ScaledToLocalSpace(camera.transform.position);
    Log.Info("sw:"+sun_world_position.x+" "+sun_world_position.y+" "+sun_world_position.z);

    // According to
    // https://docs.unity3d.com/ScriptReference/Camera-projectionMatrix.html,
    // the on-centre projection matrix has the form:
    //   n / w                0                0                0
    //     0                n / h              0                0
    //     0                  0        (n + f) / (n - f)  2 f n / (n - f)
    //     0                  0               -1                0
    // where n and f are the near- and far-clipping distances, and w and h
    // are the width and height of the screen seen in the focal plane.  h is
    // seen under an angle that is ɑ, the field of view, and therefore the focal
    // distance is d = h / tan ɑ/2 = n / (m11 tan ɑ/2).
    double focal =
        camera.nearClipPlane / (camera.projectionMatrix[1, 1] *
                                Math.Tan(Math.PI * camera.fieldOfView / 360));
    return plugin.PlanetariumCreate(
               sun_world_position,
               (XYZ)(Vector3d)opengl_camera_x_in_world,
               (XYZ)(Vector3d)opengl_camera_y_in_world,
               (XYZ)(Vector3d)opengl_camera_z_in_world,
               (XYZ)(Vector3d)camera_position_in_world,
               focal);
  }

  public static void PlotAndDeleteRP2Lines(IntPtr rp2_lines_iterator,
                                           UnityEngine.Color colour,
                                           Style style) {
    try {
      UnityEngine.GL.Color(colour);

      // First evaluate the total size of the lines.
      int size = 0;
      for (;
            !rp2_lines_iterator.IteratorAtEnd();
            rp2_lines_iterator.IteratorIncrement()) {
        IntPtr rp2_line_iterator =
            rp2_lines_iterator.IteratorGetRP2LinesIterator();
        try {
          size += rp2_line_iterator.IteratorSize();
        } finally {
          Interface.IteratorDelete(ref rp2_line_iterator);
        }
      }

      Log.Info("LN:" + rp2_lines_iterator.IteratorSize());

      // Reset the iterator and do the actual plotting.
      rp2_lines_iterator.IteratorReset();
      int index = 0;
      for (;
           !rp2_lines_iterator.IteratorAtEnd();
           rp2_lines_iterator.IteratorIncrement()) {
        IntPtr rp2_line_iterator =
            rp2_lines_iterator.IteratorGetRP2LinesIterator();
        try {
          XYZ? previous_rp2_point = null;
          Log.Info("PT:" + rp2_line_iterator.IteratorSize());
          for (;
                !rp2_line_iterator.IteratorAtEnd();
                rp2_line_iterator.IteratorIncrement()) {
            XYZ current_rp2_point = ToScreen(
                rp2_line_iterator.IteratorGetRP2LineXYZ());
            if (previous_rp2_point.HasValue) {
              Log.Info("N:" + previous_rp2_point.Value.x + " " +
                        previous_rp2_point.Value.y + "/" + current_rp2_point.x +
                        " " + current_rp2_point.y);
              if (style == Style.FADED) {
                colour.a = (float)(4 * index + size) / (float)(5 * size);
                UnityEngine.GL.Color(colour);
              }
              if (style != Style.DASHED || index % 2 == 1) {
                UnityEngine.GL.Vertex3((float)previous_rp2_point.Value.x,
                                        (float)previous_rp2_point.Value.y,
                                        0);
                UnityEngine.GL.Vertex3((float)current_rp2_point.x,
                                        (float)current_rp2_point.y,
                                        0);
              }
            }
            previous_rp2_point = current_rp2_point;
          }
        } finally {
          Interface.IteratorDelete(ref rp2_line_iterator);
        }
      }
    } finally {
      Interface.IteratorDelete(ref rp2_lines_iterator);
    }
  }

  private static UnityEngine.Vector3 WorldToMapScreen(Vector3d world) {
    return PlanetariumCamera.Camera.WorldToScreenPoint(
               ScaledSpace.LocalToScaledSpace(world));
  }

  private static XYZ ToScreen(XYZ rp2_point) {
    UnityEngine.Camera camera = PlanetariumCamera.Camera;
    return new XYZ{x = (rp2_point.x * camera.projectionMatrix[0, 0] + 1.0f) *
                       0.5f * camera.pixelWidth,
                   y = (rp2_point.y * camera.projectionMatrix[1, 1] + 1.0f) *
                       0.5f * camera.pixelHeight,
                   z = rp2_point.z};
   }

  public static void PlotPsychohistory(IntPtr plugin,
                                       string vessel_guid,
                                       XYZ sun_world_position) {
    UnityEngine.GL.Color(XKCDColors.Banana);

    IntPtr planetarium = NewPlanetarium(plugin, sun_world_position);
    IntPtr rp2_lines_iterator =
        plugin.PlanetariumPlotPsychohistory(planetarium, vessel_guid);
    Log.Info("LN:" + rp2_lines_iterator.IteratorSize());
    for (;
         !rp2_lines_iterator.IteratorAtEnd();
         rp2_lines_iterator.IteratorIncrement()) {
      XYZ? previous_rp2_point = null;
      IntPtr rp2_line_iterator =
          rp2_lines_iterator.IteratorGetRP2LinesIterator();
      Log.Info("PT:" + rp2_line_iterator.IteratorSize());
      for (;
           !rp2_line_iterator.IteratorAtEnd();
           rp2_line_iterator.IteratorIncrement()) {
        XYZ current_rp2_point = ToScreen(
            rp2_line_iterator.IteratorGetRP2LineXYZ());
        if (previous_rp2_point.HasValue) {
          Log.Info("N:" + previous_rp2_point.Value.x + " " +
                   previous_rp2_point.Value.y + "/" + current_rp2_point.x +
                   " " + current_rp2_point.y);
          UnityEngine.GL.Vertex3((float)previous_rp2_point.Value.x,
                                  (float)previous_rp2_point.Value.y,
                                  0);
          UnityEngine.GL.Vertex3((float)current_rp2_point.x,
                                  (float)current_rp2_point.y,
                                  0);
        }
        previous_rp2_point = current_rp2_point;
      }
    }
  }

  private static bool rendering_lines_ = false;
  private static CelestialBody[] hiding_bodies_;
  private static UnityEngine.Material line_material_;
  private static UnityEngine.Material line_material {
    get {
      if (line_material_ == null) {
        line_material_ = new UnityEngine.Material(
            UnityEngine.Shader.Find("Particles/Additive"));
      }
      return line_material_;
    }
  }
}

}  // namespace ksp_plugin_adapter
}  // namespace principia
