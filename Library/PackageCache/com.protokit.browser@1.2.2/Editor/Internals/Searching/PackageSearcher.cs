// (c) Meta Platforms, Inc. and affiliates. Confidential and proprietary.

using System.Collections.Generic;

namespace Protokit.Browser {

  /// <summary>
  /// An instance of a package searcher can be used to filter a database request into
  /// a list of packages using provided search parameters.  The searcher is stateful and
  /// avoids re-doing work, you can create an instance of a searcher per-context so that
  /// each context can efficiently respond to updates.
  /// </summary>
  public class PackageSearcher {

    private DatabaseRequestResult _prevRequest;
    private SearchParameters _prevParams;
    private List<PackageInfo> _prevResults = new List<PackageInfo>();
    private long _prevParamsModificationNumber;

    /// <summary>
    /// Given a database request and search parameters, output the filtered packages
    /// into the provided list.  The provided list is always cleared and then filled
    /// with results.
    /// </summary>
    public void FilterPackages(DatabaseRequestResult database,
                               SearchParameters parameters,
                               List<PackageInfo> outputList) {
      if (_prevParams != parameters ||
          _prevRequest != database ||
          parameters.ModificationCounter != _prevParamsModificationNumber) {
        _prevResults.Clear();
        _prevParams = parameters;
        _prevRequest = database;
        _prevParamsModificationNumber = parameters.ModificationCounter;

        foreach (var item in _prevRequest.Packages) {
          if (parameters.DoesMatch(database, item)) {
            _prevResults.Add(item);
          }
        }

        switch (parameters.SortMethod) {
          case SearchSortMethod.SortByTitle:
            _prevResults.Sort(OrderByTitle);
            break;
          case SearchSortMethod.SortByUpdatedDate:
            _prevResults.Sort(OrderByUpdatedDate);
            break;
          case SearchSortMethod.SortByRecentlyInstalled:
            _prevResults.Sort(OrderByRecentlyInstalled);
            break;
        }
      }

      outputList.Clear();
      outputList.AddRange(_prevResults);
    }

    private int OrderByTitle(PackageInfo a, PackageInfo b) {
      return a.Default.Title.CompareTo(b.Default.Title);
    }

    private int OrderByUpdatedDate(PackageInfo a, PackageInfo b) {
      return b.Default.DatePublished.CompareTo(a.Default.DatePublished);
    }

    private int OrderByRecentlyInstalled(PackageInfo a, PackageInfo b) {
      long installIndexA = PackageInstallTracker.GetPackageInstallOrdering(a.PackageName);
      long installIndexB = PackageInstallTracker.GetPackageInstallOrdering(b.PackageName);
      if (installIndexA != installIndexB) {
        //Reverse B and A, we want largest install orderings to come first
        return installIndexB.CompareTo(installIndexA);
      }

      //After install ordering, we then sort by whether or not the package is installed
      //currently
      bool isInstalledA = _prevRequest.IsPackageInstalled(a.PackageName);
      bool isInstalledB = _prevRequest.IsPackageInstalled(b.PackageName);
      if (isInstalledA != isInstalledB) {
        //Installed packages get ordered before uninstalled packages
        return isInstalledA ? -1 : 1;
      }

      //After that, we simply order by date published
      return OrderByUpdatedDate(a, b);
    }
  }
}
