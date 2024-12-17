// (c) Meta Platforms, Inc. and affiliates. Confidential and proprietary.

using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Protokit.Core {

  public static class CoreProjectSettings {

    private const string INCREMENT_VERSION_ON_BUILD_KEY = "Protokit_Core_IncrementVersionOnBuild";

    public static bool IncrementVersionOnBuild {
      get => PlayerPrefs.GetInt(INCREMENT_VERSION_ON_BUILD_KEY, defaultValue: 1) == 1;
      set => PlayerPrefs.SetInt(INCREMENT_VERSION_ON_BUILD_KEY, value ? 1 : 0);
    }

    [SettingsProvider]
    private static SettingsProvider CreateCoreSettingsProvider() {
      var incrementVersionContent = new GUIContent("Auto-Increment Version", "Automatically increment the internal version index every time a new build is produced.");

      return new SettingsProvider("Project/Protokit Core/Build", SettingsScope.Project) {
        label = "Build Settings",
        keywords = new HashSet<string>() { "Protokit", "Core" },
        guiHandler = (ctx) => {
          EditorGUIUtility.labelWidth = 200;
          EditorGUILayout.Space();

          using (new GUILayout.HorizontalScope())
          using (var check = new EditorGUI.ChangeCheckScope()) {
            GUILayout.Space(10);

            bool newValue = EditorGUILayout.Toggle(incrementVersionContent, IncrementVersionOnBuild);
            if (check.changed) {
              IncrementVersionOnBuild = newValue;
            }

            if (GUILayout.Button("Increment Now", GUILayout.Height(EditorGUIUtility.singleLineHeight), GUILayout.ExpandWidth(false))) {
              BundleVersionCodeAutomation.IncrementBundleVersionCode();
            }
          }
        }
      };
    }
  }
}
