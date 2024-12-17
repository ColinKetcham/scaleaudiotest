using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace Protokit.Browser {

  [Serializable]
  public class ApplyChangesGUIContent {

    private static GUILayoutOption[] _buttonOptions = new GUILayoutOption[] { GUILayout.Width(60) };

    public void OnGUI(BrowserState state) {
      if (!state.AnyPendingChanges) {
        return;
      }

      if (!state.DatabaseRequest.IsCompleted) {
        return;
      }
      var database = state.DatabaseRequest.Result;

      using (new GUILayout.VerticalScope(BrowserStyles.ApplyChangesBackground)) {
        GUILayout.Label("Pending Changes", BrowserStyles.LargeHeader);

        foreach (var toInstall in state.PackagesToInstall) {
          var package = database.PackagesByName[toInstall.Key];

          GUILayout.Space(8);
          IndentedLabel(package.Default.Title, 20, BrowserStyles.DetailsNormal);
          if (database.TryGetInstalledVersion(toInstall.Key, out var installedVersion)) {
            IndentedLabel($"v{installedVersion}   →   v{toInstall.Value}", 30, BrowserStyles.DetailsMini);
          } else {
            IndentedLabel($"Install v{toInstall.Value}", 30, BrowserStyles.DetailsMini);
          }
        }

        foreach (var toUninstall in state.PackagesToUninstall) {
          var package = database.PackagesByName[toUninstall];

          GUILayout.Space(8);
          IndentedLabel(package.Default.Title, 20, BrowserStyles.DetailsNormal);

          if (database.TryGetInstalledVersion(toUninstall, out var installedVersion)) {
            IndentedLabel($"Uninstall v{installedVersion}", 30, BrowserStyles.DetailsMini);
          }
        }

        GUILayout.Space(20);

        using (new GUILayout.HorizontalScope()) {
          GUILayout.FlexibleSpace();

          if (GUILayout.Button("Cancel", _buttonOptions)) {
            state.PackagesToInstall.Clear();
            state.PackagesToUninstall.Clear();
          }

          if (GUILayout.Button("Apply", _buttonOptions) &&
              BrowserUtil.CheckWithUserForIncompatiblePackages(state.PackagesToInstall.Select(k => (k.Key, k.Value)))) {
            foreach (var toInstall in state.PackagesToInstall) {
              PackageDatabase.Install(toInstall.Key, toInstall.Value, notifyChanges: false);
            }
            foreach (var toUninstall in state.PackagesToUninstall) {
              PackageDatabase.Uninstall(toUninstall, notifyChanges: false);
            }
            PackageDatabase.NotifyFileChanges();

            state.PackagesToInstall.Clear();
            state.PackagesToUninstall.Clear();
          }

          GUILayout.FlexibleSpace();
        }
      }
    }

    private static void IndentedLabel(string text, float indent, GUIStyle style) {
      GUILayout.BeginHorizontal();
      GUILayout.Space(indent);
      GUILayout.Label(text, style);
      GUILayout.EndHorizontal();
    }
  }
}
