// (c) Meta Platforms, Inc. and affiliates. Confidential and proprietary.

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace Protokit.Browser {

  [Serializable]
  public class PackageListGUIContent {

    [SerializeField]
    private Vector2 _packageScroll;

    private PackageSearcher _search = new PackageSearcher();
    private List<PackageInfo> _filteredPackages = new List<PackageInfo>();

    public void OnGUI(BrowserState state, PackageGUIContent packagePanel, Vector2 containerSize) {
      if (state.DatabaseRequest.IsCompleted) {
        _search.FilterPackages(state.DatabaseRequest.Result, state.SearchParameters, _filteredPackages);
      } else {
        _filteredPackages.Clear();
      }

      using (new GUILayout.HorizontalScope()) {
        GUILayout.Space(10);
        GUILayout.Label($"Displaying {_filteredPackages.Count} packages.");
        GUILayout.FlexibleSpace();
        state.SearchParameters.SortMethod = (SearchSortMethod)EditorGUILayout.EnumPopup(GUIContent.none, state.SearchParameters.SortMethod, BrowserStyles.Dropdown, GUILayout.Width(190));
        GUILayout.Space(14);
      }

      GUILayout.Box("", BrowserStyles.FilterSeparator);

      GUILayout.BeginHorizontal();
      GUILayout.Space(4);
      using (var scroller = new GUILayout.ScrollViewScope(_packageScroll, alwaysShowHorizontal: false, alwaysShowVertical: true)) {
        if (scroller.scrollPosition != _packageScroll) {
          _packageScroll = scroller.scrollPosition;
          GUIUtility.keyboardControl = 0;
          if (EditorWindow.HasOpenInstances<VersionPopupWindow>()) {
            EditorWindow.GetWindow<VersionPopupWindow>().Close();
            EditorWindow.GetWindow<ProtokitBrowser>().Focus();
          }
        }

        if (!state.DatabaseRequest.IsCompleted) {
          BrowserUtil.LoadingBarOnGUI();
        }

        if (_filteredPackages.Count == 1) {
          state.ExpandedPackageName = _filteredPackages[0].PackageName;
        }

        EditorGUI.BeginDisabledGroup(state.IsBusy);
        foreach (var result in _filteredPackages) {
          packagePanel.OnGUI(state, result, _packageScroll, containerSize);
        }
        EditorGUI.EndDisabledGroup();
      }
      GUILayout.EndHorizontal();
    }
  }
}
