// (c) Meta Platforms, Inc. and affiliates. Confidential and proprietary.

using System;
using System.IO;
using UnityEngine;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.UI;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace Protokit.Core {

  public static class PackageUtility {

    /// <summary>
    /// A utility method to add an extension and make sure they are added in a specific order.  All calls should be
    /// made from within methods annotated with InitializeOnLoadMethod
    /// </summary>
    public static void RegisterPackageExtension(IPackageManagerExtension extension, int priority) {
      //TODO: this method needs a correct implementation, the previous strategy does not work
      //      and causes issues.  There might be a correct way to go about this by creating a single
      //      extension that hosts the remaining in a specific order

      PackageManagerExtensions.RegisterExtension(extension);
    }

    [Obsolete("GetFullPathToCallersPackage can sometimes return incorrect results, please switch to GetFullPathToPackage", error: false)]
    public static string GetFullPathToCallersPackage([CallerFilePath] string path = null) {
      if (string.IsNullOrEmpty(path)) {
        return "";
      }

      //Sanity check to prevent editor from hanging
      int triesLeft = 1000;

      string dir = Path.GetDirectoryName(path);
      while (true) {
        string[] packagePaths = Directory.GetFiles(dir, "package.json", SearchOption.TopDirectoryOnly);
        if (packagePaths.Length > 0) {
          return Path.GetDirectoryName(packagePaths[0]);
        }

        dir = Path.GetDirectoryName(dir);
        if (string.IsNullOrEmpty(dir)) {
          return "";
        }

        triesLeft--;
        if (triesLeft <= 0) {
          return "";
        }
      }
    }

    /// <summary>
    /// Returns the VIRTUALIZED path to a given package.  This will always be of the form
    /// Project/Packages/com.your.package.name.  When using File operations, this will behave
    /// correctly due to Unity's automatic package filesystem virtualization.  If you need to
    /// do work outside of this virtualization, use GetFullUnVirtualizedPathToPackage.
    /// </summary>
    public static string GetFullPathToPackage(string packageName) {
      string projectDir = Path.GetDirectoryName(Application.dataPath);
      return Path.Combine(projectDir, "Packages", packageName);
    }

    /// <summary>
    /// Queries the package database to get the full unvirtualized path to a given package.  This
    /// will return the actual location the package is in the file system
    /// </summary>
    public static async Task<string> GetFullUnVirtualizedPathToPackage(string packageName) {
      var listRequest = Client.List(offlineMode: true, includeIndirectDependencies: true);
      while (!listRequest.IsCompleted) {
        await Task.Delay(10);
      }

      if (listRequest.Status != StatusCode.Success) {
        return null;
      }

      foreach (var package in listRequest.Result) {
        if (package.name == packageName) {
          return package.resolvedPath;
        }
      }

      return null;
    }
  }
}
