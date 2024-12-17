// (c) Meta Platforms, Inc. and affiliates. Confidential and proprietary.

using System;
using System.Collections.Generic;
using UnityEngine;

namespace Protokit.Browser {

  [Serializable]
  public class SearchFilter : ISerializationCallbackReceiver {

    public SearchFilterType Type;
    public List<SearchKeyword> Keywords = new List<SearchKeyword>();
    public SearchFilterState State = SearchFilterState.Closed;

    public long ModificationCounter { get; private set; }

    public bool AnyKeywordsEnabled {
      get {
        if (Keywords != null) {
          for (int i = 0; i < Keywords.Count; i++) {
            if (Keywords[i].IsEnabled) {
              return true;
            }
          }
        }
        return false;
      }
    }

    public string Title => Type.ToString();

    public SearchFilter(SearchFilterType type) {
      Type = type;
    }

    public SearchFilter(SearchFilterType type, params string[] tags) {
      Type = type;
      foreach (var tag in tags) {
        Keywords.Add(new SearchKeyword(tag, tag, false, 0));
      }
    }

    public void ResetKeywordMatchCounts() {
      ModificationCounter++;

      for (int i = 0; i < Keywords.Count; i++) {
        var tag = Keywords[i];
        tag.TotalMatches = 0;
        Keywords[i] = tag;
      }
    }

    public void IncrementKeywordMatchCount(string keywordId, string name) {
      ModificationCounter++;

      for (int i = 0; i < Keywords.Count; i++) {
        SearchKeyword keyword = Keywords[i];
        if (keyword.Id == keywordId) {
          keyword.TotalMatches++;
          Keywords[i] = keyword;
          return;
        }
      }

      Keywords.Add(new SearchKeyword(keywordId, name, false, 1));
    }

    public bool SetKeywordEnabled(string keywordId, bool isEnabled) {
      ModificationCounter++;

      for (int i = 0; i < Keywords.Count; i++) {
        var tag = Keywords[i];
        if (tag.Id == keywordId && tag.IsEnabled != isEnabled) {
          tag.IsEnabled = isEnabled;
          Keywords[i] = tag;
          return true;
        }
      }

      return false;
    }

    public void DisableAllKeywords() {
      ModificationCounter++;

      for (int i = 0; i < Keywords.Count; i++) {
        var tag = Keywords[i];
        tag.IsEnabled = false;
        Keywords[i] = tag;
      }
    }

    private static HashSet<string> _tmpSet = new HashSet<string>();
    public bool DoesMatch(DatabaseRequestResult database, PackageInfo package) {
      _tmpSet.Clear();

      switch (Type) {
        case SearchFilterType.Tags:
          if (package.Default.Keywords != null) {
            _tmpSet.UnionWith(package.Default.Keywords);
          }
          break;
        case SearchFilterType.Author:
          if (!string.IsNullOrWhiteSpace(package.Default.Author)) {
            _tmpSet.Add(package.Default.Author);
          }
          break;
        case SearchFilterType.Category:
          if (!string.IsNullOrWhiteSpace(package.Default.Category)) {
            _tmpSet.Add(package.Default.Category);
          }
          break;
        case SearchFilterType.Source:
          if (database.TryGetInstallSource(package.PackageName, out var source)) {
            _tmpSet.Add(source.ToString());
          }
          break;
      }

      bool anyEnabled = false;
      for (int i = 0; i < Keywords.Count; i++) {
        var tag = Keywords[i];
        if (!tag.IsEnabled) {
          continue;
        }

        anyEnabled = true;
        if (_tmpSet.Contains(tag.Id)) {
          return true;
        }
      }

      //If no keyword is enabled, the filter always trivially passes.  It's
      //only once at least one keyword gets enabled that filtering can begin
      if (!anyEnabled) {
        return true;
      }

      return false;
    }

    public void OnBeforeSerialize() { }

    public void OnAfterDeserialize() {
      //Unity serialization can sometimes result in keywords being null, fix here
      if (Keywords == null) {
        Keywords = new List<SearchKeyword>();
      }
    }
  }
}
