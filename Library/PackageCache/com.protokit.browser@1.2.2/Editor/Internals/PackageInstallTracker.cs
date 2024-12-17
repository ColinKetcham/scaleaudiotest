// (c) Meta Platforms, Inc. and affiliates. Confidential and proprietary.

using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor;
using UnityEditor.PackageManager.UI;
using UPackageInfo = UnityEditor.PackageManager.PackageInfo;

namespace Protokit.Browser {

  public class PackageInstallTracker : IPackageManagerExtension {

    private static readonly string InstallHistoryPath = Path.Combine(Caching.DeviceCacheFolder, "InstallHistory.json");

    private static Dictionary<string, long> _packageToInstallTimestamp = null;

    [InitializeOnLoadMethod]
    private static void Init() {
      PackageManagerExtensions.RegisterExtension(new PackageInstallTracker());
    }

    public static bool IsPackageInInstallHistory(string packageName) {
      if (!TryRefreshInfoFromDisk()) {
        return false;
      }

      return _packageToInstallTimestamp.ContainsKey(packageName);
    }

    /// <summary>
    /// Returns a number that defines an order relative to other packages.  Larger numbers
    /// represent packages that were installed more recently.
    /// </summary>
    public static long GetPackageInstallOrdering(string packageName) {
      if (!TryRefreshInfoFromDisk()) {
        return long.MinValue;
      }

      if (_packageToInstallTimestamp.TryGetValue(packageName, out var timestamp)) {
        return timestamp;
      } else {
        return long.MinValue;
      }
    }

    public static void RecordPackageInstall(string packageName) {
      if (!TryRefreshInfoFromDisk()) {
        return;
      }

      _packageToInstallTimestamp[packageName] = DateTime.Now.ToUniversalTime().Ticks;

      WriteChangesToDisk();
    }

    VisualElement IPackageManagerExtension.CreateExtensionUI() {
      return null;
    }

    void IPackageManagerExtension.OnPackageAddedOrUpdated(UPackageInfo packageInfo) {
      RecordPackageInstall(packageInfo.name);
    }

    void IPackageManagerExtension.OnPackageRemoved(UPackageInfo packageInfo) { }

    void IPackageManagerExtension.OnPackageSelectionChange(UPackageInfo packageInfo) { }

    private static bool TryRefreshInfoFromDisk() {
      //Don't do anything if the data has already been loaded, we assume that the file on disk
      //will only be changed by us, and so there is no need to re-load aggressively
      if (_packageToInstallTimestamp != null) {
        return true;
      }

      _packageToInstallTimestamp = new Dictionary<string, long>();

      if (File.Exists(InstallHistoryPath)) {
        try {
          var installHistory = JsonUtility.FromJson<InstallHistory>(File.ReadAllText(InstallHistoryPath));

          foreach (var element in installHistory.InstallTimetsamps) {
            _packageToInstallTimestamp[element.PackageName] = element.InstallTimestamp;
          }
        } catch (Exception e) {
          //Remember to set to null, an empty dictionary is a valid state
          _packageToInstallTimestamp = null;
          Debug.LogError("Could not read install history");
          Debug.LogException(e);
          return false;
        }
      }

      return true;
    }

    private static void WriteChangesToDisk() {
      try {
        //Convert local dictionary data to InstallHistory container for Json conversion
        InstallHistory history = new InstallHistory() {
          InstallTimetsamps = _packageToInstallTimestamp.Select(pair => new PackageAndInstallTimestampPair() {
            PackageName = pair.Key,
            InstallTimestamp = pair.Value
          }).ToList()
        };

        File.WriteAllText(InstallHistoryPath, JsonUtility.ToJson(history, prettyPrint: true));
      } catch (Exception e) {
        Debug.LogError("Could not write install history");
        Debug.LogException(e);
      }
    }

    [Serializable]
    private class InstallHistory {
      public List<PackageAndInstallTimestampPair> InstallTimetsamps;
    }

    [Serializable]
    private struct PackageAndInstallTimestampPair {
      public string PackageName;
      public long InstallTimestamp;
    }
  }
}
