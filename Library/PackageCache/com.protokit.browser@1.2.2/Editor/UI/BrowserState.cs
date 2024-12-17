// (c) Meta Platforms, Inc. and affiliates. Confidential and proprietary.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Protokit.Browser {

  /// <summary>
  /// A container class that contains some basic state that is core to the Browser
  /// UI.  This is placed in a container so that it can be better passed around to
  /// various classes that need access.  Furthermore it is also serialized and saved
  /// so that when you restore the Browser window it returns to a previous state.
  /// </summary>
  [Serializable]
  public class BrowserState {

    /// <summary>
    /// The search parameters instance used by the browser window.
    /// </summary>
    public SearchParameters SearchParameters = new SearchParameters();

    /// <summary>
    /// The name of the current package that is expanded by the user.  Only
    /// one package can be expanded at a time.
    /// </summary>
    public string ExpandedPackageName = "";

    /// <summary>
    /// The current database request that is outgoing or completed, the
    /// browser instance manages this database request and uses it for
    /// all package display.
    /// </summary>
    [NonSerialized]
    public Task<DatabaseRequestResult> DatabaseRequest;

    /// <summary>
    /// A flag that represents whether or not the Browser is currently in
    /// a 'busy' state.  Various parts of the UI can use this to disable
    /// or hide elements while the Browser is busy.
    /// </summary>
    [NonSerialized]
    public bool IsBusy;

    [NonSerialized]
    public Dictionary<string, string> PackagesToInstall = new Dictionary<string, string>();

    [NonSerialized]
    public HashSet<string> PackagesToUninstall = new HashSet<string>();

    public bool AnyPendingChanges => PackagesToInstall.Count != 0 || PackagesToUninstall.Count != 0;

    public void QueuePackageInstall(string package, string version) {
      PackagesToUninstall.Remove(package);
      PackagesToInstall[package] = version;
    }

    public void QueuePackageUninstall(string package) {
      PackagesToInstall.Remove(package);
      PackagesToUninstall.Add(package);
    }
  }
}
