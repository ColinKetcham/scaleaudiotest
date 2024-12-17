// (c) Meta Platforms, Inc. and affiliates. Confidential and proprietary.

using System;
using System.Linq;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace Protokit.Browser {

  [Serializable]
  public class PackageInfo {

    public int SerializationVersion;
    public string PackageName;
    public string VerifiedVersion;

    [JsonIgnore]
    public string LatestVersion => Versions[0].Version;

    public List<VersionInfo> Versions = new List<VersionInfo>();

    [JsonIgnore]
    public IEnumerable<VersionInfo> CompatibleVersions => Versions.Where(v => v.IsCompatible);

    [JsonIgnore]
    public VersionInfo Default;

    [JsonIgnore]
    public bool IsValid {
      get {
        if (Default == null) {
          return false;
        }

        if (string.IsNullOrWhiteSpace(PackageName)) {
          return false;
        }

        if (Versions.Count == 0) {
          return false;
        }

        for (int i = 0; i < Versions.Count; i++) {
          if (!Versions[i].IsValid) {
            return false;
          }
        }

        return true;
      }
    }

    [JsonIgnore]
    private bool? _isDeprecated;

    [JsonIgnore]
    public bool IsDeprecated {
      get {
        if (!_isDeprecated.HasValue) {
          _isDeprecated = Versions.Any(v => v.IsDeprecated);
        }
        return _isDeprecated.Value;
      }
    }
  }
}
