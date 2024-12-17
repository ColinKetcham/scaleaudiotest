// (c) Meta Platforms, Inc. and affiliates. Confidential and proprietary.

using System;
using UnityEngine;

namespace Protokit.Browser {

  [Serializable]
  public class SearchTagGUIContent {

    public void OnGUI(BrowserState state) {
      GUILayout.Space(4);
      using (new GUILayout.HorizontalScope()) {
        foreach (var tag in state.SearchParameters.ActiveTags) {
          DrawTag(tag);
        }

        GUILayout.FlexibleSpace();
      }
    }

    private void DrawTag(SearchTag tag) {
      if (GUILayout.Button(tag.Name, BrowserStyles.SearchTag, BrowserUtil.NoExpandWidth)) {
        tag.Remove();
      }
    }
  }
}
