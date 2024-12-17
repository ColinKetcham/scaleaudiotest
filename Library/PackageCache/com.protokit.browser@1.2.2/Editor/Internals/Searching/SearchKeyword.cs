// (c) Meta Platforms, Inc. and affiliates. Confidential and proprietary.

using System;

namespace Protokit.Browser {

  [Serializable]
  public struct SearchKeyword {
    public string Id;
    public string Display;
    public bool IsEnabled;
    public int TotalMatches;

    public SearchKeyword(string id, string name, bool isEnabled, int totalMatches) {
      Id = id;
      Display = name;
      IsEnabled = isEnabled;
      TotalMatches = totalMatches;
    }
  }
}
