// (c) Meta Platforms, Inc. and affiliates. Confidential and proprietary.

using System.IO;

namespace Protokit.Browser {

  public static class Caching {

    /// <summary>
    /// A folder that is shared between all Unity versions on the users device.
    /// </summary>
    public static readonly string DeviceCacheFolder;

    /// <summary>
    /// A folder that stores the cache of the server responses for which packages there are.
    /// </summary>
    public static readonly string NetCacheFolder;

    static Caching() {
      DeviceCacheFolder = Path.Combine(Path.GetTempPath(), "ProtokitBrowser");
      NetCacheFolder = Path.Combine(DeviceCacheFolder, "NetCache");

      Directory.CreateDirectory(DeviceCacheFolder);
      Directory.CreateDirectory(NetCacheFolder);
    }
  }
}
