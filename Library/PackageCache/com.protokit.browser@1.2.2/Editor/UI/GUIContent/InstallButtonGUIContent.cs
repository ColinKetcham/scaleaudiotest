// (c) Meta Platforms, Inc. and affiliates. Confidential and proprietary.

using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UPackageSource = UnityEditor.PackageManager.PackageSource;

namespace Protokit.Browser {

  public class InstallButtonGUIContent {

    private static Color InstallColor = new Color(0.31f, 0.67f, 0.99f);
    private static Color UpgradeColor = new Color(0.5f, 0.8f, 0.5f);
    private static Color ChangeColor = UpgradeColor;
    private static Color InstalledColor = new Color(0.8f, 0.8f, 0.8f);
    private static Color EmbeddedColor = new Color(0.8f, 0.7f, 0.8f);
    private static Color LocalColor = EmbeddedColor;
    private static Color LockColor = new Color(0.8f, 0.8f, 0.8f);

    private static GUIContent _installContent = new GUIContent("Install", "Add this package as a direct dependency in your project");
    private static GUIContent _changeContent = new GUIContent("Change", "Change the version of this package");
    private static GUIContent _changeContentDisabled = new GUIContent("Change", "Changing the version of embedded or local packages is not yet supported.");
    private static GUIContent _installedContent = new GUIContent("Installed", "This package is already installed as a dependency of your project");
    private static GUIContent _embeddedContent = new GUIContent("Embedded", "This package has been embedded into your project directly.");
    private static GUIContent _localContent = new GUIContent("Local", "This package is a local reference to a location on your machine.");
    private static GUIContent _lockContent = new GUIContent("Lock", "Create a direct dependency on this version so it doesn't change");

    private static Dictionary<string, GUIContent> _upgradeVersionToButtonContent = new Dictionary<string, GUIContent>();

    public void OnGUI(BrowserState state, Rect rect, PackageInfo package, string selectedVersion) {
      GUIContent installButtonContent = _installContent;
      GUI.color = InstallColor;
      string versionToInstall = selectedVersion;
      bool shouldDisable = false;

      if (state.DatabaseRequest.IsCompleted) {
        if (state.DatabaseRequest.Result.Dependencies.TryGetValue(package.PackageName, out var installInfo)) {
          if (installInfo.InstalledVersion != selectedVersion) {
            installButtonContent = _changeContent;
            GUI.color = ChangeColor;

            switch (installInfo.InstallSource) {
              case PackageSource.Local:
              case PackageSource.Embedded:
                shouldDisable = true;
                installButtonContent = _changeContentDisabled;
                break;
            }
          } else {
            switch (installInfo.InstallSource) {
              case PackageSource.Embedded:
                installButtonContent = _embeddedContent;
                GUI.color = EmbeddedColor;
                break;
              case PackageSource.Local:
                installButtonContent = _localContent;
                GUI.color = LocalColor;
                break;
              case PackageSource.DirectDependency:
              case PackageSource.IndirectDependency:
                if (!string.IsNullOrEmpty(installInfo.UpgradeVersion)) {
                  if (!_upgradeVersionToButtonContent.TryGetValue(installInfo.UpgradeVersion, out installButtonContent)) {
                    installButtonContent = new GUIContent("Upgrade", $"Upgrade this package to version {installInfo.UpgradeVersion}");
                    _upgradeVersionToButtonContent[installInfo.UpgradeVersion] = installButtonContent;
                  }

                  versionToInstall = installInfo.UpgradeVersion;
                  GUI.color = UpgradeColor;
                } else {
                  if (state.DatabaseRequest.Result.IsDirectDependency(package.PackageName)) {
                    installButtonContent = _installedContent;
                    GUI.color = InstalledColor;
                  } else {
                    installButtonContent = _lockContent;
                    GUI.color = LockColor;
                  }
                }
                break;
            }
          }
        }
      }

      EditorGUI.BeginDisabledGroup(shouldDisable);
      if (GUI.Button(rect, installButtonContent, BrowserStyles.Install)) {
        if (BrowserUtil.ShouldQueueAction()) {
          state.QueuePackageInstall(package.PackageName, versionToInstall);
        } else if (BrowserUtil.CheckWithUserForIncompatiblePackage(package.PackageName, versionToInstall)) {
          PackageDatabase.Install(package.PackageName, versionToInstall);
        }
      }
      EditorGUI.EndDisabledGroup();

      GUI.color = Color.white;
    }
  }
}
