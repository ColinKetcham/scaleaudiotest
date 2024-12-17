// (c) Meta Platforms, Inc. and affiliates. Confidential and proprietary.

using UnityEngine;
using UnityEditor;

namespace Protokit.Core {

  public static class DistributionMenuItem {
    
    [MenuItem("Protokit/Utilities/App Distribution")]
    public static void ShowDistributionInfo() {
      Application.OpenURL("https://fburl.com/protokitappdistribution");
    }
  }
}
