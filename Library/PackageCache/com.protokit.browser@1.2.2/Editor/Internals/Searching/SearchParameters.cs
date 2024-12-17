// (c) Meta Platforms, Inc. and affiliates. Confidential and proprietary.

using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;

namespace Protokit.Browser {

  /// <summary>
  /// A container that holds all of the parameters needed to search the package
  /// list.  It doesn't perform any actions itself, but simply provides the data
  /// and methods needed to do the filtering.
  /// </summary>
  [Serializable]
  public class SearchParameters {

    public const bool DEFAULT_ONLY_INSTALLED = false;
    public const bool DEFAULT_ONLY_DIRECT = false;
    public const bool DEFAULT_ONLY_INDIRECT = false;
    public const bool DEFAULT_UPGRADE_AVAILABLE = false;
    public const bool DEFAULT_SHOW_UNSUPPORTED = false;
    public const bool DEFAULT_CONTAINS_EXAMPLES = false;
    public const SearchSortMethod DEFAULT_SORT_METHOD = SearchSortMethod.SortByTitle;

    [SerializeField]
    private List<SearchFilter> _filters = new List<SearchFilter>() {
        new SearchFilter(SearchFilterType.Category),
        new SearchFilter(SearchFilterType.Tags),
        new SearchFilter(SearchFilterType.Author),
        new SearchFilter(SearchFilterType.Source)
    };

    public SearchFilter this[SearchFilterType type] {
      get {
        return _filters[(int)type];
      }
    }

    [SerializeField]
    private List<string> _searchTerms = new List<string>();

    /// <summary>
    /// Specifies the kind of data the packages should be sorted by.
    /// </summary>
    public SearchSortMethod SortMethod {
      get => _sortMethod;
      set {
        if (value != _sortMethod) {
          _modificationCounter++;
          _sortMethod = value;
        }
      }
    }
    [SerializeField]
    private SearchSortMethod _sortMethod = DEFAULT_SORT_METHOD;

    /// <summary>
    /// Restricts the package list to only show packages that are currently
    /// installed into the project, either as a direct or indirect dependency.
    /// </summary>
    public bool OnlyInstalled {
      get => _onlyInstalled;
      set {
        if (_onlyInstalled != value) {
          _modificationCounter++;
          _onlyInstalled = value;
        }
      }
    }
    [SerializeField]
    private bool _onlyInstalled = DEFAULT_ONLY_INSTALLED;

    /// <summary>
    /// Restricts the package list to only show packages that are direct
    /// dependencies of this project.
    /// </summary>
    public bool OnlyDirect {
      get => _onlyDirect;
      set {
        if (_onlyDirect != value) {
          _modificationCounter++;
          _onlyDirect = value;
        }
      }
    }
    [SerializeField]
    private bool _onlyDirect = DEFAULT_ONLY_DIRECT;

    /// <summary>
    /// Restricts the package list to only show packages that are indirect
    /// dependencies of this project.
    /// </summary>
    public bool OnlyIndirect {
      get => _onlyIndirect;
      set {
        if (_onlyIndirect != value) {
          _modificationCounter++;
          _onlyIndirect = value;
        }
      }
    }
    [SerializeField]
    private bool _onlyIndirect = DEFAULT_ONLY_INDIRECT;

    /// <summary>
    /// Restricts the package list to only show packages that are installed
    /// and have a newer version available.
    /// </summary>
    public bool UpgradeAvailable {
      get => _upgradeAvailable;
      set {
        if (_upgradeAvailable != value) {
          _modificationCounter++;
          _upgradeAvailable = value;
        }
      }
    }
    [SerializeField]
    private bool _upgradeAvailable = DEFAULT_UPGRADE_AVAILABLE;

    /// <summary>
    /// Allows packages marked as unsupported to show in the list.
    /// </summary>
    public bool ShowUnsupported {
      get => _showUnsupported;
      set {
        if (_showUnsupported != value) {
          _showUnsupported = value;
          _modificationCounter++;
        }
      }
    }
    [SerializeField]
    private bool _showUnsupported = DEFAULT_SHOW_UNSUPPORTED;

    /// <summary>
    /// Restricts the package list to only include packages that come with
    /// example assets or scenes.
    /// </summary>
    public bool ContainsExamples {
      get => _containsExamples;
      set {
        if (_containsExamples != value) {
          _containsExamples = value;
          _modificationCounter++;
        }
      }
    }
    [SerializeField]
    private bool _containsExamples = DEFAULT_CONTAINS_EXAMPLES;

    /// <summary>
    /// The UI can sometimes treat the sort method as a tag that can be removed, but
    /// not always.  This flag decides whether or not to treat the sort method as a tag.
    /// </summary>
    public bool IsSortMethodATag {
      get => _isSortMethodATag;
      set {
        if (value != _isSortMethodATag) {
          _modificationCounter++;
          _isSortMethodATag = value;
        }
      }
    }
    [SerializeField]
    private bool _isSortMethodATag = false;

    /// <summary>
    /// This modification counter is incremented whenever there is any change to the 
    /// search parameters.  You can use it to decide whether or not you need to do
    /// any work, or if you can re-use previously calculated results because they are
    /// still valid.
    /// </summary>
    public long ModificationCounter {
      get {
        long result = _modificationCounter;
        foreach (var filter in _filters) {
          result += filter.ModificationCounter;
        }
        return result;
      }
    }
    private long _modificationCounter = 0;

    [NonSerialized]
    private List<SearchTag> _activeTagsCache = new List<SearchTag>();
    [NonSerialized]
    private long _activeTagsCacheLastRefreshedCounter = -1;

    /// <summary>
    /// Gets a list of currently active search tags.  Each search tag has a name
    /// and a method that can be invoked to remove the tag.  Search tags are a
    /// way to visualize current search restrictions in a way that can easily
    /// be reversed or undone.
    /// </summary>
    public IReadOnlyList<SearchTag> ActiveTags {
      get {
        //we use the search parameters own modification counter to rebuild the active tags
        //cache only when needed
        if (_activeTagsCacheLastRefreshedCounter != ModificationCounter) {
          _activeTagsCacheLastRefreshedCounter = ModificationCounter;

          _activeTagsCache.Clear();
          if (_isSortMethodATag) {
            _activeTagsCache.Add(new SearchTag(_sortMethod.ToString(), () => IsSortMethodATag = false));
          }

          if (_searchTerms.Count != 0) {
            _activeTagsCache.AddRange(_searchTerms.Select(t => new SearchTag(t, () => RemoveSearchTerm(t))));
          }

          if (_onlyInstalled) {
            _activeTagsCache.Add(new SearchTag("Installed", () => OnlyInstalled = false));
          }

          if (_onlyDirect) {
            _activeTagsCache.Add(new SearchTag("Direct", () => OnlyDirect = false));
          }

          if (_onlyIndirect) {
            _activeTagsCache.Add(new SearchTag("Indirect", () => OnlyIndirect = false));
          }

          if (_upgradeAvailable) {
            _activeTagsCache.Add(new SearchTag("Upgrade", () => UpgradeAvailable = false));
          }

          if (_containsExamples) {
            _activeTagsCache.Add(new SearchTag("Examples", () => ContainsExamples = false));
          }

          foreach (var filter in _filters) {
            foreach (var keyword in filter.Keywords) {
              if (keyword.IsEnabled) {
                _activeTagsCache.Add(new SearchTag(keyword.Display, () => filter.SetKeywordEnabled(keyword.Id, false)));
              }
            }
          }
        }

        return _activeTagsCache;
      }
    }

    /// <summary>
    /// Resets the package search parameters to its default state.  This operation will
    /// increment the modification counter.
    /// </summary>
    public void ResetToDefault() {
      _modificationCounter++;

      _searchTerms.Clear();

      foreach (var filter in _filters) {
        filter.DisableAllKeywords();
      }

      _sortMethod = DEFAULT_SORT_METHOD;
      _onlyInstalled = DEFAULT_ONLY_INSTALLED;
      _onlyDirect = DEFAULT_ONLY_DIRECT;
      _onlyIndirect = DEFAULT_ONLY_INDIRECT;
      _upgradeAvailable = DEFAULT_UPGRADE_AVAILABLE;
      _containsExamples = DEFAULT_CONTAINS_EXAMPLES;
      _showUnsupported = DEFAULT_SHOW_UNSUPPORTED;
    }

    /// <summary>
    /// Adds a new search term to the search parameters.  Only packages that match
    /// search terms will be listed.  A package matches the search terms if:
    ///   it soft-matches ALL search terms
    ///   OR
    ///   it exact-matches ANY search term
    /// </summary>
    public void AddSearchTerm(string term) {
      term = term.ToLower();
      if (!_searchTerms.Contains(term)) {
        _searchTerms.Add(term);
        _modificationCounter++;
      }
    }

    /// <summary>
    /// Removes a given search term from the search parameters.
    /// </summary>
    public void RemoveSearchTerm(string term) {
      term = term.ToLower();
      if (_searchTerms.Remove(term)) {
        _modificationCounter++;
      }
    }

    /// <summary>
    /// Removes all search terms from the search parameters.
    /// </summary>
    public void ClearSearchTerms() {
      if (_searchTerms.Count > 0) {
        _searchTerms.Clear();
        _modificationCounter++;
      }
    }


    /// <summary>
    /// The search parameters structure needs to be updated any time a new database result
    /// becomes available.  This allows it to populate filters or other structures with
    /// relevant data for searching the available packages.  This method avoids re-doing
    /// work when presented with an unchanged database result.
    /// </summary>
    public void UpdateFiltersUsingPackageList(DatabaseRequestResult database) {
      if (database == _prevFilterUpdateDatabase) {
        return;
      }
      _prevFilterUpdateDatabase = database;

      var packages = database.Packages;

      _modificationCounter++;

      foreach (var filter in _filters) {
        filter.ResetKeywordMatchCounts();
      }

      foreach (var package in packages) {
        if (database.TryGetInstallSource(package.PackageName, out var source)) {
          this[SearchFilterType.Source].IncrementKeywordMatchCount(source.ToString(), source.ToString());
        }
      }
      foreach (var category in packages.Select(p => p.Default.Category)) {
        if (string.IsNullOrEmpty(category)) {
          continue;
        }
        this[SearchFilterType.Category].IncrementKeywordMatchCount(category, category);
      }
      foreach (var keyword in packages.SelectMany(p => p.Default.Keywords)) {
        if (string.IsNullOrEmpty(keyword)) {
          continue;
        }
        this[SearchFilterType.Tags].IncrementKeywordMatchCount(keyword, keyword);
      }
      foreach (var author in packages.Select(p => p.Default.Author)) {
        if (string.IsNullOrEmpty(author)) {
          continue;
        }
        this[SearchFilterType.Author].IncrementKeywordMatchCount(author, author);
      }

      foreach (var filter in _filters) {
        switch (filter.Type) {
          case SearchFilterType.Source:
            break;
          default:
            filter.Keywords.Sort((a, b) => {
              int byRef = b.TotalMatches.CompareTo(a.TotalMatches);
              if (byRef == 0) {
                return a.Id.CompareTo(b.Id);
              } else {
                return byRef;
              }
            });
            break;
        }
      }
    }
    [NonSerialized]
    private DatabaseRequestResult _prevFilterUpdateDatabase;

    /// <summary>
    /// Returns whether or not the given package matches the current search parameters.
    /// Searching is done with the provided database results, and always uses the default
    /// version of the provided package.  Default version is used to ensure package list
    /// is consistent.
    /// </summary>
    public bool DoesMatch(DatabaseRequestResult database, PackageInfo package) {
      foreach (var term in _searchTerms) {
        string titleLower = package.Default.Title.ToLower();
        string nameLower = package.PackageName.ToLower();

        //Handle perfect matches first, they override everything else
        if (titleLower == term) return true;
        if (nameLower == term) return true;

        bool matchTitle = titleLower.Contains(term);
        bool matchName = nameLower.Contains(term);
        bool matchTag = package.Default.Keywords.Any(k => k.ToLower().Contains(term));
        //TODO: match description?

        //Package must match in one of three ways to not get rejected
        if (!matchTitle && !matchName && !matchTag) {
          return false;
        }
      }

      if (_onlyInstalled && !database.IsPackageInstalled(package.PackageName)) {
        return false;
      }

      if (_onlyIndirect && !database.IsIndirectDependency(package.PackageName)) {
        return false;
      }

      if (_onlyDirect && !database.IsDirectDependency(package.PackageName)) {
        return false;
      }

      if (_containsExamples && !package.Default.HasSamples) {
        return false;
      }

      //If we are restricted to only show supported packages, return false for any package that is not marked as supported
      //Installed packages are never rejected by this filter
      if (!_showUnsupported &&
          !package.Default.IsSupported &&
          !database.IsPackageInstalled(package.PackageName)) {
        return false;
      }

      if (_upgradeAvailable && !database.IsUpgradeAvailable(package)) {
        return false;
      }

      for (int i = 0; i < _filters.Count; i++) {
        if (!_filters[i].DoesMatch(database, package)) {
          return false;
        }
      }

      return true;
    }
  }
}
