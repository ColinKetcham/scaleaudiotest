// (c) Meta Platforms, Inc. and affiliates. Confidential and proprietary.

using System;
using System.Linq;
using UnityEngine;

namespace Protokit.Browser {

  [Serializable]
  public class FilterGUIContent {

    private const int KEYWORDS_TO_SHOW_IN_PARTIAL_VIEW = 8;

    [SerializeField]
    private Vector2 _filterScroll;

    private GUIContent _upgradeAvailableContent = new GUIContent("Upgrade Available");
    private GUIContent _onlyInstalledContent = new GUIContent("Only Installed");
    private GUIContent _directDependenciesContent = new GUIContent("Direct Dependencies");
    private GUIContent _indirectDependenciesContent = new GUIContent("Indirect Dependencies");
    private GUIContent _withExamplesContent = new GUIContent("With Examples");
    private GUIContent _showUnsupportedContent = new GUIContent("Show Unsupported");

    public void OnGUI(BrowserState state) {
      using (new GUILayout.VerticalScope())
      using (var scroller = new GUILayout.ScrollViewScope(_filterScroll)) {
        _filterScroll = scroller.scrollPosition;

        GUILayout.Label("Filter Packages", BrowserStyles.FilterHeader);
        state.SearchParameters.UpgradeAvailable = GUILayout.Toggle(state.SearchParameters.UpgradeAvailable, _upgradeAvailableContent, BrowserStyles.FilterToggle);
        state.SearchParameters.ContainsExamples = GUILayout.Toggle(state.SearchParameters.ContainsExamples, _withExamplesContent, BrowserStyles.FilterToggle);

        if (ProtokitBrowserFeatureFlags.SupportedPackagesFilter) {
          //Only show the option to the user if the flag is enabled
          state.SearchParameters.ShowUnsupported = GUILayout.Toggle(state.SearchParameters.ShowUnsupported, _showUnsupportedContent, BrowserStyles.FilterToggle);
        } else {
          //Otherwise always disable the option so that all packages are visible
          state.SearchParameters.ShowUnsupported = true;
        }

        GUILayout.Box("", BrowserStyles.FilterSeparator);

        DrawFilterGroup(state, SearchFilterType.Source);

        if (ProtokitBrowserFeatureFlags.ShowCategoryFilter) {
          DrawFilterGroup(state, SearchFilterType.Category);
        }

        DrawFilterGroup(state, SearchFilterType.Tags);
        DrawFilterGroup(state, SearchFilterType.Author);
      }
    }

    private void DrawFilterGroup(BrowserState state, SearchFilterType filterType) {
      bool isOpen = state.SearchParameters[filterType].State != SearchFilterState.Closed;

      using (new GUILayout.HorizontalScope()) {
        var newIsOpen = GUILayout.Toggle(isOpen, $"By {state.SearchParameters[filterType].Title}", BrowserStyles.FilterFoldout);
        if (newIsOpen != isOpen) {
          state.SearchParameters[filterType].State = newIsOpen ? SearchFilterState.Open_Partial : SearchFilterState.Closed;
        }
      }

      if (isOpen) {
        var keywords = state.SearchParameters[filterType].Keywords;
        bool isFullView = state.SearchParameters[filterType].State == SearchFilterState.Open_Full;
        for (int i = 0; i < keywords.Count; i++) {
          if (!isFullView && i >= KEYWORDS_TO_SHOW_IN_PARTIAL_VIEW) {
            break;
          }

          var keyword = keywords[i];
          //Hide empty keywords, but never if they are currently checked
          if (keyword.TotalMatches == 0 && !keyword.IsEnabled) {
            continue;
          }

          bool shouldBeEnabled = GUILayout.Toggle(keyword.IsEnabled, $"{keyword.Display} ({keyword.TotalMatches})", BrowserStyles.FilterToggle);
          if (shouldBeEnabled != keyword.IsEnabled) {
            state.SearchParameters[filterType].SetKeywordEnabled(keyword.Id, shouldBeEnabled);
          }
        }

        if (!isFullView && keywords.Count > KEYWORDS_TO_SHOW_IN_PARTIAL_VIEW) {
          if (GUILayout.Button($"Show all ({keywords.Count})", BrowserStyles.FilterViewAll)) {
            state.SearchParameters[filterType].State = SearchFilterState.Open_Full;
          }
        }
      }

      GUILayout.Box("", BrowserStyles.FilterSeparator);
    }
  }
}
