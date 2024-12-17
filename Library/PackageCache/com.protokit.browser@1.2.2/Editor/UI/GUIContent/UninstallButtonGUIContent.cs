// (c) Meta Platforms, Inc. and affiliates. Confidential and proprietary.

using System;
using UnityEngine;
using UnityEditor;

namespace Protokit.Browser {

  [Serializable]
  public class UninstallButtonGUIContent {

    private static GUIContent UninstallContent = new GUIContent("Remove");
    private static GUIContent CantUninstallContent = new GUIContent("Remove", "Can't remove this package because another package depends on it");

    private static Color UninstallColor = new Color(1.0f, 0.4f, 0.4f);
    private static GUILayoutOption[] UninstallWidth = new GUILayoutOption[] { GUILayout.Width(67) };

    public void OnGUI(BrowserState state, PackageInfo package) {
      if (!state.DatabaseRequest.IsCompleted) {
        return;
      }

      GUI.color = UninstallColor;

      if (state.DatabaseRequest.Result.IsPackageInstalled(package.PackageName)) {
        if (state.DatabaseRequest.Result.IsDirectDependency(package.PackageName)) {
          if (GUILayout.Button(UninstallContent, BrowserStyles.Install, UninstallWidth)) {
            if (BrowserUtil.ShouldQueueAction()) {
              state.QueuePackageUninstall(package.PackageName);
            } else {
              PackageDatabase.Uninstall(package.PackageName);
            }
          }
        } else {
          EditorGUI.BeginDisabledGroup(true);
          GUILayout.Button(CantUninstallContent, BrowserStyles.Install, UninstallWidth);
          EditorGUI.EndDisabledGroup();
        }
      }

      GUI.color = Color.white;
    }
  }
}
