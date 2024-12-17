// (c) Meta Platforms, Inc. and affiliates. Confidential and proprietary.

using System;
using UnityEngine;
using UnityEditor;

namespace Protokit.Browser {

  public class VersionPopupWindow : EditorWindow {

    private const int MAX_LINES_EXTENT = 6;
    private const int MAX_LINES = MAX_LINES_EXTENT * 2 + 1;

    private const int WINDOW_WIDTH_PX = 147;
    private const float LINE_HEIGHT_PX = 15;
    private const float HEADER_FOOTER_HEIGHT_PX = 3;

    private static GUIContent VerifiedContent = new GUIContent("VERIFIED");

    private static Color PreviewVersionColor = EditorGUIUtility.isProSkin ? new Color(0.75f, 0.75f, 0.75f) :
                                                                            new Color(0.25f, 0.25f, 0.25f);

    private static Color VerifiedVersionColor = EditorGUIUtility.isProSkin ? Color.green :
                                                                             new Color(0.05f, 0.5f, 0.05f);

    private static Color RegularVersionColor = EditorGUIUtility.isProSkin ? Color.white :
                                                                            new Color(0.05f, 0.05f, 0.05f);

    public static void Show(Rect buttonRect, PackageInfo package, string selectedVersion, string installedVersion, Action<PackageInfo, string> onVersionSelected) {
      if (HasOpenInstances<VersionPopupWindow>()) {
        GetWindow<VersionPopupWindow>().Close();
      }

      (var windowRect, var scroll) = CalculateWindowRectAndScroll(buttonRect, package, selectedVersion);

      var window = CreateInstance<VersionPopupWindow>();
      window.position = windowRect;
      window.ShowPopup();
      window.Focus();

      window._package = package;
      window._scroll = scroll;
      window._installed = installedVersion;
      window._onVersionSelected = onVersionSelected;
    }

    /// <summary>
    /// The version dropdown has a special behavior where it will always appear with the version text
    /// of the selected version in the exact same position as the text on the dropdown button.  This
    /// is to increase the ease-of-use when switching versions, as you don't need to scan up/down in
    /// order to find where the current version is located.
    /// 
    /// This method calculates the correct window position and scroll amount to ensure that the selected
    /// version is in the correct position.  Since the window shows at most MAX_LINES versions, scrolling
    /// might be required to show the correct version.  If possible, this method will choose a scroll
    /// value that results in the selected version being in the VERTICAL CENTER of the window, rather
    /// than at the top.  Of course, if the selected version is near the start or end of the list the
    /// version cannot be placed in the exact center.
    /// </summary>
    private static (Rect, Vector2) CalculateWindowRectAndScroll(Rect buttonRect, PackageInfo package, string selectedVersion) {
      //Manual tweak to make sure the version text is in the same position as in the button to-the-pixel
      Vector2 tweakedPosition = buttonRect.position + new Vector2(-9, 2);

      int indexOfSelectedVersion = 0;
      for (int i = 0; i < package.Versions.Count; i++) {
        if (package.Versions[i].Version == selectedVersion) {
          indexOfSelectedVersion = i;
          break;
        }
      }

      int indexOfFirstVisibleVersion;
      int numberOfVisibleVersions;
      if (package.Versions.Count <= MAX_LINES) {
        //If we have few enough versions that it always fits in our window, values
        //are trivial to calculate.  Index of first version is always zero, and we
        //by definition can see every element.

        indexOfFirstVisibleVersion = 0;
        numberOfVisibleVersions = package.Versions.Count;
      } else {
        //Otherwise, we need to calculate the index of the first visible version manually.
        //We subtract the line extent from the index of the selected version, but make
        //sure to clamp it so that we never select an invalid index.  This ensures the selected
        //version is placed in the center of the window if possible

        indexOfFirstVisibleVersion = Mathf.Clamp(indexOfSelectedVersion - MAX_LINES_EXTENT, 0, package.Versions.Count - MAX_LINES);
        numberOfVisibleVersions = MAX_LINES;
      }

      //The vertical position is taken by offsetting upwards from the position position based on where the selected version
      //lies inside the visible window.  We offset upwards by one line for every visible line above the visible selected version
      Rect windowRect = new Rect();
      windowRect.x = tweakedPosition.x;
      windowRect.y = tweakedPosition.y - (indexOfSelectedVersion - indexOfFirstVisibleVersion) * LINE_HEIGHT_PX - HEADER_FOOTER_HEIGHT_PX;
      windowRect.width = WINDOW_WIDTH_PX;
      windowRect.height = numberOfVisibleVersions * LINE_HEIGHT_PX + HEADER_FOOTER_HEIGHT_PX * 2;

      Vector2 windowScroll = new Vector2();
      windowScroll.x = 0;
      windowScroll.y = indexOfFirstVisibleVersion * LINE_HEIGHT_PX;

      return (windowRect, windowScroll);
    }

    private void OnLostFocus() {
      Close();
    }

    private PackageInfo _package;
    private string _installed;
    private Vector2 _scroll;
    private Action<PackageInfo, string> _onVersionSelected;

    private void OnGUI() {
      Repaint();

      GUI.skin = BrowserStyles.Skin;

      GUILayout.BeginVertical(BrowserStyles.VersionDropdownWindow);

      _scroll = GUILayout.BeginScrollView(_scroll);

      foreach (var versionInfo in _package.Versions) {
        //We want to do our own custom layout within each line, both for efficiency 
        //as well as for getting precise control over hit-boxes for each element.
        Rect lineRect = GUILayoutUtility.GetRect(0, LINE_HEIGHT_PX);

        //Via sem-ver rules
        bool isPreviewVersion = versionInfo.Version.Contains("-");

        if (isPreviewVersion) {
          GUI.contentColor = PreviewVersionColor;
        } else if (versionInfo.Version == _package.VerifiedVersion) {
          GUI.contentColor = VerifiedVersionColor;
        } else {
          GUI.contentColor = RegularVersionColor;
        }

        //The button uses the entire line rect, because we want the entire width
        //of the line to be reactive to the users mouse.
        if (GUI.Button(lineRect, versionInfo.Version, BrowserStyles.VersionDropdownButton)) {
          //Selecting the version simply delegates to the provided callback
          //and closes the window, version window doesn't actually care what
          //happens once a version is selected
          _onVersionSelected(_package, versionInfo.Version);
          Close();
        }

        if (versionInfo.Version == _package.VerifiedVersion && !isPreviewVersion) {
          Rect verifiedLabelRect = lineRect;

          //We place the label rect against the right-edge, using the calculated
          //label width to ensure we calculate the position correctly
          verifiedLabelRect.width = EditorStyles.label.CalcSize(VerifiedContent).x;
          verifiedLabelRect.x = lineRect.xMax - verifiedLabelRect.width;

          GUI.Label(verifiedLabelRect, VerifiedContent, EditorStyles.label);
        }

        GUI.contentColor = Color.white;

        if (versionInfo.Version == _installed) {
          Rect iconRect = lineRect;
          iconRect.width = LINE_HEIGHT_PX; //use the height of the line as the width for a square icon
          GUI.Box(iconRect, GUIContent.none, BrowserStyles.VersionInstalledIcon);
        }
      }
      GUI.color = Color.white;

      EditorGUILayout.EndScrollView();

      GUILayout.EndVertical();
    }
  }
}
