// (c) Meta Platforms, Inc. and affiliates. Confidential and proprietary.

using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace Protokit.Core {

  public static class KeystoreProjectSettings {

    private const string OVERWRITE_KEYSTORE_ON_BUILD_KEY = "Protokit_Core_OverwriteKeystoreOnBuild";
    private const string KEYSTORE_SOURCE_IDENTIFIER_KEY = "Protokit_KeystoreSourceIdentifier";

    public static bool OverwriteKeystoreOnBuild {
      get => PlayerPrefs.GetInt(OVERWRITE_KEYSTORE_ON_BUILD_KEY, defaultValue: 1) == 1;
      set => PlayerPrefs.SetInt(OVERWRITE_KEYSTORE_ON_BUILD_KEY, value ? 1 : 0);
    }

    public static string KeystoreSourceIdentifier {
      get => PlayerPrefs.GetString(KEYSTORE_SOURCE_IDENTIFIER_KEY, defaultValue: "");
      set => PlayerPrefs.SetString(KEYSTORE_SOURCE_IDENTIFIER_KEY, value);
    }

    [SettingsProvider]
    private static SettingsProvider CreateCoreSettingsProvider() {
      var overwriteKeystoreContent = new GUIContent("Auto-Set Keystore", "Automatically overwrite the existing keystore when an Android build is produced.  This allows the build to be used in the 1-click distribution tool.");
      var keystoreContent = new GUIContent("Keystore Source", "The source of the keystore to use when overwriting the build keystore.");
      var keystoreTitles = KeystoreAutomation.AvailableKeystores.Select(k => new GUIContent(k.Title)).ToArray();

      return new SettingsProvider("Project/Protokit Core/Keystore", SettingsScope.Project) {
        label = "Keystore",
        keywords = new HashSet<string>() { "Protokit", "Core" },
        guiHandler = (ctx) => {
          EditorGUIUtility.labelWidth = 200;
          EditorGUILayout.Space();

          //Use constant of 1 because the `Auto` keystore counts as one, even if there are no other actual keystores around
          if (KeystoreAutomation.AvailableKeystores.Count <= 1) {
            EditorGUILayout.HelpBox("No custom keystores were found, implement IKeystoreProvider to specify your own.", MessageType.Warning);
            EditorGUI.BeginDisabledGroup(true);
          } else {
            EditorGUI.BeginDisabledGroup(false);
          }

          using (new GUILayout.HorizontalScope())
          using (var check = new EditorGUI.ChangeCheckScope()) {
            GUILayout.Space(10);

            bool newValue = EditorGUILayout.Toggle(overwriteKeystoreContent, OverwriteKeystoreOnBuild);
            if (check.changed) {
              OverwriteKeystoreOnBuild = newValue;
            }

            if (GUILayout.Button("Set Keystore Now", GUILayout.Height(EditorGUIUtility.singleLineHeight), GUILayout.ExpandWidth(false))) {
              KeystoreAutomation.SetKeystore();
            }
          }

          using (new GUILayout.HorizontalScope())
          using (var check = new EditorGUI.ChangeCheckScope()) {
            GUILayout.Space(10);

            int index = 0;
            for (int i = 0; i < KeystoreAutomation.AvailableKeystores.Count; i++) {
              if (KeystoreAutomation.AvailableKeystores[i].Identifier == KeystoreSourceIdentifier) {
                index = i;
                break;
              }
            }

            int newIndex = EditorGUILayout.Popup(keystoreContent, index, keystoreTitles);
            if (check.changed) {
              KeystoreSourceIdentifier = KeystoreAutomation.AvailableKeystores[newIndex].Identifier;
            }
          }

          EditorGUI.EndDisabledGroup();
        }
      };
    }
  }
}
