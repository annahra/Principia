﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace principia {
namespace ksp_plugin_adapter {

internal abstract class BaseWindowRenderer : IConfigNode {
  protected BaseWindowRenderer(UnityEngine.GUILayoutOption[] options) {
    // All dimensions are expressed in "units".  By default a unit is 25 pixels
    // but it can scale up or down based on the KSP UI scale.
    unit_ = 25 * GameSettings.UI_SCALE;
    UnityEngine.GUILayoutOption[] default_options =
        {UnityEngine.GUILayout.MinWidth(20 * unit_)};
    
    options_ = options.Length == 0 ? default_options : options;
    lock_name_ = GetType().ToString() + ":lock:" + GetHashCode();
  }

  // Locking.

  protected void ClearLock() {
    InputLockManager.RemoveControlLock(lock_name_);
  }

  private void InputLock() {
    UnityEngine.Vector3 mouse = UnityEngine.Input.mousePosition;
    mouse.y = UnityEngine.Screen.height - mouse.y;
    if (rectangle_.Contains(mouse)) {
      InputLockManager.SetControlLock(PrincipiaLock, lock_name_);
    } else {
      InputLockManager.RemoveControlLock(lock_name_);
    }
  }

  // Rendering.
  public void RenderWindow() {
    var old_skin = UnityEngine.GUI.skin;
    ApplySkin();
    if (show_) {
      rectangle_ = UnityEngine.GUILayout.Window(
                       id         : this.GetHashCode(),
                       screenRect : rectangle_,
                       func       : RenderWindow,
                       text       : Title,
                       options    : options_);

      // The first time a window is shown, we have a moral duty to place it at
      // the centre of the screen.  This is tricky because we don't know its
      // width.  So we go through this method until the width has been
      // determined, and we update the window based on the width.  Note that
      // |must_centre_| has to be persisted because there can be multiple
      // scene changes because a window is shown for the first time.
      if (must_centre_ && rectangle_.width > 0) {
        rectangle_ =
            UnityEngine.GUILayout.Window(
                id         : this.GetHashCode(),
                screenRect : new UnityEngine.Rect(
                    x      : (UnityEngine.Screen.width - rectangle_.width) / 2,
                    y      : UnityEngine.Screen.height / 3,
                    width  : 0,
                    height : 0),
                func       : RenderWindow,
                text       : Title,
                options    : options_);
        must_centre_ = false;
      }
      EnsureOnScreen();
      InputLock();
    } else {
      ClearLock();
    }
    UnityEngine.GUI.skin = old_skin;
  }

  private void EnsureOnScreen() {
    rectangle_.x = UnityEngine.Mathf.Clamp(
                       rectangle_.x,
                       -rectangle_.width + min_width_on_screen_,
                       UnityEngine.Screen.width - min_width_on_screen_);
    rectangle_.y = UnityEngine.Mathf.Clamp(
                       rectangle_.y,
                       -rectangle_.height + min_height_on_screen_,
                       UnityEngine.Screen.height - min_height_on_screen_);
  }

  public void Shrink() {
    rectangle_.height = 0.0f;
    rectangle_.width = 0.0f;
  }

  // Visibility.

  public void Hide() {
    show_ = false;
  }

  public void Show() {
    show_ = true;
  }

  public bool Shown() {
    return show_;
  }

  public void Toggle() {
    show_ = !show_;
  }

  // Scaling.

  protected UnityEngine.GUILayoutOption GUILayoutMinWidth(int units) {
    return UnityEngine.GUILayout.MinWidth(unit_ * units);
  }

  protected UnityEngine.GUILayoutOption GUILayoutWidth(int units) {
    return UnityEngine.GUILayout.Width(unit_ * units);
  }

  protected float Width(int units) {
    return unit_ * units;
  }

  private void ApplySkin() {
    //KSP.UI.UIMasterController.Instance.appCanvas.scaleFactor =
    //    GameSettings.UI_SCALE_APPS;
    if (skin_ == null) {
      UnityEngine.GUI.skin = null;
      // The default Unity skin uses Arial 13.
      // TODO(phl): Do we need to change the fonts of the nested styles?
      skin_ = UnityEngine.Object.Instantiate(UnityEngine.GUI.skin);

      var pangram = new UnityEngine.GUIContent(
          "Portez ce vieux whisky au juge blond qui fume.");
      float button_height = skin_.button.CalcHeight(pangram, width: 1000);
      float label_height = skin_.label.CalcHeight(pangram, width: 1000);
      float text_area_height = skin_.textArea.CalcHeight(pangram, width: 1000);
      float toggle_height = skin_.toggle.CalcHeight(pangram, width: 1000);
      UnityEngine.Debug.LogError(button_height + " " + label_height + " " +
      text_area_height+" "+toggle_height+" "+skin_.toggle.margin.top);

      skin_.font = UnityEngine.Font.CreateDynamicFontFromOSFont(
                        skin_.font.fontNames,
                        (int)(skin_.font.fontSize * GameSettings.UI_SCALE));

      skin_.button.fixedHeight = button_height * GameSettings.UI_SCALE;
      skin_.button.contentOffset =
          new UnityEngine.Vector2(0, -skin_.button.fixedHeight / 10);
      skin_.label.fixedHeight = label_height * GameSettings.UI_SCALE;
      skin_.label.contentOffset =
          new UnityEngine.Vector2(0, skin_.label.fixedHeight / 20);
      skin_.textArea.fixedHeight = text_area_height * GameSettings.UI_SCALE;
      skin_.textArea.contentOffset =
          new UnityEngine.Vector2(0, skin_.textArea.fixedHeight / 20);
      skin_.toggle.fixedHeight = toggle_height * GameSettings.UI_SCALE;
      skin_.toggle.contentOffset =
          new UnityEngine.Vector2(0, -skin_.toggle.fixedHeight / 10);
      skin_.toggle.margin = new UnityEngine.RectOffset(
          skin_.toggle.margin.left,
          skin_.toggle.margin.right,
          (int)(skin_.toggle.margin.top * 1.7 * GameSettings.UI_SCALE),
          skin_.toggle.margin.bottom);
    }
    UnityEngine.GUI.skin = skin_;
  }

  // Persistence.

  public virtual void Load(ConfigNode node) {
    String must_centre_value = node.GetAtMostOneValue("must_centre");
    if (must_centre_value != null) {
      must_centre_ = Convert.ToBoolean(must_centre_value);
    }
    String show_value = node.GetAtMostOneValue("show");
    if (show_value != null) {
      show_ = Convert.ToBoolean(show_value);
    }
    String x_value = node.GetAtMostOneValue("x");
    if (x_value != null) {
      rectangle_.x = Convert.ToSingle(x_value);
    }
    String y_value = node.GetAtMostOneValue("y");
    if (y_value != null) {
      rectangle_.y = Convert.ToSingle(y_value);
    }
  }

  public virtual void Save(ConfigNode node) {
    node.SetValue("must_centre", must_centre_, createIfNotFound : true);
    node.SetValue("show", show_, createIfNotFound : true);
    node.SetValue("x", rectangle_.x, createIfNotFound : true);
    node.SetValue("y", rectangle_.y, createIfNotFound : true);
  }

  abstract protected String Title { get; }
  abstract protected void RenderWindow(int window_id);

  private static readonly ControlTypes PrincipiaLock =
      ControlTypes.ALLBUTCAMERAS &
      ~ControlTypes.ALL_SHIP_CONTROLS;

  private const float min_height_on_screen_ = 50;
  private const float min_width_on_screen_ = 50;

  private readonly float unit_;
  private readonly UnityEngine.GUILayoutOption[] options_;
  private readonly String lock_name_;
  private UnityEngine.GUISkin skin_;
  private bool must_centre_ = true;
  private bool show_ = false;
  private UnityEngine.Rect rectangle_;
}

internal abstract class SupervisedWindowRenderer : BaseWindowRenderer {
  public interface ISupervisor {
    event Action clear_locks;
    event Action dispose_windows;
    event Action render_windows;
  }

  public SupervisedWindowRenderer(ISupervisor supervisor,
                                  params UnityEngine.GUILayoutOption[] options)
        : base(options) {
    supervisor_ = supervisor;
    supervisor_.clear_locks += ClearLock;
    supervisor_.dispose_windows += DisposeWindow;
    supervisor_.render_windows += RenderWindow;
  }

  public void DisposeWindow() {
    supervisor_.clear_locks -= ClearLock;
    supervisor_.dispose_windows -= DisposeWindow;
    supervisor_.render_windows -= RenderWindow;
  }

  private ISupervisor supervisor_;
}

internal abstract class UnsupervisedWindowRenderer : BaseWindowRenderer {
  public UnsupervisedWindowRenderer(
      params UnityEngine.GUILayoutOption[] options) : base(options) {}
}

}  // namespace ksp_plugin_adapter
}  // namespace principia
