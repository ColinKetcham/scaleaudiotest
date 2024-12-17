// (c) Meta Platforms, Inc. and affiliates. Confidential and proprietary.

using System.IO;

namespace Protokit.Core {

  public class DefaultKeystoreProvider : IKeystoreProvider {

    public const string ID = "ProtokitCoreBasicDebugKeystore";
    public const string PASSWORD = "ProtokitCoreKeystorePassword";
    public const string ALIAS = "protokitcore_keystorealias";

    public KeystoreData GetKeystore() {
      string pathToPackage = PackageUtility.GetFullPathToPackage("com.protokit.core");
      string pathToKeystore = Path.Combine(pathToPackage, "Editor", "BuildAutomation", "protokit_basic_debug.keystore");

      return new KeystoreData() {
        Title = "Basic Debug",
        Identifier = ID,
        Priority = 0,

        KeystoreName = pathToKeystore,
        KeystorePassword = PASSWORD,
        KeyaliasName = ALIAS,
        KeyaliasPassword = PASSWORD
      };
    }
  }
}
