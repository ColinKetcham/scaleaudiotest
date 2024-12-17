// (c) Meta Platforms, Inc. and affiliates. Confidential and proprietary.

using System;
using System.IO;
using UnityEngine;
using UnityEditor;

namespace Protokit.Browser {

  [Serializable]
  public class HeaderGUIContent {

    private const string SEARCH_FIELD_CONTROL_ID = "ProtokitSearchField";

    [NonSerialized]
    private string _searchText;
    public string SearchText => _searchText;

    [NonSerialized]
    private GUIContent _refreshContent;
    [NonSerialized]
    private GUIContent _moreContent;

    public void OnGUI(BrowserState state, Action onRefresh) {
      if (_refreshContent == null) {
        _refreshContent = EditorGUIUtility.IconContent("Refresh");
      }

      if (_moreContent == null) {
        _moreContent = EditorGUIUtility.IconContent("_Menu");
      }

      using (new GUILayout.HorizontalScope(BrowserStyles.HeaderBackground)) {
        if (GUILayout.Button(GUIContent.none, BrowserStyles.ProtokitLogo)) {
          state.SearchParameters.ResetToDefault();
        }

        GUI.SetNextControlName(SEARCH_FIELD_CONTROL_ID);
        _searchText = GUILayout.TextField(_searchText);

        //Unity doesn't provide enter-to-submit behavior automatically, so we infer through
        //the use of events and which control is currently focused.
        string focusedControl = GUI.GetNameOfFocusedControl();
        if (focusedControl == SEARCH_FIELD_CONTROL_ID && (Event.current.keyCode == KeyCode.Return || Event.current.keyCode == KeyCode.KeypadEnter)) {
          if (!string.IsNullOrWhiteSpace(_searchText)) {
            state.SearchParameters.AddSearchTerm(_searchText);
          }

          //Reset keyboard control to force input field to lose focus
          GUIUtility.keyboardControl = 0;
          //Remember to clear search text
          _searchText = "";
        }

        if (GUILayout.Button(_refreshContent, BrowserUtil.NoExpandWidth)) {
          onRefresh?.Invoke();
        }

        if (ProtokitBrowserFeatureFlags.ShowMoreButtonInHeader) {
          if (GUILayout.Button(_moreContent, BrowserUtil.NoExpandWidth)) {
            GenericMenu menu = new GenericMenu();
            menu.AddItem(new GUIContent("Add Local Package"), on: false, InstallLocalPackage);

            menu.AddItem(new GUIContent("Queue Actions"), on: ProtokitBrowserProjectSettings.DefaultActionIsQueue, () => {
              ProtokitBrowserProjectSettings.DefaultActionIsQueue = !ProtokitBrowserProjectSettings.DefaultActionIsQueue;
            });

            //Add separator between the more common operations and the debugging operations
            menu.AddSeparator("");

            menu.AddItem(new GUIContent("Report An Issue"), on: false, ReportAnIssue);

            menu.AddItem(new GUIContent("Verbose Logging"), on: Logging.Verbose, () => {
              Logging.Verbose = !Logging.Verbose;
            });

            menu.AddItem(new GUIContent("Show Cache Folder"), on: false, () => {
              EditorUtility.RevealInFinder(Caching.DeviceCacheFolder);
            });

            menu.AddItem(new GUIContent("Reset UI State"), on: false, () => {
              ProtokitBrowser.ResetBrowserState();
            });

            menu.ShowAsContext();
          }
        }
      }
    }

    private void InstallLocalPackage() {
      string path = EditorUtility.OpenFilePanel("Select package.json of Package to Install", "", "json");
      if (!string.IsNullOrWhiteSpace(path) && File.Exists(path)) {
        PackageDatabase.InstallLocal(path);
      }
    }

    private void ReportAnIssue() {
      Application.OpenURL("https://www.internalfb.com/butterfly/form/1392002801190013");
    }
  }
}
