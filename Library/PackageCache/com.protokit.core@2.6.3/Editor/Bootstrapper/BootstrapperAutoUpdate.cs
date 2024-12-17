// (c) Meta Platforms, Inc. and affiliates. Confidential and proprietary.

using System.IO;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEditor;
using System;

public static class BootstrapperAutoUpdate {

  private const string CURRENT_VERSION = "2.0.1";
  private const string BOOTSTRAPPER_FILENAME = "ProtokitBootstrapper.dll";

  [InitializeOnLoadMethod]
  public static void CheckForBootstrapperUpdate() {
    var status = GetBootstrapperStatus();
    if (status != BootstrapperStatus.UpToDate) {
      if (status == BootstrapperStatus.Missing) {
        EditorUtility.DisplayDialog("Creating Bootstrapper", "Protokit will be creating The Protokit Bootstrapper for this project.  " +
                                    "Bootstrapper allows authentication and issue reporting to be more robust.  It lives in the folder " +
                                    "Assets/Protokit and SHOULD be checked into source control.", "Ok");
      }

      Debug.Log($"Updating protokit bootstrapper to version {CURRENT_VERSION}");
      UpdateBootstrapper();
    }
  }

  public static BootstrapperStatus GetBootstrapperStatus() {
    foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies()) {
      var versionType = assembly.GetType("Protokit.Bootstrapper.BootstrapperVersion");
      if (versionType != null) {
        string version = versionType.GetField("VALUE")?.GetValue(null) as string;
        if (version == CURRENT_VERSION) {
          return BootstrapperStatus.UpToDate;
        } else {
          return BootstrapperStatus.OutOfDate;
        }
      }
    }

    return BootstrapperStatus.Missing;
  }

  public static void UpdateBootstrapper() {
    //Make sure the protokit folder exists first!
    Directory.CreateDirectory(Path.Combine(Application.dataPath, "Protokit"));

    //simply copy over the files from our resources to the destination
    File.Copy(GetBootstrapperResourcePath(), GetBootstrapperDestinationPath(), overwrite: true);
    File.Copy(GetBootstrapperResourcePath() + ".meta", GetBootstrapperDestinationPath() + ".meta", overwrite: true);

    //Force a refresh so changes can take effect
    AssetDatabase.Refresh();
  }

  private static string GetBootstrapperResourcePath() {
    return Path.Combine("Packages", "com.protokit.core", "Editor", "Bootstrapper", ".BootstrapperResources", BOOTSTRAPPER_FILENAME);
  }

  private static string GetBootstrapperDestinationPath() {
    return Path.Combine(Application.dataPath, "Protokit", BOOTSTRAPPER_FILENAME);
  }

  public enum BootstrapperStatus {
    Missing,
    OutOfDate,
    UpToDate
  }
}
