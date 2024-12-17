// (c) Meta Platforms, Inc. and affiliates. Confidential and proprietary.

using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.UI;
using UnityEditor.PackageManager.Requests;
using UnityEngine.UIElements;

namespace Protokit.Core.PackageExtensions {

  public enum PackageAutoUpdateSetting {
    /// <summary>
    /// In the Off setting, no action will be taken for a package, even
    /// if there are new versions available.
    /// </summary>
    Off = 0,

    /// <summary>
    /// In the Notify setting, if there are newer versions available the
    /// package update script will notify the user and give them the option
    /// of updating.
    /// </summary>
    Notify = 1,

    /// <summary>
    /// In the AutoUpdate setting, if there are newer versions available
    /// they will be automatically installed when the user opens the Unity
    /// project.
    /// </summary>
    AutoUpdate = 2
  }

  /// <summary>
  /// The PackageAutoUpdate script allows packages to be watched for newer versions.  The user
  /// can configure an action to take when a new version is found, which includes either notifying
  /// the user that a new version is available, or automatically updating the package without
  /// notifying.
  /// </summary>
  public class PackageAutoUpdate : IPackageManagerExtension {

    #region PUBLIC API

    /// <summary>
    /// Returns whether or not the given package supports auto-update functionality.  Only packages that
    /// come from a registry support auto-update because those are the only types of packages that
    /// support a version stream.
    /// </summary>
    public static bool DoesSupportAutoUpdateForPackage(UnityEditor.PackageManager.PackageInfo package) {
      return package != null && package.source == PackageSource.Registry;
    }

    /// <summary>
    /// Returns the package-update setting for a given package.
    /// This value is project local and machine local.
    /// </summary>
    public static PackageAutoUpdateSetting GetUpdateSettingForPackage(string packageName) {
      string key = PACKAGE_AUTO_UPDATE_SETTING_PREFIX + packageName;

      if (PlayerPrefs.HasKey(key)) {
        return (PackageAutoUpdateSetting)PlayerPrefs.GetInt(key);
      }

      switch (packageName) {
        //for a small subset of hardcoded packages, the default behavior is to auto-update.
        //Core is important because it contains the auto-update flow itself, and various
        //other workflows.  Allowing it to auto-update allows us to push new features to
        //users automatically
        case "com.protokit.core":
          return PackageAutoUpdateSetting.AutoUpdate;
        default:
          return PackageAutoUpdateSetting.Off;
      }
    }

    /// <summary>
    /// Sets the package-update setting for a given package.
    /// 
    /// This value is project local and machine local, so modifying this value will not affect
    /// the package-update setting for the package on any other project, even projects on the
    /// same machine.
    /// </summary>
    public static void SetAutoUpdateForPackage(string packageName, PackageAutoUpdateSetting setting) {
      string key = PACKAGE_AUTO_UPDATE_SETTING_PREFIX + packageName;
      PlayerPrefs.SetInt(key, (int)setting);
    }

    /// <summary>
    /// Start the package update check.  This call will return right away, and the update process
    /// will continue in the background.
    /// 
    /// Notify can be set to false to prevent any dialog options from being shown to the user.
    /// ShowProgress can be set to true to give constant feedback to the user about the process.
    /// </summary>
    public static void PerformPackageUpdateCheck(bool notify = true, bool showProgress = false) {
      PerformPackageUpdateCheckAsync(notify, showProgress);
    }

    /// <summary>
    /// Perform the package update check.  This call is async, and so you can await the method to
    /// wait until the update check has finished.
    /// 
    /// Notify can be set to false to prevent any dialog options from being shown to the user.
    /// ShowProgress can be set to true to give constant feedback to the user about the process.
    /// </summary>
    public static async void PerformPackageUpdateCheckAsync(bool notify = true, bool showProgress = false) {
      try {
        var searchRequest = Client.List(offlineMode: false, includeIndirectDependencies: false);

        {
          float timeStarted = Time.realtimeSinceStartup;
          float halfwayTime = 0.2f;
          while (!searchRequest.IsCompleted) {
            if (showProgress) {
              //this is a fairly fast operation, and so we show some fake progress to let the user know something is happening
              float mockProgress = 1 - 1 / (1 + (Time.realtimeSinceStartup - timeStarted) / halfwayTime);
              if (EditorUtility.DisplayCancelableProgressBar("Checking For Updates", "Checking for package updates...", mockProgress)) {
                return;
              }
            }

            await Task.Delay(20);
          }
        }

        EditorUtility.ClearProgressBar();

        //We don't log errors on failure, but we will retry the next time there is a recompile
        if (searchRequest.Status == StatusCode.Failure) {
          return;
        }

        List<PackageUpdateInfo> allPackagesWithUpdates = new List<PackageUpdateInfo>();

        foreach (var package in searchRequest.Result) {
          if (!DoesSupportAutoUpdateForPackage(package)) {
            continue;
          }

          var installedVersion = package.version;
          var isInstalledInPreview = installedVersion.Contains("preview");

          //If the currently installed package is not a preview package, we don't consider newer packages as valid if they
          //are in preview state, because we never want to recommend an upgrade from a non-preview package to a preview package.
          //But if the user already has a preview package installed, we *do* recommend newer preview packages.
          var newestCompatible = package.versions.compatible.Where(p => isInstalledInPreview || !p.Contains("preview")).LastOrDefault();

          if (string.IsNullOrEmpty(newestCompatible)) {
            continue;
          }

          if (installedVersion != newestCompatible) {
            allPackagesWithUpdates.Add(new PackageUpdateInfo() {
              PackageDisplayName = package.displayName,
              PackageName = package.name,
              CurrentVersion = package.version,
              NewVersion = newestCompatible
            });
          }
        }

        var toUpdate = allPackagesWithUpdates.Where(u => GetUpdateSettingForPackage(u.PackageName) == PackageAutoUpdateSetting.AutoUpdate);
        var toNotify = allPackagesWithUpdates.Where(u => GetUpdateSettingForPackage(u.PackageName) == PackageAutoUpdateSetting.Notify);

        if (notify && toNotify.Any()) {
          int choice = EditorUtility.DisplayDialogComplex("New Packages Available",
                                                          "There are new versions of installed packages available!\n\n" + string.Join("\n", toNotify.Select(n => n.ToString())),
                                                          "Dismiss",
                                                          "Update All",
                                                          "Never Show Again");

          switch (choice) {
            case 0:
              //Dismiss
              break;
            case 1:
              //Update all
              toUpdate = toUpdate.Concat(toNotify).Distinct();
              break;
            case 2:
              // Never show again
              // Singe the user has selected to NEVER show again, we turn off the notify setting for ALL
              // packages that were set to notify, not just the ones that got updated this time.
              foreach (var package in searchRequest.Result.Where(p => GetUpdateSettingForPackage(p.name) == PackageAutoUpdateSetting.Notify)) {
                SetAutoUpdateForPackage(package.name, PackageAutoUpdateSetting.Off);
              }
              break;
          }
        }

        //We mark as having performed the update here, before we try to update any packages.  That way we are
        //sure that domain reloads don't prevent us from losing our progress.
        HasPerformedAutoUpdateThisSession = true;

        if (toUpdate.Any()) {
          if (showProgress) {
            //It takes a little bit before the Add request starts showing progress bars, so we show a progress dialog here so
            //that the user knows something is happening

            //We can't show fake progress here because we can't know when the package add process is going to start showing
            //its own progress bar, and we don't want to overwrite it
            EditorUtility.DisplayProgressBar("Updating Packages", "Updating to newest versions of installed packages...", 0.0f);
          }

          List<AddRequest> installRequests = new List<AddRequest>();

          foreach (var package in toUpdate) {
            installRequests.Add(Client.Add(package.PackageName + "@" + package.NewVersion));
          }

          //Wait until all add requests have completed
          while (installRequests.Any(r => !r.IsCompleted)) {
            await Task.Delay(100);
          }

          //Check to see if any packages failed to update
          for (int i = 0; i < installRequests.Count; i++) {
            var request = installRequests[i];
            var package = toUpdate.ElementAt(i);

            //We *do* log errors for auto-updates
            if (request.Status == StatusCode.Failure) {
              Debug.LogError($"Auto-update of package {package.PackageName} failed!  The following error was encountered:\n\n{request.Error.errorCode}\n{request.Error.message}");
            }
          }
        }

        //If it was a user-initiated request, we show a dialog box to let them know everything completed without error
        if (notify && showProgress && !toNotify.Any() && !toUpdate.Any()) {
          EditorUtility.ClearProgressBar();
          EditorUtility.DisplayDialog("Update Complete",
                                      "All Packages you are watching are up to date.  To watch more packages, visit the Package Manager window.",
                                      "Ok");
        }
      } finally {
        EditorUtility.ClearProgressBar();
      }
    }

    #endregion

    #region IMPLEMENTATION

    private const string PACKAGE_AUTO_UPDATE_SETTING_PREFIX = "Protokit_PackageAutoUpdateSetting_";
    private const string PACKAGE_AUTO_UPDATE_HAS_PERFORMED_AUTO_UPDATE_THIS_SESSION_KEY = "Protokit_PackageAutoUpdate_HasPerformedAutoUpdateThisSession";

    private static GUIContent _titleContent = new GUIContent("Package Updates:", "You can configure whether or not you want to watch this package for updates, or automatically update when new versions are released.");

    private static bool HasPerformedAutoUpdateThisSession {
      get => SessionState.GetBool(PACKAGE_AUTO_UPDATE_HAS_PERFORMED_AUTO_UPDATE_THIS_SESSION_KEY, defaultValue: false);
      set => SessionState.SetBool(PACKAGE_AUTO_UPDATE_HAS_PERFORMED_AUTO_UPDATE_THIS_SESSION_KEY, value);
    }

    private UnityEditor.PackageManager.PackageInfo _selectedPackage;

    [InitializeOnLoadMethod]
    private static void OnInitializeOnLoad() {
      PackageManagerExtensions.RegisterExtension(new PackageAutoUpdate());

      // We perform the auto update after a domain reload, assuming we haven't already
      // completed an auto update this session
      if (!HasPerformedAutoUpdateThisSession) {
        PerformPackageUpdateCheck();
      }
    }

    [MenuItem("Protokit/Packages/Check For Package Updates")]
    private static void PerformAutoUpdateMenuItem() {
      PerformPackageUpdateCheck(notify: true, showProgress: true);
    }

    VisualElement IPackageManagerExtension.CreateExtensionUI() {
      return new IMGUIContainer(() => {
        if (!DoesSupportAutoUpdateForPackage(_selectedPackage)) {
          return;
        }

        //Don't show for uninstalled packages
        if (string.IsNullOrEmpty(_selectedPackage.resolvedPath)) {
          return;
        }

        using (new GUILayout.HorizontalScope())
        using (var check = new EditorGUI.ChangeCheckScope()) {
          GUILayout.Space(3);

          var setting = GetUpdateSettingForPackage(_selectedPackage.name);

          GUILayout.Label(_titleContent, GUILayout.Width(115));

          setting = (PackageAutoUpdateSetting)EditorGUILayout.EnumPopup(setting, GUILayout.Width(18));

          GUILayout.Label(setting.ToString(), GUILayout.ExpandWidth(false));

          if (check.changed) {
            SetAutoUpdateForPackage(_selectedPackage.name, setting);
          }
        }
      });
    }

    void IPackageManagerExtension.OnPackageAddedOrUpdated(UnityEditor.PackageManager.PackageInfo packageInfo) { }

    void IPackageManagerExtension.OnPackageRemoved(UnityEditor.PackageManager.PackageInfo packageInfo) { }

    void IPackageManagerExtension.OnPackageSelectionChange(UnityEditor.PackageManager.PackageInfo packageInfo) {
      _selectedPackage = packageInfo;
    }

    private struct PackageUpdateInfo {
      public string PackageDisplayName;
      public string PackageName;
      public string CurrentVersion;
      public string NewVersion;

      public override string ToString() {
        string displayName = string.IsNullOrWhiteSpace(PackageDisplayName) ? PackageName : $"{PackageDisplayName} ({PackageName})";
        return $"{displayName}\n    {CurrentVersion} -> {NewVersion}";
      }
    }

    #endregion
  }
}
