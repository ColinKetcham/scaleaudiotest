// (c) Meta Platforms, Inc. and affiliates. Confidential and proprietary.

using System;
using System.Linq;
using System.Runtime.Serialization;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using System.IO;

namespace Protokit.Core {

  /// <summary>
  /// Automates the process of finding and using a custom keystore.  When the user triggers a build
  /// this process will search for all classes that have implemented the IKeystoreProvider interface,
  /// and use them to gather a pool of potential keystores.  The keystore with the highest priority
  /// is selected and assigned to the build.
  /// 
  /// The user can also optionally visit the Project Settings menu and configure this process.  The
  /// user can select a specific keystore they want to use, rather than let it be selected automatically.
  /// The keystore writing process can also be disabled completely.
  /// </summary>
  public class KeystoreAutomation : IPreprocessBuildWithReport {

    public static readonly string AutoKeystoreId = "AUTO";
    public static readonly List<KeystoreData> AvailableKeystores;
    public int callbackOrder => 100;

    static KeystoreAutomation() {
      AvailableKeystores = TypeCache.GetTypesDerivedFrom<IKeystoreProvider>().
                           Select(t => {
                             try {
                               var instance = FormatterServices.GetUninitializedObject(t) as IKeystoreProvider;
                               return instance?.GetKeystore();
                             } catch (Exception e) {
                               Debug.LogException(e);
                               return null;
                             }
                           }).
                           Where(k => k != null).
                           OrderByDescending(k => k.Priority).
                           ToList();


      //The auto-keystore is a special mock keystore that represents the desire to auto-select
      //the keystore based on the available keystores
      AvailableKeystores.Insert(0, new KeystoreData() {
        Title = "Auto",
        Identifier = AutoKeystoreId,
        Priority = int.MinValue
      });
    }

    public void OnPreprocessBuild(BuildReport report) {
      if (KeystoreProjectSettings.OverwriteKeystoreOnBuild) {
        SetKeystore();
      }
    }

    public static void SetKeystore() {
      KeystoreData keystore = AvailableKeystores.FirstOrDefault(k => k.Identifier == KeystoreProjectSettings.KeystoreSourceIdentifier);
      if (keystore == null || keystore.Identifier == AutoKeystoreId) {
        keystore = AvailableKeystores.Where(k => k.Identifier != AutoKeystoreId).
                                      OrderByDescending(k => k.Priority). //First sort by priority
                                      ThenBy(k => k.Identifier).          //Then by identifier so auto-selection is deterministic given equal priorities
                                      FirstOrDefault();
      }

      if (keystore == null) {
        return;
      }

      if (string.IsNullOrEmpty(keystore.KeystoreName)) {
        return;
      }

      //GetFullPath magically de-virtualizes paths for us!
      //Important because Unity expects de-virtualized paths when referencing
      //a keystore asset
      string keystorePath = Path.GetFullPath(keystore.KeystoreName);
      if (!File.Exists(keystorePath)) {
        return;
      }

      //Unity always expects forward slashes for it's keystore path
      PlayerSettings.Android.keystoreName = keystorePath.Replace('\\', '/');
      PlayerSettings.Android.keystorePass = keystore.KeystorePassword ?? "";
      PlayerSettings.Android.keyaliasName = keystore.KeyaliasName ?? "";
      PlayerSettings.Android.keyaliasPass = keystore.KeyaliasPassword ?? "";
      PlayerSettings.Android.useCustomKeystore = true;
    }
  }
}
