// (c) Meta Platforms, Inc. and affiliates. Confidential and proprietary.

using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using TinyJson;

namespace Protokit.Core {

  public class PackageNamespaceTool : EditorWindow {

    /// <summary>
    /// Remembered namespaces serve to populate the list of available namespaces the user can check or uncheck in the
    /// namespace tool.  Once a namespace gets added, it will be remembered across multiple project instances.
    /// </summary>
    private const string REMEMBERED_NAMESPACES_KEY = "PrototypingToolkit_PackageNamespaceTool_RememberedNamespaces";

    private static readonly GUIContent _mandatoryNamespacesLabel = new GUIContent("Mandatory Namespaces", "These namespaces cannot be disabled because they contain required packages.");
    private static readonly GUIContent _extraNamespacesLabel = new GUIContent("Extra Namespaces", "These are a few namespaces that contain packages that might be interesting.\n\n" +
                                                                                                  "If a namespace you are looking for isn't in this list, you can add it using the option at the bottom of this window.");

    /// <summary>
    /// Namespaces that cannot be disabled and will always show up
    /// </summary>
    private static readonly string[] MANDATORY_NAMESPACES = new string[] {
      "com.protokit",
      "com.oculus"
    };

    [MenuItem("Protokit/Packages/Namespace Tool")]
    private static void Init() {
      GetWindow<PackageNamespaceTool>().Show();
    }

    private static string ManifestPath {
      get {
        string rootProjectPath = Path.GetDirectoryName(Application.dataPath);
        string path = Path.Combine(rootProjectPath, "Packages", "manifest.json");
        return path;
      }
    }

    private static Dictionary<string, object> Manifest;

    private static Dictionary<string, object> Registry {
      get {
        if (!Manifest.ContainsKey("scopedRegistries")) {
          return null;
        }

        if (!Manifest.TryGetValue("scopedRegistries", out object registriesObj)) {
          return null;
        }
        if (!(registriesObj is List<object> registriesList)) {
          return null;
        }

        foreach (var obj in registriesList) {
          var registry = obj as Dictionary<string, object>;
          if (registry == null) {
            continue;
          }

          if (!(registry.TryGetValue("scopes", out object element) && element is List<object> scopes)) {
            continue;
          }

          if (scopes.Any(s => MANDATORY_NAMESPACES.Contains(s))) {
            return registry;
          }
        }

        return null;
      }
    }

    private static List<object> EnabledNamespaces {
      get {
        if (Registry == null) {
          return null;
        }

        List<object> list;

        if (!(Registry.TryGetValue("scopes", out object element) && element is List<object> scopes)) {
          list = new List<object>();
          Registry["scopes"] = list;
        } else {
          list = scopes;
        }

        return list;
      }
    }

    private static string[] _cachedRememberedNamespaces = null;
    private static IEnumerable<string> RememberedNamespaces {
      get {
        if (_cachedRememberedNamespaces == null) {
          _cachedRememberedNamespaces = EditorPrefs.GetString(REMEMBERED_NAMESPACES_KEY, defaultValue: "").
                                                    Split(new char[] { '|' }, StringSplitOptions.RemoveEmptyEntries);
        }

        return _cachedRememberedNamespaces;
      }
      set {
        if (!_cachedRememberedNamespaces.SequenceEqual(value)) {
          _cachedRememberedNamespaces = value.ToArray();
          EditorPrefs.SetString(REMEMBERED_NAMESPACES_KEY, string.Join("|", value));
        }
      }
    }

    private string _customNamespaceToAdd;
    private float _refreshTime = 0;
    private bool _showSavedMessage = false;

    private Vector2 _scrollPosition;

    private void Awake() {
      titleContent = new GUIContent("Package Namespace Tool");
    }

    private void OnFocus() {
      Manifest = null;
      RefreshManifest();
    }

    private void OnGUI() {
      RefreshManifest();

      using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox)) {
        EditorGUILayout.LabelField("This namespace tool allows you to enable and disable different package namespaces.  " +
                                   "By enabling new namespaces you can get access to the packages inside of that namespace, " +
                                   "and by disabling a namespace you can hide packages in that namespace, although this will " +
                                   "NOT uninstall packages from that namespace.",
                                   EditorStyles.wordWrappedLabel);
      }

      Rect headerRect = GUILayoutUtility.GetRect(0, 20, GUILayout.ExpandWidth(true));

      float refreshTimeLeft = _refreshTime - Time.realtimeSinceStartup;
      if (refreshTimeLeft > 0) {
        Rect iconRect = headerRect;
        iconRect.width = iconRect.height;
        iconRect.x = headerRect.xMax - iconRect.width - 5;

        _showSavedMessage = true;
        int animFrame = Mathf.RoundToInt(Time.realtimeSinceStartup * 40) % 12;
        var spinContent = EditorGUIUtility.IconContent("WaitSpin" + animFrame.ToString().PadLeft(2, '0'));
        GUI.Box(iconRect, spinContent, GUIStyle.none);
        Repaint();
      } else if (_showSavedMessage) {
        GUIContent content = new GUIContent("Changes Saved");

        Rect messageRect = headerRect;
        messageRect.width = EditorStyles.label.CalcSize(content).x;
        messageRect.x = headerRect.xMax - messageRect.width - 10;

        GUI.color = Color.green;
        GUI.Label(messageRect, "Changes Saved", EditorStyles.label);
        GUI.color = Color.white;
      }

      if (Registry == null) {
        EditorGUILayout.HelpBox("Could not locate an installed registry!  Make sure protokit has been installed correctly and your manifest.json has not been incorrectly modified.", MessageType.Error);
        return;
      }

      using (var scroller = new EditorGUILayout.ScrollViewScope(_scrollPosition)) {
        _scrollPosition = scroller.scrollPosition;

        EditorGUILayout.LabelField(_mandatoryNamespacesLabel, EditorStyles.boldLabel);

        foreach (var name in MANDATORY_NAMESPACES) {
          DrawNamespace(name, true);
        }

        GUILayout.Space(30);
        EditorGUILayout.LabelField(_extraNamespacesLabel, EditorStyles.boldLabel);

        foreach (var name in RememberedNamespaces.Except(MANDATORY_NAMESPACES).OrderByDescending(IsBuiltInNamespace)) {
          DrawNamespace(name, false);
        }
      }

      GUILayout.FlexibleSpace();

      using (new GUILayout.HorizontalScope()) {
        const string CONTROL_NAME = "custom_namespace_to_add_control";

        GUI.SetNextControlName(CONTROL_NAME);
        _customNamespaceToAdd = (EditorGUILayout.TextField(_customNamespaceToAdd) ?? "").Trim();

        if (_customNamespaceToAdd == "" && GUI.GetNameOfFocusedControl() != CONTROL_NAME) {
          Rect previewRect = GUILayoutUtility.GetLastRect();
          previewRect.x += 3;

          var prevColor = GUI.color;
          GUI.color = new Color(1, 1, 1, 0.3f);
          EditorGUI.LabelField(previewRect, "com.example.namespace");
          GUI.color = prevColor;
        }

        bool isValidNamespace = _customNamespaceToAdd.StartsWith("com.");

        using (new EditorGUI.DisabledGroupScope(!isValidNamespace)) {
          if (GUILayout.Button(new GUIContent("Add Custom Namespace", isValidNamespace ? "" : "Namespaces must start with 'com.' in order to be valid"), GUILayout.Height(EditorGUIUtility.singleLineHeight))) {
            EnableNamespace(_customNamespaceToAdd);
            _customNamespaceToAdd = "";
            EditorGUIUtility.keyboardControl = 0;
          }
        }
      }

      GUILayout.Space(5);
    }

    private void DrawNamespace(string name, bool isMandatory) {
      using (new EditorGUILayout.HorizontalScope()) {
        bool isEnabled = EnabledNamespaces.Contains(name);

        EditorGUI.BeginDisabledGroup(isMandatory && isEnabled);
        bool shouldBeEnabled = EditorGUILayout.Toggle(isEnabled, GUILayout.Width(40));
        EditorGUI.EndDisabledGroup();

        if (isEnabled != shouldBeEnabled) {
          if (shouldBeEnabled) {
            EnableNamespace(name);
          } else {
            DisableNamespace(name);
          }
        }

        EditorGUILayout.LabelField(name);
      }

      if (!IsBuiltInNamespace(name)) {
        Rect contextRect = GUILayoutUtility.GetLastRect();
        if (Event.current.type == EventType.ContextClick && contextRect.Contains(Event.current.mousePosition)) {
          GenericMenu menu = new GenericMenu();

          menu.AddItem(new GUIContent("Forget Custom Namespace"), on: false, () => {
            DisableNamespace(name);
            RememberedNamespaces = RememberedNamespaces.Except(new string[] { name });
          });

          menu.ShowAsContext();
        }
      }
    }

    private static bool IsBuiltInNamespace(string name) {
      return MANDATORY_NAMESPACES.Contains(name);
    }

    private static void RefreshManifest() {
      if (Manifest == null) {
        if (!File.Exists(ManifestPath)) {
          Manifest = new Dictionary<string, object>();
        } else {
          Manifest = File.ReadAllText(ManifestPath).
                          FromJson<Dictionary<string, object>>();
        }
      }
    }

    private static void WriteManifest() {
      File.WriteAllText(ManifestPath, Manifest.ToJson());
    }

    private void EnableNamespace(string name) {
      EnabledNamespaces.Add(name);
      RememberedNamespaces = RememberedNamespaces.Concat(EnabledNamespaces.OfType<string>()).Distinct();

      WriteManifest();

      _refreshTime = Time.realtimeSinceStartup + 0.2f;
    }

    private void DisableNamespace(string name) {
      EnabledNamespaces.Remove(name);
      RememberedNamespaces = RememberedNamespaces.Union(EnabledNamespaces.OfType<string>());

      WriteManifest();

      _refreshTime = Time.realtimeSinceStartup + 0.2f;
    }
  }
}
