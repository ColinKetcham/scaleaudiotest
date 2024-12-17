// (c) Meta Platforms, Inc. and affiliates. Confidential and proprietary.

using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace Protokit.Browser {

  public static class ProtokitBrowserProjectSettings {

    private const string DEFAULT_ACTION_IS_QUEUE_KEY = "PROTOKIT_BROWSER_DEFAULT_ACTION_IS_QUEUE";

    public static bool DefaultActionIsQueue {
      get => EditorPrefs.GetBool(DEFAULT_ACTION_IS_QUEUE_KEY, defaultValue: true);
      set => EditorPrefs.SetBool(DEFAULT_ACTION_IS_QUEUE_KEY, value);
    }

    private static readonly GUIContent DefaultQueueContent = new GUIContent("Queue Actions By Default",
      "Whether or not to queue install/remove actions by default, which will allow you to make multiple actions " +
      "without waiting for a reimport in between.\n\nYou can always press shift to take the opposite of the default " +
      "action specified here.");

    [SettingsProvider]
    private static SettingsProvider CreateBrowserSettingsProvider() {
      return new SettingsProvider("Preferences/Protokit/Browser", SettingsScope.User) {
        label = "Browser",
        keywords = new HashSet<string>() { "ProtoKit", "Core", "Browser" },
        guiHandler = ctx => {
          EditorGUILayout.Space();
          EditorGUIUtility.labelWidth = 200;

          if (ProtokitBrowserFeatureFlags.AllowPackageActionQueueing) {
            using (new GUILayout.HorizontalScope())
            using (var check = new EditorGUI.ChangeCheckScope()) {
              GUILayout.Space(10);

              var newValue = EditorGUILayout.Toggle(DefaultQueueContent, DefaultActionIsQueue);
              if (check.changed) {
                DefaultActionIsQueue = newValue;
              }
            }
          }
        }
      };
    }
  }
}
