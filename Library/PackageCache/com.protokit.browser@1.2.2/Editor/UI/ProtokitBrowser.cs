// (c) Meta Platforms, Inc. and affiliates. Confidential and proprietary.

using System;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.Assertions;
using UnityEditor;
using UPackageInfo = UnityEditor.PackageManager.PackageInfo;

namespace Protokit.Browser {

  public class ProtokitBrowser : EditorWindow {

    private const string STATE_KEY = "ProtokitBrowser_UI_State";
    private const float FILTER_SUBWINDOW_WIDTH = 220;

    [SerializeField]
    private HeaderGUIContent _headerContent = new HeaderGUIContent();

    [SerializeField]
    private FilterGUIContent _filterContent = new FilterGUIContent();

    [SerializeField]
    private ApplyChangesGUIContent _applyChangesContent = new ApplyChangesGUIContent();

    [SerializeField]
    private SearchTagGUIContent _searchTagContent = new SearchTagGUIContent();

    [SerializeField]
    private PackageGUIContent _packageContent = new PackageGUIContent();

    [SerializeField]
    private PackageListGUIContent _packageListContent = new PackageListGUIContent();

    [SerializeField]
    private HomeGUIContent _homeContent = new HomeGUIContent();

    [NonSerialized]
    public BrowserState _state;

    [MenuItem("Window/ProtoKit Browser", priority = 1500)]
    private static ProtokitBrowser OpenBrowser() {
      var window = GetWindow<ProtokitBrowser>();
      window.titleContent = new GUIContent("ProtoKit Browser");
      return window;
    }

    [MenuItem("Assets/View In ProtoKit Browser", priority = 30)]
    private static void OpenBrowserWithContext() {
      var packageInfo = GetPackageInfoForSelectedAsset();
      Assert.IsNotNull(packageInfo);

      var window = OpenBrowser();
      window.LoadBrowserState();
      window._state.SearchParameters.ClearSearchTerms();
      window._state.SearchParameters.AddSearchTerm(packageInfo.name);
      window._state.ExpandedPackageName = packageInfo.name;
      window.SaveBrowserState();
    }

    [MenuItem("Assets/View In ProtoKit Browser", isValidateFunction: true)]
    private static bool OpenBrowserWithContextValidation() {
      return GetPackageInfoForSelectedAsset() != null;
    }

    /// <summary>
    /// Gets the UPackageInfo that hosts the selected asset.  Will also
    /// work for selecting folders that are part of packages.
    /// </summary>
    private static UPackageInfo GetPackageInfoForSelectedAsset() {
      foreach (var guid in Selection.assetGUIDs) {
        var assetPath = AssetDatabase.GUIDToAssetPath(guid);
        if (string.IsNullOrEmpty(assetPath)) {
          continue;
        }

        var packageInfo = UPackageInfo.FindForAssetPath(assetPath);
        if (packageInfo != null) {
          return packageInfo;
        }
      }

      return null;
    }

    public static void ResetBrowserState() {
      PlayerPrefs.DeleteKey(STATE_KEY);
      GetWindow<ProtokitBrowser>()._state = null;
    }

    private void OnGUI() {
      try {
        GUI.skin = BrowserStyles.Skin;

        HandleWindowRepaint();

        LoadBrowserState();

        DrawInterface();

        SaveBrowserState();
      } catch (Exception) {
        ResetBrowserState();
        throw;
      }
    }

    private void HandleWindowRepaint() {
      //Hover effects only work properly with legacy gui if the window is repainted every frame.
      //We can avoid a repaint *only* if we are not the focused window, and if the current database
      //request has been completed.
      bool canAvoidRepaint = focusedWindow != this &&
                             _state?.DatabaseRequest?.IsCompleted == true;

      if (!canAvoidRepaint) {
        Repaint();
      }
    }

    private void LoadBrowserState() {
      if (_state == null) {
        if (PlayerPrefs.HasKey(STATE_KEY)) {
          _state = JsonUtility.FromJson<BrowserState>(PlayerPrefs.GetString(STATE_KEY));
        } else {
          _state = new BrowserState();
        }
      }

      _state.DatabaseRequest = PackageDatabase.Query();

      _state.IsBusy = !_state.DatabaseRequest.IsCompleted ||
                      EditorApplication.isCompiling ||
                      EditorApplication.isUpdating;
    }

    private void DrawInterface() {
      _headerContent.OnGUI(_state, onRefresh: () => {
        _state.DatabaseRequest = PackageDatabase.Query(forceRefresh: true);
      });

      if (_state.DatabaseRequest.IsCompleted) {
        _state.SearchParameters.UpdateFiltersUsingPackageList(_state.DatabaseRequest.Result);
      }

      using (new GUILayout.HorizontalScope()) {
        using (new GUILayout.VerticalScope(GUILayout.MaxWidth(position.width - FILTER_SUBWINDOW_WIDTH), GUILayout.ExpandWidth(true))) {
          if (_state.SearchParameters.ActiveTags.Count != 0) {
            GUILayout.Space(10);
            _searchTagContent.OnGUI(_state);

            Profiler.BeginSample("Package List");
            _packageListContent.OnGUI(_state, _packageContent, position.size);
            Profiler.EndSample();
          } else {
            Profiler.BeginSample("Home");
            _homeContent.OnGUI(_state, _packageContent, position.size);
            Profiler.EndSample();
          }
        }

        using (new GUILayout.VerticalScope(GUILayout.Width(FILTER_SUBWINDOW_WIDTH), GUILayout.ExpandWidth(false))) {
          Profiler.BeginSample("Filtering");
          _filterContent.OnGUI(_state);
          Profiler.EndSample();

          Profiler.BeginSample("Apply Changes");
          _applyChangesContent.OnGUI(_state);
          Profiler.EndSample();
        }
      }
    }

    private void SaveBrowserState() {
      PlayerPrefs.SetString(STATE_KEY, JsonUtility.ToJson(_state));
    }
  }
}
