// (c) Meta Platforms, Inc. and affiliates. Confidential and proprietary.

using System;
using System.IO;
using System.Net;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Collections.Concurrent;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEngine;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UPackageSource = UnityEditor.PackageManager.PackageSource;

namespace Protokit.Browser {

  public static class PackageDatabase {

    #region API

    public static void NotifyFileChanges() {
      Client.Resolve();
      _databaseRequestTask = null;
    }

    public static void Install(string packageName, string version, bool notifyChanges = true) {
      if (!Query().IsCompleted) {
        throw new InvalidOperationException("You must wait for the database to be completely loaded before installing new packages.");
      }

      PackageInstallTracker.RecordPackageInstall(packageName);
      InstallInternal(packageName, version, notifyChanges);
    }

    public static void InstallLocal(string pathToPackageJson, bool notifyChanges = true) {
      var json = JObject.Parse(File.ReadAllText(pathToPackageJson));
      var name = json["name"].Value<string>();
      Install(name, $"file:{Path.GetDirectoryName(pathToPackageJson)}", notifyChanges);
    }

    public static void Uninstall(string packageName, bool notifyChanges = true) {
      UninstallInternal(packageName, notifyChanges);
    }

    public static float CurrentProgress { get; private set; }
    public static string CurrentProgressText { get; private set; }

    public static Task<DatabaseRequestResult> Query(bool forceRefresh = false) {
      if (_databaseRequestTask == null || (_databaseRequestTask.IsCompleted && forceRefresh)) {
        _databaseRequestTask = FetchPackageInfo(forceRefresh);
      }

      return _databaseRequestTask;
    }

    public static async Task<VersionInfo> GetVersionInfo(PackageInfo package, string version) {
      return await GetVersionInfo(package.PackageName, version);
    }

    public static async Task<VersionInfo> GetVersionInfo(string package, string version) {
      var result = await Query();
      var pInfo = result.Packages.Single(p => p.PackageName == package);
      return pInfo.Versions.Single(p => p.Version == version);
    }

    #endregion

    #region IMPLEMENTATION

    // A number of tasks perform better when they are multithreaded.  This controls the number of worker
    // threads that can be active at once.
    private const int WORKER_THREADS = 16;

    private const string MANIFEST_PATH = "Packages/manifest.json";
    private const string REGISTRY_URL_SNIPPET = "npm.thefacebook.com";
    private const string REGISTRY_URL = "https://npm.thefacebook.com/";

    private const int CURRENT_SERIALIZATION_VERSION = 6;

    private const string BROWSER_LOG_PREFIX = "PB:  ";

    private static Task<DatabaseRequestResult> _databaseRequestTask;

    private static void LoadManifest(out JObject manifest, out JObject dependencies, out JArray protoKitScopes) {
      manifest = JObject.Parse(File.ReadAllText(MANIFEST_PATH));
      dependencies = manifest["dependencies"] as JObject;
      var registries = manifest["scopedRegistries"] as JArray;

      if (registries == null) {
        throw new InvalidOperationException("Could not find scoped registries in Manifest, is ProtoKit installed correctly?");
      }

      JObject protoKitRegistry = null;
      foreach (var token in registries) {
        var registry = token as JObject;
        if (registry == null) {
          continue;
        }

        if (registry["url"].ToString().Contains(REGISTRY_URL_SNIPPET)) {
          protoKitRegistry = registry;
          break;
        }
      }

      if (protoKitRegistry == null) {
        throw new InvalidOperationException("Could not find ProtoKit Registry!");
      }

      protoKitScopes = protoKitRegistry["scopes"] as JArray;
      if (protoKitScopes == null) {
        throw new InvalidOperationException("Could not find scopes list");
      }
    }

    private static void WriteManifest(JObject manifest, bool notifyChanges) {
      File.WriteAllText(MANIFEST_PATH, manifest.ToString());
      if (notifyChanges) {
        NotifyFileChanges();
      }
    }

    private static void InstallInternal(string packageName, string version, bool notifyChanges) {
      try {
        Console.WriteLine($"{BROWSER_LOG_PREFIX}Installing package {packageName} at version {version}.");

        var database = _databaseRequestTask.Result;

        LoadManifest(out var manifest, out var dependencies, out var protoKitScopes);

        foreach (var requiredNamespace in database.GetConservativeProtoKitNamespaceList(packageName)) {
          if (!protoKitScopes.Any(t => t.Value<string>() == requiredNamespace)) {
            Console.WriteLine($"{BROWSER_LOG_PREFIX}Adding namespace {requiredNamespace} because it was required to install {packageName}");
            protoKitScopes.Add(requiredNamespace);
          } else {
            Console.WriteLine($"{BROWSER_LOG_PREFIX}Not adding namespace {requiredNamespace} because it was already present.");
          }
        }

        //Remove any pre-existing entry first
        dependencies.Remove(packageName);

        //Then add with updated version
        dependencies.Add(packageName, version);

        WriteManifest(manifest, notifyChanges);
      } catch (Exception e) {
        Debug.LogException(e);
      }
    }

    private static void UninstallInternal(string packageName, bool notifyChanges) {
      try {
        Console.WriteLine($"{BROWSER_LOG_PREFIX}Uninstalling package {packageName}");

        LoadManifest(out var manifest, out var dependencies, out _);

        dependencies.Remove(packageName);

        WriteManifest(manifest, notifyChanges);
      } catch (Exception e) {
        Debug.LogException(e);
      }
    }

    private static async Task<DatabaseRequestResult> FetchPackageInfo(bool waitForServerResponse = false) {
      var packagesFromCacheTask = FetchPackageInfoFromCache();
      var installedInProjectTask = FetchInstalledPackageInfo();

      CurrentProgressText = "Loading From Cache...";
      await packagesFromCacheTask;

      CurrentProgressText = "Querying Unity API...";
      await installedInProjectTask;

      var packages = packagesFromCacheTask.Result;
      var inProject = installedInProjectTask.Result;

      if (waitForServerResponse || packages.Count == 0) {
        var canConnectToServer = await CheckConnectionToServer();
        if (canConnectToServer) {
          var jsonFromServerTask = FetchPackageJsonFromServer();
          CurrentProgressText = "Waiting For Server...";
          await jsonFromServerTask;

          var json = jsonFromServerTask.Result;
          await RefreshVersionsForUpdatedPackages(json, packages);
        } else {
          Debug.LogWarning($"Could not reach the package server at {REGISTRY_URL} to refresh packages, " +
                           $"make sure you are on VPN to refresh the package list, or check to see if you " +
                           $"are having other network issues that might interrupt your connection to the " +
                           $"server.");
        }
      } else {
        //TODO: would be nice to fetch the results from the server anyway, but not
        //wait for them. We could store the results locally async, and use them the
        //next time we refresh. In this way, the user might _never_ need to wait for
        //the server, but still be mostly up-to-date.
      }

      CurrentProgress = 1;

      InsertInstalledVersionsIfNeeded(packages, installedInProjectTask.Result);

      foreach (var package in packages.Values) {
        package.Versions.Sort();
      }

      //Calculate default version after sorting, since calculating default uses ordering
      CalculateDefaultVersions(inProject, packages);

      //Remove invalid packages after calculating default, as any package without a default
      //is invalid!
      RemoveInvalidPackages(packages);

      return new DatabaseRequestResult(packages.Values.ToList(), installedInProjectTask.Result.ToList());
    }

    /// <summary>
    /// Check to see if we can actually reach the server at all, that way we can offer a useful error
    /// message to the user if they are not on VPN, or if the server is down.
    /// </summary>
    private static async Task<bool> CheckConnectionToServer() {
      try {
        return await Task.Run(() => {
          HttpWebRequest request = WebRequest.CreateHttp(REGISTRY_URL);
          HttpWebResponse response = (HttpWebResponse)request.GetResponse();
          return response.StatusCode == HttpStatusCode.OK;
        });
      } catch (Exception) {
        return false;
      }
    }

    /// <summary>
    /// Fetch the information for the currently installed packages, which includes direct
    /// and indirect packages.  This is important because it affects how packages are visualized
    /// within the browser.
    /// </summary>
    private static async Task<PackageCollection> FetchInstalledPackageInfo() {
      //We like to avoid the Unity API as much as possible because it is slow, but in this case it is
      //still the best way to get accurate information.  We perform this in offline mode to help ensure
      //it does not take a long time due to trying to send web requests.
      var task = Client.List(offlineMode: true, includeIndirectDependencies: true);

      while (!task.IsCompleted) {
        await Task.Delay(20);
      }
      if (task.Status != StatusCode.Success) {
        //TODO
        throw new Exception();
      }

      return task.Result;
    }

    /// <summary>
    /// PackageInfo is stored in a local device-wide cache so that we can avoid sending out hundreds of
    /// web requests every time the user wants to refresh their browser.  A package is only refreshed if
    /// a new version gets published.
    ///
    /// This method traverses the local cache and loads all of the existing package info into a map.
    /// </summary>
    private static async Task<Dictionary<string, PackageInfo>> FetchPackageInfoFromCache() {
      var files = new ConcurrentQueue<string>(Directory.GetFiles(Caching.NetCacheFolder, "*.json"));
      var packages = new ConcurrentQueue<PackageInfo>();

      int packagesToLoad = files.Count;
      int packagesLoaded = 0;

      var serializer = JsonSerializer.CreateDefault();

      //We simply go wide on a number of worker threads where each thread loads the json files
      //of the stored PackageInfo objects.  The cache is just a simple list of all the serialized
      //PackageInfo objects, and so very little processing is required.
      var tasks = new List<Task>();
      for (int i = 0; i < WORKER_THREADS; i++) {
        tasks.Add(Task.Run(() => {
          while (files.TryDequeue(out var file)) {
            using (var stream = File.OpenRead(file))
            using (var streamReader = new StreamReader(stream))
            using (var jsonReader = new JsonTextReader(streamReader)) {
              var package = serializer.Deserialize<PackageInfo>(jsonReader);

              //Ignore any package with a different serialized version, forces us to actually
              //refresh packages when the serialization format changes, instead of risking loading
              //bad or out of date information.
              if (package.SerializationVersion == CURRENT_SERIALIZATION_VERSION) {
                packages.Enqueue(package);
              }
            }

            Interlocked.Increment(ref packagesLoaded);
            CurrentProgress = packagesLoaded / (float)packagesToLoad;
          }
        }));
      }

      await Task.WhenAll(tasks);

      return packages.ToDictionary(p => p.PackageName);
    }

    /// <summary>
    /// Fetches the list of all packages from the server.  This lists all packages, including incompatible
    /// and those packages with namespaces not currently referenced by any scoped registry.  The list contains
    /// only a small amount of information about packages, and so we will still need to fetch the full info
    /// for any given package before it can be displayed, but this list can at least help us determine which
    /// packages have been updated since we last checked.
    /// </summary>
    private static async Task<JObject> FetchPackageJsonFromServer() {
      return await Task.Run(() => {
        HttpWebRequest request = WebRequest.CreateHttp($"{REGISTRY_URL}-/v1/search?size=100000");
        var response = (HttpWebResponse)request.GetResponse();
        var streamReader = new StreamReader(response.GetResponseStream());
        var json = JObject.Load(new JsonTextReader(streamReader));
        response.Close();
        return json;
      });
    }

    private static PackageInfo FetchPackageInfoFromServer(string packageName) {
      var request = WebRequest.CreateHttp(REGISTRY_URL + packageName);
      var response = (HttpWebResponse)request.GetResponse();
      var streamReader = new StreamReader(response.GetResponseStream());

      var json = JObject.Parse(streamReader.ReadToEnd());
      response.Close();

      PackageInfo package = new PackageInfo();
      package.SerializationVersion = CURRENT_SERIALIZATION_VERSION;
      package.PackageName = json["name"].ToString();

      foreach (var obj in json["versions"] as JObject) {
        VersionInfo info = new VersionInfo(packageName, obj.Key, obj.Value);
        package.Versions.Add(info);
      }

      foreach (var version in package.Versions) {
        version.DatePublished = DateTime.Parse(json["time"][version.Version].ToString());
      }

      return package;
    }

    /// <summary>
    /// Given the json taken from querying the server, which has the updated package info, plus the current
    /// version of the packages (if any) taken from the cache, automatically fetch the information from packages
    /// that are missing or have had updates.
    /// </summary>
    private static async Task RefreshVersionsForUpdatedPackages(JObject allPackages, Dictionary<string, PackageInfo> currentPackages) {
      var packagesToRefresh = new ConcurrentQueue<string>();
      foreach (JObject result in (JArray)allPackages["objects"]) {
        var package = (JObject)result["package"];

        string packageName = (string)package["name"];

        //Packages must have lowercase identifiers in order to be installed, ignore any packages with uppercase letters
        if (packageName.Any(c => char.IsUpper(c))) {
          continue;
        }

        string latestVersion = package["dist-tags"]["latest"].ToString();

        //A package should be refreshed if we currently don't have information for that package, or for
        //the most recently published version
        if (!currentPackages.TryGetValue(packageName, out var info) || !info.Versions.Any(v => v.Version == latestVersion)) {
          if (info == null) {
            Logging.Log($"Fetching package data for {packageName} because it was not found in cache.");
          } else {
            Logging.Log($"Fetching package data for {packageName} because version {latestVersion} was not found in cache.");
          }

          packagesToRefresh.Enqueue(packageName);
        }
      }

      List<Task> netTasks = new List<Task>();

      int totalRequest = packagesToRefresh.Count;
      int totalComplete = 0;

      if (totalRequest > 0) {
        CurrentProgressText = "Fetching New Versions...";
        CurrentProgress = 0;
      }

      //We send a single web request per-package to fetch the updated information.  We do this across
      //multiple threads, with a maximum of WORKER_THREADS requests in flight at a time.  This speeds things
      //up significantly, since a large part of the time spent is simply waiting for the server to respond.
      for (int i = 0; i < WORKER_THREADS; i++) {
        netTasks.Add(Task.Run(() => {
          while (packagesToRefresh.TryDequeue(out var toRefresh)) {
            try {
              var pInfo = FetchPackageInfoFromServer(toRefresh);

              lock (currentPackages) {
                currentPackages[toRefresh] = pInfo;
              }

              //Write out the updated json to the cache right after receiving it
              File.WriteAllText(Path.Combine(Caching.NetCacheFolder, toRefresh + ".json"),
                                JsonConvert.SerializeObject(pInfo, Formatting.Indented));

              Interlocked.Increment(ref totalComplete);
              CurrentProgress = totalComplete / (float)totalRequest;
            } catch (ThreadAbortException) {
              //Don't log for ThreadAbortException, it happens regularly if the user recompiles
              //while ProtoKit Browser is refreshing, no need to alarm anybody!
              throw;
            } catch (Exception e) {
              //Exceptions don't get logged from within worker threads by default, catch and log
              //them so that the user has a chance to see what went wrong.  Don't re-throw, or else
              //the core update loop gets messed up
              Logging.Err($"Failed to fetch package info for {toRefresh} due to unexpected error\n\n{e.ToString()}");
            }
          }
        }));
      }

      await Task.WhenAll(netTasks);
    }

    /// <summary>
    /// Supplements the total package list with the list of packages installed as reported by Unity.  Important when
    /// embedded or local packages have information not found on the server.
    /// </summary>
    private static void InsertInstalledVersionsIfNeeded(Dictionary<string, PackageInfo> packages, PackageCollection installedPackages) {
      foreach (var installed in installedPackages) {
        if (!packages.TryGetValue(installed.name, out var package)) {
          package = new PackageInfo();
          package.SerializationVersion = CURRENT_SERIALIZATION_VERSION;
          package.PackageName = installed.name;
        }

        var existingVersion = package.Versions.FirstOrDefault(v => v.Version == installed.version);
        if (existingVersion != null) {
          //For packages simply installed by registry, there is nothing to do, no benefit for
          //overwriting the version.
          if (installed.source == UPackageSource.Registry) {
            continue;
          }

          //Otherwise remove it so that we can overwrite it with the Unity API version.  When we are dealing
          //with an embedded or local package we can frequently get a version both from the server and from the
          //Unity API.  We always want to use the Unity API version, since that represents the local version, which
          //will have local changes to things like descriptions or dependencies.
          package.Versions.Remove(existingVersion);
        }

        package.Versions.Add(new VersionInfo(installed));
      }
    }

    /// <summary>
    /// Removes invalid packages from the mapping.  We do this as a post-filter on the package
    /// map rather than in the fetch step to avoid re-fetching invalid packages over and over again.
    /// It is important to keep a record of invalid packages in the cache to remember that even
    /// though they are invalid, we shouldn't try to fetch them again.
    /// </summary>
    private static void RemoveInvalidPackages(Dictionary<string, PackageInfo> packages) {
      List<string> toRemove = new List<string>();
      foreach (var p in packages.Values) {
        for (int i = 0; i < p.Versions.Count; i++) {
          if (!p.Versions[i].IsValid) {
            p.Versions.RemoveAt(i);
            i--;
          }
        }

        if (!p.IsValid) {
          toRemove.Add(p.PackageName);
        }
      }

      foreach (var p in toRemove) {
        packages.Remove(p);
      }
    }

    /// <summary>
    /// The 'default' version is the version that is shown to the user when they browse packages.  This version
    /// changes based on the situation, and is calculated by this method.  If a package is installed, that installed
    /// version is always preferred.
    /// </summary>
    private static void CalculateDefaultVersions(PackageCollection inProject, Dictionary<string, PackageInfo> packages) {
      foreach (var package in packages.Values) {
        var installed = inProject.FirstOrDefault(p => p.name == package.PackageName);

        //Choose the installed version if this package is currently installed
        if (installed != null) {
          package.Default = package.Versions.FirstOrDefault(v => v.Version == installed.version);
          if (package.Default != null) {
            continue;
          }
        }

        //Otherwise choose the best version, considering compatibility and preview status
        package.Default = package.CompatibleVersions.Where(v => !v.IsPreview).FirstOrDefault() ??
                          package.CompatibleVersions.FirstOrDefault() ??
                          package.Versions.Where(v => !v.IsPreview).FirstOrDefault() ??
                          package.Versions.FirstOrDefault();
      }
    }

    #endregion
  }
}
