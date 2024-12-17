// (c) Meta Platforms, Inc. and affiliates. Confidential and proprietary.

using UnityEngine;
using UnityEditor;
using System.Linq;
using System.Collections.Generic;

namespace Protokit.Browser {

  public static class BrowserUtil {

    /// <summary>
    /// A static pre-allocated array with a single option of GUILayout.ExpandWidth(false).
    /// You can use this in GUILayout calls to avoid the automatic allocation of a params
    /// array when passing in this specific gui layout option.
    /// </summary>
    public static readonly GUILayoutOption[] NoExpandWidth = new GUILayoutOption[] {
        GUILayout.ExpandWidth(false)
    };

    /// <summary>
    /// Draws a loading spinner.  Participates in the automatic GUILayout process.
    /// </summary>
    public static void LoadingSpinnerOnGUI() {
      int frame = (int)(Time.realtimeSinceStartup * 15 % 12);
      var spinContent = EditorGUIUtility.IconContent($"d_WaitSpin{frame.ToString().PadLeft(2, '0')}");
      GUILayout.Box(spinContent, GUIStyle.none);
    }

    /// <summary>
    /// Draws a loading spinner and bar based on the current database progress.
    /// </summary>
    public static void LoadingBarOnGUI() {
      GUILayout.FlexibleSpace();
      using (new GUILayout.HorizontalScope()) {
        GUILayout.FlexibleSpace();
        var progressRect = GUILayoutUtility.GetRect(200, 16, GUILayout.Width(200));
        EditorGUI.ProgressBar(progressRect, PackageDatabase.CurrentProgress, PackageDatabase.CurrentProgressText);
        GUILayout.Space(5);
        LoadingSpinnerOnGUI();
        GUILayout.FlexibleSpace();
      }
      GUILayout.FlexibleSpace();
    }

    public static bool ShouldQueueAction() {
      if (!ProtokitBrowserFeatureFlags.AllowPackageActionQueueing) {
        return false;
      }

      bool shouldQueue = ProtokitBrowserProjectSettings.DefaultActionIsQueue;
      if (Event.current.shift) {
        shouldQueue = !shouldQueue;
      }

      return shouldQueue;
    }

    public static bool CheckWithUserForIncompatiblePackage(string package, string version) {
      return CheckWithUserForIncompatiblePackages(new (string, string)[] { (package, version) });
    }

    public static bool CheckWithUserForIncompatiblePackages(IEnumerable<(string package, string version)> toInstall) {
      var incompatible = toInstall.Select(i => {
        var versionTask = PackageDatabase.GetVersionInfo(i.package, i.version);
        return versionTask.Result;
      }).Where(v => !v.IsCompatible);

      if (!incompatible.Any()) {
        return true;
      }

      if (incompatible.Count() == 1) {
        return EditorUtility.DisplayDialog(
          $"Package Incompatible",
          $"The package {incompatible.First().PackageName} requires a Unity version of " +
          $"{incompatible.First().UnityVersion} or newer.\n\nDo you want to install " +
          $"the package anyway? You might encounter errors or compile issues.",
          $"Install Incompatible Package",
          $"Cancel");
      } else {
        return EditorUtility.DisplayDialog(
          $"Packages Incompatible",
          $"The following packages require a newer Unity version:\n" +
          $"  {string.Join("\n  ", incompatible.Select(p => p.PackageName))}\n\n" +
          $"Do you want to install these packages anyway? You might encounter " +
          $"errors or compile issues",
          $"Install Incompatible Packages",
          $"Cancel");
      }
    }
  }
}
