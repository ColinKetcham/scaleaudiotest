// (c) Meta Platforms, Inc. and affiliates. Confidential and proprietary.

namespace Protokit.Core {

  public class KeystoreData {

    /// <summary>
    /// The title of this keystore when shown to the developer in the project settings menu
    /// </summary>
    public string Title;

    /// <summary>
    /// Used to identify this keystore when the user has selected a custom keystore via the project 
    /// settings menu.
    /// </summary>
    public string Identifier;

    /// <summary>
    /// The priority of this keystore.  If the user has not selected a custom keystore, the keystore with
    /// the highest priority will be chosen.  The priority of the built-in keystore is 0, so if you
    /// make your keystore priority higher than 0, it will be selected by default instead of the default
    /// keystore.  If you make your keystore priority less than 0, it will not be selected by default
    /// and the developer will need to manually select it in the Project Settings window.
    /// </summary>
    public int Priority;

    /// <summary>
    /// Value is assigned to PlayerSettings.Android.keystoreName on build
    /// 
    /// This value typically represents the path to the keystore file.
    /// </summary>
    public string KeystoreName;

    /// <summary>
    /// Value is assigned to PlayerSettings.Android.keystorePass on build
    /// </summary>
    public string KeystorePassword;

    /// <summary>
    /// Value is assigned to PlayerSettings.Android.keyaliasName on build
    /// </summary>
    public string KeyaliasName;

    /// <summary>
    /// Value is assigned to PlayerSettings.Android.keyaliasPass on build
    /// </summary>
    public string KeyaliasPassword;
  }
}
