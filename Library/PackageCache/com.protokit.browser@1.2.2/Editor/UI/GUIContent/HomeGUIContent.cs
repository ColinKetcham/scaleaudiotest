// (c) Meta Platforms, Inc. and affiliates. Confidential and proprietary.

using System;
using System.Linq;
using UnityEngine;
using UnityEditor;

namespace Protokit.Browser {

  [Serializable]
  public class HomeGUIContent {

    [SerializeField]
    private Vector2 _scroll;

    private static Color ShowAllTint => EditorGUIUtility.isProSkin ? new Color(1f, 1f, 1f) :
                                                                     new Color(0.85f, 0.85f, 0.85f);

    public void OnGUI(BrowserState state, PackageGUIContent packagePanel, Vector2 containerSize) {
      using (var scroller = new GUILayout.ScrollViewScope(_scroll)) {
        _scroll = scroller.scrollPosition;

        GUILayout.Space(9);

        if (!state.DatabaseRequest.IsCompleted) {
          BrowserUtil.LoadingBarOnGUI();
          return;
        }

        using (new EditorGUI.DisabledGroupScope(state.IsBusy)) {
          var updateAvailable = state.DatabaseRequest.Result.Packages.OrderByDescending(p => p.Default.DatePublished).
                                                                      Where(p => state.DatabaseRequest.Result.IsUpgradeAvailable(p)).
                                                                      Take(5);
          if (updateAvailable.Count() > 0) {
            DrawSortHeader("Upgrade Available", onShowMore: () => {
              state.SearchParameters.UpgradeAvailable = true;
            });
            GUILayout.Box("", BrowserStyles.FilterSeparator);
            foreach (var package in updateAvailable) {
              packagePanel.OnGUI(state, package, _scroll, containerSize);
            }
          }

          DrawSortHeaderWithSort("Newly Updated", SearchSortMethod.SortByUpdatedDate, state);
          GUILayout.Box("", BrowserStyles.FilterSeparator);
          foreach (var package in state.DatabaseRequest.Result.Packages.OrderByDescending(p => p.Default.DatePublished).
                                                                        ThenBy(p => state.DatabaseRequest.Result.IsPackageInstalled(p.PackageName)).
                                                                        Take(5)) {
            packagePanel.OnGUI(state, package, _scroll, containerSize);
          }

          DrawSortHeaderWithSort("Recently Installed", SearchSortMethod.SortByRecentlyInstalled, state);
          GUILayout.Box("", BrowserStyles.FilterSeparator);
          foreach (var package in state.DatabaseRequest.Result.Packages.OrderByDescending(p => PackageInstallTracker.GetPackageInstallOrdering(p.PackageName)).
                                                                        ThenBy(p => state.DatabaseRequest.Result.IsPackageInstalled(p.PackageName) ? 0 : 1).
                                                                        Take(5)) {
            packagePanel.OnGUI(state, package, _scroll, containerSize);
          }
        }
      }
    }

    private void DrawSortHeader(string header, Action onShowMore) {
      GUILayout.Space(15);

      using (new GUILayout.HorizontalScope()) {
        GUILayout.Label(header, BrowserStyles.LargeHeader, BrowserUtil.NoExpandWidth);

        GUILayout.FlexibleSpace();

        GUILayout.Space(20);
        GUI.color = ShowAllTint;
        if (GUILayout.Button(" Show More ", BrowserStyles.FilterViewAll, GUILayout.Height(17), GUILayout.ExpandWidth(false))) {
          onShowMore?.Invoke();
        }
        GUI.color = Color.white;

        GUILayout.Space(6);
      }
    }

    private void DrawSortHeaderWithSort(string header, SearchSortMethod method, BrowserState state) {
      DrawSortHeader(header, onShowMore: () => {
        state.SearchParameters.SortMethod = method;
        state.SearchParameters.IsSortMethodATag = true;
      });
    }
  }
}
