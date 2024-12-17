// (c) Meta Platforms, Inc. and affiliates. Confidential and proprietary.

using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEditor;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UPackageInfo = UnityEditor.PackageManager.PackageInfo;

namespace Protokit.Browser {

  [Serializable]
  public class VersionInfo : IComparable<VersionInfo> {

    public string Version;

    public string Title;

    public string PackageName;

    public string Description;

    public DateTime DatePublished;

    public VersionNumber UnityVersion;

    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public List<string> Keywords = new List<string>();

    public string Category;

    public string Team;

    public string Author;

    public bool IsSupported;

    public List<PackageDependency> Dependencies = new List<PackageDependency>();

    public bool HasSamples;

    public SampleData[] Samples;

    [JsonIgnore]
    private bool? _isDeprecated;

    [JsonIgnore]
    public bool IsDeprecated {
      get {
        if (!_isDeprecated.HasValue) {
          _isDeprecated = Keywords.Any(k => k.ToLower() == "deprecated");
        }
        return _isDeprecated.Value;
      }
    }

    [JsonIgnore]
    private bool? _isEmpty;

    public bool IsEmpty {
      get {
        if (!_isEmpty.HasValue) {
          _isEmpty = Keywords.Any(k => k.ToLower() == "empty");
        }
        return _isEmpty.Value;
      }
    }

    [JsonIgnore]
    public bool IsPreview => Version.Contains("-");

    public VersionInfo() { }

    [NonSerialized]
    private GUIContent _cachedTitleContent = null;
    [JsonIgnore]
    public GUIContent TitleContent {
      get {
        if (_cachedTitleContent == null) {
          _cachedTitleContent = new GUIContent(Title);
        }
        return _cachedTitleContent;
      }
    }

    [JsonIgnore]
    public bool IsCompatible => VersionNumber.CurrentUnityVersion >= UnityVersion;

    [JsonIgnore]
    public bool IsValid {
      get {
        if (string.IsNullOrWhiteSpace(Version)) {
          return false;
        }

        if (string.IsNullOrWhiteSpace(Title)) {
          return false;
        }

        if (string.IsNullOrWhiteSpace(PackageName)) {
          return false;
        }

        return true;
      }
    }

    /// <summary>
    /// Construct a VersionInfo instance given the json returned directly from the server
    /// </summary>
    public VersionInfo(string packageName, string version, JToken json) {
      PackageName = packageName;
      Version = version;

      Title = SanitizeTitle(json["displayName"]?.ToString() ?? "");
      Description = json["description"]?.ToString();
      HasSamples = json["samples"] != null;
      if (HasSamples) {
        var samplesArray = json["samples"] as JArray;
        Samples = samplesArray.Select(s => new SampleData() {
          description = s["description"]?.ToString(),
          displayName = s["displayName"]?.ToString(),
          packageTitle = Title,
          packageVersion = Version,
          resolvedSourcePath = $"Packages/{PackageName}/{s["path"]?.ToString()}"
        }).ToArray();
      }

      var unityVersionObj = json["unity"];
      if (unityVersionObj != null) {
        VersionNumber.TryParse(unityVersionObj.ToString(), out UnityVersion);
      }

      var authorObj = json["author"];
      if (authorObj == null) {
        Author = "";
      } else if (authorObj.Type == JTokenType.String) {
        Author = authorObj.ToString();
      } else {
        Author = authorObj["name"]?.ToString() ?? "";
      }

      var keywordsObj = json["keywords"];
      if (keywordsObj != null) {
        foreach (var keyword in keywordsObj as JArray) {
          Keywords.Add(keyword.ToString());
        }
      }

      var dependenciesObj = json["dependencies"];
      if (dependenciesObj != null) {
        foreach (var dependency in dependenciesObj as JObject) {
          Dependencies.Add(new PackageDependency() {
            Name = dependency.Key,
            Version = dependency.Value.ToString()
          });
        }
      }
    }

    /// <summary>
    /// Construct a VersionInfo instance given the structure returned by Unity itself.
    /// </summary>
    public VersionInfo(UPackageInfo unityPackage) {
      PackageName = unityPackage.name;
      Version = unityPackage.version;

      Title = unityPackage.displayName;
      Description = unityPackage.description;
      HasSamples = false;

      UnityVersion = VersionNumber.Parse(Application.unityVersion);

      Author = unityPackage.author.name;

      Keywords.AddRange(unityPackage.keywords);
      Dependencies.AddRange(unityPackage.dependencies.Select(d => new PackageDependency() {
        Name = d.name,
        Version = d.version
      }));
    }

    private static string SanitizeTitle(string title) {
      int endBrackedIndex = title.IndexOf(']');
      if (endBrackedIndex >= 0) {
        string prefix = title.Substring(1, endBrackedIndex - 1);
        title = prefix + " " + title.Substring(endBrackedIndex + 1).Trim();
      }
      return title;
    }

    [Serializable]
    private struct SampleDataPackageJson {
      public SampleData[] samples;

      public SampleDataPackageJson(SampleData[] samples) {
        this.samples = samples;
      }
    }

    [Serializable]
    public struct SampleData {
      public string displayName;
      public string description;

      public string resolvedSourcePath;
      public string packageTitle;
      public string packageVersion;

      public void Import(bool autoRefresh = true) {
        //We create the directory from scratch each time to make sure it has properties we desire:
        //  Path is in project Assets folder - Always starts with Application.dataPath, which points directly to Assets folder
        string destinationDirectory = Path.Combine(Path.GetFullPath(Application.dataPath), "Samples", packageTitle, packageVersion, displayName);

        Assert.IsFalse(destinationDirectory.Contains(".."), "Target path should not contain ..");

        Directory.CreateDirectory(destinationDirectory);

        //Make sure to delete all files before importing
        foreach (var file in Directory.GetFiles(destinationDirectory, "*", SearchOption.AllDirectories)) {
          File.Delete(file);
        }

        CopyFilesRecursively(new DirectoryInfo(resolvedSourcePath), new DirectoryInfo(destinationDirectory));

        //Remember to refresh database so that files show up!
        if (autoRefresh) {
          AssetDatabase.Refresh();
        }
      }

      private static void CopyFilesRecursively(DirectoryInfo source, DirectoryInfo target) {
        foreach (DirectoryInfo dir in source.GetDirectories()) {
          CopyFilesRecursively(dir, target.CreateSubdirectory(dir.Name));
        }

        foreach (FileInfo file in source.GetFiles()) {
          file.CopyTo(Path.Combine(target.FullName, file.Name), overwrite: true);
        }
      }
    }

    public int CompareTo(VersionInfo other) {
      var aNumber = VersionNumber.Parse(Version);
      var bNumber = VersionNumber.Parse(other.Version);
      if (aNumber == bNumber) {
        //Preview versions are always ordered before non-preview versions
        if (IsPreview != other.IsPreview) {
          return IsPreview ? 1 : -1;
        }

        var aTokens = Version.Split('.');
        var bTokens = other.Version.Split('.');
        if (aTokens.Length != bTokens.Length) {
          return bTokens.Length.CompareTo(aTokens.Length);
        }

        for (int i = 0; i < aTokens.Length; i++) {
          var aToken = aTokens[i];
          var bToken = bTokens[i];

          var aIsNumeric = aToken.All(c => char.IsDigit(c));
          var bIsNumeric = bToken.All(c => char.IsDigit(c));

          int compResult;
          if (aIsNumeric && bIsNumeric) {
            compResult = int.Parse(bToken).CompareTo(int.Parse(aToken));
          } else if (!aIsNumeric && !bIsNumeric) {
            compResult = bToken.CompareTo(aToken);
          } else {
            compResult = aIsNumeric ? 1 : -1;
          }

          if (compResult != 0) {
            return compResult;
          }
        }

        return 0;
      } else {
        return bNumber.CompareTo(aNumber);
      }
    }
  }
}
