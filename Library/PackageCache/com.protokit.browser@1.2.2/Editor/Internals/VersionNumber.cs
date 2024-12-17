// (c) Meta Platforms, Inc. and affiliates. Confidential and proprietary.

using System;
using UnityEngine;

namespace Protokit.Browser {

  /// <summary>
  /// Represents a version number with major, minor, and patch numbers.  Version numbers
  /// can be parsed and compared to each other.
  /// </summary>
  public struct VersionNumber : IComparable<VersionNumber>, IEquatable<VersionNumber> {

    public static readonly VersionNumber CurrentUnityVersion = Parse(Application.unityVersion);

    public int Major, Minor, Patch;

    /// <summary>
    /// Returns the major, minor, or patch number by index.
    /// </summary>
    public int this[int index] {
      get {
        switch (index) {
          default:
          case 0: return Major;
          case 1: return Minor;
          case 2: return Patch;
        }
      }
      set {
        switch (index) {
          case 0: Major = value; break;
          case 1: Minor = value; break;
          case 2: Patch = value; break;
        }
      }
    }

    public static bool operator >(VersionNumber a, VersionNumber b) {
      return a.CompareTo(b) > 0;
    }

    public static bool operator >=(VersionNumber a, VersionNumber b) {
      return a.CompareTo(b) >= 0;
    }

    public static bool operator <(VersionNumber a, VersionNumber b) {
      return a.CompareTo(b) < 0;
    }

    public static bool operator <=(VersionNumber a, VersionNumber b) {
      return a.CompareTo(b) <= 0;
    }

    public static bool operator ==(VersionNumber a, VersionNumber b) {
      return a.Equals(b);
    }

    public static bool operator !=(VersionNumber a, VersionNumber b) {
      return !a.Equals(b);
    }

    /// <summary>
    /// Tries to parse a version number from the given string. A valid version
    /// number is three numbers separated by two periods, and can have any number
    /// of non-numeric characters as a suffix, which are currently ignored.
    /// </summary>
    public static bool TryParse(string version, out VersionNumber result) {
      result = default;

      int index = 0;
      for (int i = 0; i < version.Length; i++) {
        char c = version[i];
        if (c == '.') {
          index++;
          if (index >= 3) {
            return false;
          }
          continue;
        }

        if (c >= '0' && c <= '9') {
          result[index] = result[index] * 10 + (c - '0');
        } else if (index == 2) {
          break;
        } else {
          return false;
        }
      }

      return true;
    }

    public static VersionNumber Parse(string version) {
      if (!TryParse(version, out var result)) {
        throw new FormatException($"Unity Version '{version}' did not have the correct format.");
      }
      return result;
    }

    public int CompareTo(VersionNumber other) {
      if (Major != other.Major) {
        return Major.CompareTo(other.Major);
      } else if (Minor != other.Minor) {
        return Minor.CompareTo(other.Minor);
      } else {
        return Patch.CompareTo(other.Patch);
      }
    }

    public bool Equals(VersionNumber other) {
      return Major == other.Major &&
             Minor == other.Minor &&
             Patch == other.Patch;
    }

    public override bool Equals(object obj) {
      if (obj is VersionNumber other) {
        return Equals(other);
      } else {
        return false;
      }
    }

    public override int GetHashCode() {
      return (Major, Minor, Patch).GetHashCode();
    }

    public override string ToString() {
      return $"{Major}.{Minor}.{Patch}";
    }
  }
}
