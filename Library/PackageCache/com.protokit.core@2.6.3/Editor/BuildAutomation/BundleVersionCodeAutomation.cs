// (c) Meta Platforms, Inc. and affiliates. Confidential and proprietary.

using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

namespace Protokit.Core {

  public class BundleVersionCodeAutomation : IPreprocessBuildWithReport {

    public int callbackOrder => 100;

    public void OnPreprocessBuild(BuildReport report) {
      if (CoreProjectSettings.IncrementVersionOnBuild) {
        IncrementBundleVersionCode();
      }
    }

    public static void IncrementBundleVersionCode() {
      PlayerSettings.Android.bundleVersionCode++;
    }
  }
}
