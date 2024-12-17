// (c) Meta Platforms, Inc. and affiliates. Confidential and proprietary.

namespace Protokit.Core {

  /// <summary>
  /// Implement this interface to hook into the automated keystore selection process.
  /// </summary>
  public interface IKeystoreProvider {

    /// <summary>
    /// The returned keystore data will be processed by the automated keystore selection
    /// process built-in to the Protokit Core package.
    /// </summary>
    KeystoreData GetKeystore();
  }
}
