// (c) Meta Platforms, Inc. and affiliates. Confidential and proprietary.

using UnityEngine;
using UnityEditor;

namespace Protokit.Browser {

  public static class BrowserStyles {

    private const string SKIN_DARK_PATH = "Packages/com.protokit.browser/Editor/Skins/ProtokitBrowserSkinDark.guiskin";
    private const string SKIN_LIGHT_PATH = "Packages/com.protokit.browser/Editor/Skins/ProtokitBrowserSkinLight.guiskin";

    private static GUISkin _cachedSkinDarkMode;
    private static GUISkin _cachedSkinLightMode;

    public static GUISkin Skin {
      get {
        //TODO: what is the best way to load a specific asset at edit time?  Don't want to use Resources folder
        //      because then browser assets get auto-bundled into every build
        if (_cachedSkinDarkMode == null) {
          _cachedSkinDarkMode = AssetDatabase.LoadAssetAtPath<GUISkin>(SKIN_DARK_PATH);
        }
        if (_cachedSkinLightMode == null) {
          _cachedSkinLightMode = AssetDatabase.LoadAssetAtPath<GUISkin>(SKIN_LIGHT_PATH);//
        }

        return EditorGUIUtility.isProSkin ? _cachedSkinDarkMode : _cachedSkinLightMode;
      }
    }

    public static readonly GUIStyle FilterFoldout = Skin.FindStyle("Filter Foldout");
    public static readonly GUIStyle FilterViewAll = Skin.FindStyle("Filter ViewAll");
    public static readonly GUIStyle FilterToggle = Skin.FindStyle("Filter Toggle");
    public static readonly GUIStyle LargeHeader = Skin.FindStyle("Large Header");
    public static readonly GUIStyle FilterHeader = Skin.FindStyle("Filter Header");
    public static readonly GUIStyle FilterSeparator = Skin.FindStyle("Filter Separator");
    public static readonly GUIStyle Install = Skin.FindStyle("Install");
    public static readonly GUIStyle PackageHighlight = Skin.FindStyle("Package Highlight");
    public static readonly GUIStyle Dropdown = Skin.FindStyle("Dropdown");
    public static readonly GUIStyle Date = Skin.FindStyle("Date");
    public static readonly GUIStyle SearchTag = Skin.FindStyle("Search Tag");
    public static readonly GUIStyle PackageFoldout = Skin.FindStyle("Package Foldout");
    public static readonly GUIStyle DetailsMini = Skin.FindStyle("Details Mini");
    public static readonly GUIStyle DetailsNormal = Skin.FindStyle("Details Normal");
    public static readonly GUIStyle DownloadButton = Skin.FindStyle("Download Button");
    public static readonly GUIStyle ProtokitLogo = Skin.FindStyle("Protokit Logo");
    public static readonly GUIStyle HeaderBackground = Skin.FindStyle("Header Background");
    public static readonly GUIStyle ReportButton = Skin.FindStyle("Report Button");
    public static readonly GUIStyle VersionInstalledIcon = Skin.FindStyle("Version Installed Icon");
    public static readonly GUIStyle VersionDropdownButton = Skin.FindStyle("Version Dropdown Button");
    public static readonly GUIStyle VersionDropdownWindow = Skin.FindStyle("Version Dropdown Window");
    public static readonly GUIStyle ApplyChangesBackground = Skin.FindStyle("Apply Changes Background");
  }
}
