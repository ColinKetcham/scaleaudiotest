// (c) Meta Platforms, Inc. and affiliates. Confidential and proprietary.

using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Assertions;
using UPackageInfo = UnityEditor.PackageManager.PackageInfo;
using UPackageSource = UnityEditor.PackageManager.PackageSource;

namespace Protokit.Browser {

  [Serializable]
  public class DatabaseRequestResult {

    public PackageInfo[] Packages => _packages;
    public Dictionary<string, InstallInfo> Dependencies {
      get {
        if (_installedPackagesDictionary == null) {
          _installedPackagesDictionary = new Dictionary<string, InstallInfo>();
          Assert.AreEqual(_installedPackagesNames.Length, _installedPackagesInfo.Length);
          for (int i = 0; i < _installedPackagesNames.Length; i++) {
            _installedPackagesDictionary[_installedPackagesNames[i]] = _installedPackagesInfo[i];
          }
        }
        return _installedPackagesDictionary;
      }
    }

    public Dictionary<string, PackageInfo> PackagesByName {
      get {
        if (_packagesByName == null) {
          _packagesByName = new Dictionary<string, PackageInfo>();
          foreach (var package in _packages) {
            _packagesByName[package.PackageName] = package;
          }
        }
        return _packagesByName;
      }
    }

    //We store all database info in simple serializable arrays for easy Json serialization
    //using Unity's JsonUtility
    [SerializeField]
    private PackageInfo[] _packages;
    [SerializeField]
    private string[] _installedPackagesNames;
    [SerializeField]
    private InstallInfo[] _installedPackagesInfo;

    //Installed information is serialized in arrays, but accessed through a dictionary
    //that is loaded lazily and stored in this variable
    private Dictionary<string, InstallInfo> _installedPackagesDictionary;

    private Dictionary<string, PackageInfo> _packagesByName;

    private Dictionary<string, HashSet<string>> _packageToAllFirstLevelDependencies;

    public DatabaseRequestResult(List<PackageInfo> packages, List<UPackageInfo> installed) {
      _packages = packages.ToArray();
      if (_packages.Any(p => p.Versions.Any(v => v == null))) {
        throw new NullReferenceException("Expected all versions to be non-null to construct a DatabaseRequestResult.");
      }

      _installedPackagesNames = installed.Select(i => i.name).ToArray();
      _installedPackagesInfo = installed.Select(i => new InstallInfo(i)).ToArray();
    }

    public bool IsDirectDependency(string packageName) {
      return Dependencies.TryGetValue(packageName, out var info) && info.InstallSource == PackageSource.DirectDependency;
    }

    public bool IsIndirectDependency(string packageName) {
      return Dependencies.TryGetValue(packageName, out var info) && info.InstallSource == PackageSource.IndirectDependency;
    }

    public bool IsPackageInstalled(string packageName) {
      return Dependencies.ContainsKey(packageName);
    }

    public bool IsUpgradeAvailable(PackageInfo package) {
      return Dependencies.TryGetValue(package.PackageName, out var info) && !string.IsNullOrEmpty(info.UpgradeVersion);
    }

    public bool TryGetInstalledVersion(string packageName, out string version) {
      if (Dependencies.TryGetValue(packageName, out var info)) {
        version = info.InstalledVersion;
        return true;
      } else {
        version = null;
        return false;
      }
    }

    public bool TryGetInstallSource(string packageName, out PackageSource source) {
      if (Dependencies.TryGetValue(packageName, out var info)) {
        source = info.InstallSource;
        return true;
      } else {
        source = default;
        return false;
      }
    }

    /// <summary>
    /// Returns a list of all direct dependencies across all published versions of a package.  Useful
    /// usually only for conservative dependency calculations.
    /// </summary>
    public HashSet<string> GetAllDirectLevelDependencies(string packageName) {
      if (!PackagesByName.TryGetValue(packageName, out var package)) {
        throw new ArgumentException($"Could not find a package with the name {packageName}");
      }

      if (_packageToAllFirstLevelDependencies == null) {
        _packageToAllFirstLevelDependencies = new Dictionary<string, HashSet<string>>();
      }

      if (!_packageToAllFirstLevelDependencies.TryGetValue(packageName, out var set)) {
        set = new HashSet<string>(package.Versions.SelectMany(v => v.Dependencies.Select(d => d.Name)));
        _packageToAllFirstLevelDependencies[packageName] = set;
      }

      return set;
    }

    /// <summary>
    /// Returns a conservative list of all dependencies of a given package, excluding version information.
    /// Returned packages will always include all packages that *could* be installed as a direct or indirect
    /// dependency, but may include packages that would *not* be installed for a *specific* version of a
    /// package.
    /// </summary>
    public HashSet<string> GetConservativeProtoKitDependencyList(string packageName) {
      var result = new HashSet<string>();

      void Visit(string dependencyName) {
        //We ignore dependencies we can't find inside ProtoKit
        //This could either be a broken package, or a dependency on another valid scope.
        if (!PackagesByName.ContainsKey(dependencyName)) {
          return;
        }

        foreach (var dependency in GetAllDirectLevelDependencies(dependencyName)) {
          if (result.Contains(dependency)) {
            continue;
          }

          result.Add(dependency);
          Visit(dependency);
        }
      }

      Visit(packageName);

      return result;
    }

    public HashSet<string> GetConservativeProtoKitNamespaceList(string packageName) {
      return new HashSet<string>(GetConservativeProtoKitDependencyList(packageName).
                                 Concat(new string[] { packageName }).
                                 Select(d => string.Join(".", d.Split('.').
                                                                Take(2))));
    }

    [Serializable]
    public struct InstallInfo {
      public string InstalledVersion;
      public string UpgradeVersion;
      public PackageSource InstallSource;

      public InstallInfo(UPackageInfo info) {
        InstalledVersion = info.version;
        UpgradeVersion = CalculateUpgradeVersion(info);

        switch (info.source) {
          case UPackageSource.Embedded:
            InstallSource = PackageSource.Embedded;
            break;
          case UPackageSource.Local:
          case UPackageSource.LocalTarball:
            InstallSource = PackageSource.Local;
            break;
          default:
            InstallSource = info.isDirectDependency ? PackageSource.DirectDependency : PackageSource.IndirectDependency;
            break;
        }
      }

      private static string CalculateUpgradeVersion(UPackageInfo info) {
        //No upgrade if installed from any of these sources
        switch (info.source) {
          case UPackageSource.Local:
          case UPackageSource.LocalTarball:
          case UPackageSource.Embedded:
          case UPackageSource.Unknown:
            return null;
        }

        //If the installed version is the verified version, never upgrade
        if (info.version == info.versions.verified) {
          return null;
        }

        var compatibleVersions = info.versions.compatible;

        //First check to see if we can upgrade to the verified version
        if (!string.IsNullOrEmpty(info.versions.verified) &&
            Array.IndexOf(compatibleVersions, info.versions.verified) > Array.IndexOf(compatibleVersions, info.version)) {
          return info.versions.verified;
        }

        bool isInstalledInPreview = info.version.Contains("-");

        //Then we look for the newest version that is compatible
        for (int i = compatibleVersions.Length - 1; i >= 0; i--) {
          var version = compatibleVersions[i];

          //If we have checked all newer versions without finding a match, we 
          //stop checking and report that there is no valid version to upgrade to
          if (version == info.version) {
            return null;
          }

          bool isVersionInPreview = version.Contains("-");

          if (isVersionInPreview) {
            //If the version is a preview version, only accept as an upgrade
            //if the installed version is also in preview
            if (isInstalledInPreview) {
              return version;
            }
          } else {
            //Otherwise, if the version is not a preview version, always
            //accept it as a valid upgrade
            return version;
          }
        }

        return null;
      }
    }
  }
}
