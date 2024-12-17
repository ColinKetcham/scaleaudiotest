// (c) Meta Platforms, Inc. and affiliates. Confidential and proprietary.

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace Protokit.Browser {

  [Serializable]
  public class PackageGUIContent {

    private const string DEPRECATED_MESSAGE = "This package is deprecated.  It will not be receiving any support "+ 
                                              "or updates from the creators, and should be used only at your own risk.";

    [SerializeField]
    private InstallButtonGUIContent _installButtonContent = new InstallButtonGUIContent();

    [SerializeField]
    private UninstallButtonGUIContent _uninstallButtonContent = new UninstallButtonGUIContent();

    [SerializeField]
    private VersionDropdownGUIContent _versionDropdownContent = new VersionDropdownGUIContent();

    private Color PackageHighlightTint => EditorGUIUtility.isProSkin ? new Color(0.275f, 0.275f, 0.275f, 1.0f) :
                                                                       new Color(0.68f, 0.68f, 0.68f, 1.0f);
    private static readonly float HeaderHeight = 21;
    private static readonly string DateFormat = "MMM d, yyyy";

    private static readonly Vector2 PackageHeaderPositionOffset = new Vector2(2, 3);
    private static readonly Vector2 PackageHeaderSizeOffset = new Vector2(-5, 0);
    private static readonly float InstallButtonWidth = 67;
    private static readonly float VersionButtonWidth = 130;

    private bool _viewDependencies;
    private Dictionary<string, string> _packageToSelectedVersion = new Dictionary<string, string>();

    private Dictionary<VersionInfo, GUIContent> _dateToContent = new Dictionary<VersionInfo, GUIContent>();
    private string _previouslyShownDescription;

    public void ClearCustomSearchVersions() {
      _packageToSelectedVersion.Clear();
    }

    public void OnGUI(BrowserState state, PackageInfo package, Vector2 containerScroll, Vector2 containerSize) {
      bool isExpanded = state.ExpandedPackageName == package.PackageName;

      if (isExpanded) {
        GUI.color = PackageHighlightTint;
        GUILayout.BeginVertical(BrowserStyles.PackageHighlight);
        GUI.color = Color.white;
      }

      Rect headerRect = GUILayoutUtility.GetRect(1, HeaderHeight);

      if (headerRect.yMax > containerScroll.y && headerRect.y < (containerScroll.y + containerSize.y)) {
        DrawPackageHeader(state, package, headerRect, isExpanded);
      }

      if (isExpanded) {
        var selectedVersion = GetSelectedVersionForPackage(state, package);

        using (new GUILayout.HorizontalScope()) {
          GUILayout.Space(10);
          using (new GUILayout.VerticalScope()) {
            DrawPackage_ExpandedDetails(state, package, selectedVersion);
          }
        }

        GUILayout.EndVertical();
      }
    }

    private void DrawPackageHeader(BrowserState state, PackageInfo package, Rect headerRect, bool isExpanded) {
      headerRect.position += PackageHeaderPositionOffset;
      headerRect.size += PackageHeaderSizeOffset;

      Rect installRect = headerRect;
      installRect.x = headerRect.xMax - InstallButtonWidth;
      installRect.width = InstallButtonWidth;

      Rect dropdownRect = installRect;
      dropdownRect.x -= VersionButtonWidth;
      dropdownRect.width = VersionButtonWidth;

      float foldoutWidth = BrowserStyles.PackageFoldout.CalcSize(package.Default.TitleContent).x;
      float dateWidth = 0;
      GUIContent dateContent = GUIContent.none;
      if (!_dateToContent.TryGetValue(package.Default, out dateContent)) {
        var date = package.Default.DatePublished;
        if (date == default) {
          dateContent = new GUIContent("-");
        } else {
          dateContent = new GUIContent(date.ToString(DateFormat));
        }

        _dateToContent[package.Default] = dateContent;
      }

      dateWidth = BrowserStyles.Date.CalcSize(dateContent).x;

      Rect foldoutRect = headerRect;
      foldoutRect.width = dropdownRect.x - headerRect.x;

      Rect dateRect = dropdownRect;
      dateRect.x = Mathf.Max(dropdownRect.xMin - dateWidth, foldoutRect.x + foldoutWidth);
      dateRect.width = dropdownRect.xMin - dateRect.x;

      if (package.IsDeprecated) {
        GUI.color = Color.red;
      }
      bool newExpanded = GUI.Toggle(foldoutRect, isExpanded, package.Default.TitleContent, BrowserStyles.PackageFoldout);
      GUI.color = Color.white;
      if (newExpanded != isExpanded) {
        state.ExpandedPackageName = newExpanded ? package.PackageName : "";
      }

      GUI.Label(dateRect, dateContent, BrowserStyles.Date);

      var selectedVersion = GetSelectedVersionForPackage(state, package);

      _versionDropdownContent.OnGUI(state, dropdownRect, package, selectedVersion, onVersionSelected: (info, version) => {
        _packageToSelectedVersion[package.PackageName] = version;
        EditorWindow.GetWindow<ProtokitBrowser>().Focus();
      });

      _installButtonContent.OnGUI(state, installRect, package, selectedVersion);
    }

    private void DrawPackage_ExpandedDetails(BrowserState state, PackageInfo package, string selectedVersion) {
      var versionTask = PackageDatabase.GetVersionInfo(package.PackageName, selectedVersion);

      if (!versionTask.IsCompleted) {
        GUILayout.Space(10);
        BrowserUtil.LoadingSpinnerOnGUI();
        GUILayout.Space(10);
        return;
      }
      var version = versionTask.Result;

      using (new GUILayout.HorizontalScope()) {
        using (new GUILayout.VerticalScope()) {
          if (package.IsDeprecated) {
            EditorGUILayout.HelpBox(DEPRECATED_MESSAGE, MessageType.Error, wide: true);  
          }

          if (!version.IsCompatible) {
            GUI.color = new Color(0.7f, 0.2f, 0.2f, 1f);
            GUILayout.BeginHorizontal(BrowserStyles.PackageHighlight);
            GUI.color = Color.black;
            GUILayout.Label($"Requires Unity {version.UnityVersion}", BrowserStyles.DetailsMini);
            GUI.color = Color.white;
            GUILayout.Space(10);
            GUILayout.EndHorizontal();
          }

          using (new GUILayout.HorizontalScope()) {
            if (state.DatabaseRequest.Result.TryGetInstalledVersion(package.PackageName, out string installedVersion)) {
              GUILayout.Label($"{package.PackageName} (installed: {installedVersion})", BrowserStyles.DetailsMini);
            } else {
              GUILayout.Label(package.PackageName, BrowserStyles.DetailsMini);
            }

            if (ProtokitBrowserFeatureFlags.SupportedPackagesFilter) {
              GUIContent content = new GUIContent();
              if (version.IsSupported) {
                GUI.color = Color.green;
                content.text = "VERIFIED";
                content.tooltip = "This package has been verified to work well.";
              } else {
                GUI.color = Color.yellow;
                content.text = "UNVERIFIED";
                if (string.IsNullOrEmpty(package.VerifiedVersion)) {
                  content.tooltip = "This package has not been verified, using it may be risky.";
                } else {
                  content.tooltip = $"This is an older unverified version of this package, using it may be risky.\n\nVerified at {package.VerifiedVersion}";
                }
              }
              GUILayout.Space(10);
              GUILayout.Label(content, BrowserStyles.DetailsMini);
              GUI.color = Color.white;
            }
          }

          GUILayout.Label($"Published on {version.DatePublished.ToString(DateFormat)}", BrowserStyles.DetailsMini);

          if (!string.IsNullOrEmpty(version.Author)) {
            GUILayout.Label(version.Author, BrowserStyles.DetailsMini);
          }

          GUILayout.Label(string.Join(" ", version.Keywords), BrowserStyles.DetailsMini);
        }

        GUILayout.FlexibleSpace();

        using (new GUILayout.VerticalScope()) {
          _uninstallButtonContent.OnGUI(state, package);
        }

        GUILayout.Space(3);
      }

      GUILayout.Space(30);

      var descriptionContent = new GUIContent(version.Description);
      var descriptionRect = GUILayoutUtility.GetRect(descriptionContent, BrowserStyles.DetailsNormal);

      //Unity gets confused if the description gets highlighted and then hidden due to packages
      //being expanded / closed or new packages being shown.  Make sure to reset the keyboard
      //control if the description ever changes
      if (version.Description != _previouslyShownDescription) {
        _previouslyShownDescription = version.Description;
        GUIUtility.keyboardControl = 0;
      }

      EditorGUI.SelectableLabel(descriptionRect, version.Description, BrowserStyles.DetailsNormal);

      if (version.HasSamples) {
        GUILayout.Space(10);
        if (state.DatabaseRequest.Result.TryGetInstalledVersion(package.PackageName, out string installedVersion) && installedVersion == version.Version) {
          if (version.Samples != null) {
            if (version.Samples.Length >= 2) {
              if (GUILayout.Button("Import All Samples", BrowserStyles.DownloadButton)) {
                foreach (var sample in version.Samples) {
                  sample.Import(autoRefresh: false);
                }
                AssetDatabase.Refresh();
              }
            }

            foreach (var sample in version.Samples) {
              if (GUILayout.Button(new GUIContent(sample.displayName, sample.description), BrowserStyles.DownloadButton)) {
                sample.Import();
              }
            }
          }
        } else {
          using (new EditorGUI.DisabledGroupScope(true)) {
            GUILayout.Button(new GUIContent("Import Samples", "You must install this package before you can install its Samples"), BrowserStyles.DownloadButton);
          }
        }
        GUILayout.Space(10);
      }

      if (version.Dependencies.Count > 0) {
        _viewDependencies = GUILayout.Toggle(_viewDependencies, $"Dependencies ({version.Dependencies.Count})", BrowserStyles.PackageFoldout);
        if (_viewDependencies) {
          using (new EditorGUI.IndentLevelScope()) {
            foreach (var dependency in version.Dependencies) {
              if (GUILayout.Button($"{dependency.Name} {dependency.Version}", BrowserUtil.NoExpandWidth)) {
                EditorApplication.delayCall += () => {
                  state.SearchParameters.ClearSearchTerms();
                  state.SearchParameters.AddSearchTerm(dependency.Name);
                  state.ExpandedPackageName = dependency.Name;
                  _packageToSelectedVersion[state.ExpandedPackageName] = dependency.Version;
                };
              }
            }
          }
        }
      }

      GUILayout.Space(4);
    }

    private string GetSelectedVersionForPackage(BrowserState state, PackageInfo package) {
      if (!_packageToSelectedVersion.TryGetValue(package.PackageName, out var selectedVersion)) {
        selectedVersion = GetDefaultVersionToShow(state, package);
        _packageToSelectedVersion[package.PackageName] = selectedVersion;
      }
      return selectedVersion;
    }

    private string GetDefaultVersionToShow(BrowserState state, PackageInfo package) {
      if (state.DatabaseRequest.Result.TryGetInstalledVersion(package.PackageName, out var installedVersion)) {
        //Always show installed version if package is installed
        return installedVersion;
      } else if (!string.IsNullOrEmpty(package.VerifiedVersion)) {
        //If not installed, always prefer to show verified version
        return package.VerifiedVersion;
      } else {
        //Otherwise prefer to show newest non-preview version
        for (int i = 0; i < package.Versions.Count; i++) {
          if (!package.Versions[i].Version.Contains("-")) {
            return package.Versions[i].Version;
          }
        }

        //Otherwise, if all packages are in preview, show latest version
        return package.Versions[0].Version;
      }
    }
  }
}
