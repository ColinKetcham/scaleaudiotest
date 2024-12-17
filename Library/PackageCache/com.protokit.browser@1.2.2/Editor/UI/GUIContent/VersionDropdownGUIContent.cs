// (c) Meta Platforms, Inc. and affiliates. Confidential and proprietary.

using System;
using UnityEngine;
using UnityEditor;

namespace Protokit.Browser {

  public class VersionDropdownGUIContent {

    public void OnGUI(BrowserState state, Rect rect, PackageInfo package, string selectedVersion, Action<PackageInfo, string> onVersionSelected) {
      if (EditorGUI.DropdownButton(rect, new GUIContent(selectedVersion), FocusType.Keyboard, BrowserStyles.Dropdown)) {
        Rect buttonRect = rect;

        state.DatabaseRequest.Result.TryGetInstalledVersion(package.PackageName, out string installedVersion);

        buttonRect.position = GUIUtility.GUIToScreenPoint(buttonRect.position);

        VersionPopupWindow.Show(buttonRect, package, selectedVersion, installedVersion, onVersionSelected);
      }
    }
  }
}
