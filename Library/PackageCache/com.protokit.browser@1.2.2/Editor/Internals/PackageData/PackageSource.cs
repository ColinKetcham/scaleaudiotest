// (c) Meta Platforms, Inc. and affiliates. Confidential and proprietary.

namespace Protokit.Browser {

  public enum PackageSource {
    DirectDependency,
    IndirectDependency,
    Embedded,
    Local
  }

  public static class PackageSourceExtensions {

    public static bool IsEmbeddable(this PackageSource source) {
      return source == PackageSource.DirectDependency;
    }
  }
}
