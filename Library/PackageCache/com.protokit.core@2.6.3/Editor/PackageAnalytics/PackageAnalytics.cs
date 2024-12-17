// (c) Meta Platforms, Inc. and affiliates. Confidential and proprietary.

using MongoDB.Bson;
using MongoDB.Driver;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.UI;
using UnityEngine;
using UnityEngine.UIElements;

namespace Protokit.Core.PackageExtensions {

  [InitializeOnLoad]
  public class PackageAnalytics : IPackageManagerExtension {
    private static string serverIP = "100.104.68.77";
    private static int serverPort = 27017;
    private static string machineName;
    private static string dateTime;
    private static string unityVersion;
    private static MongoClient client;
    private static IMongoDatabase database;
    private static IMongoCollection<BsonDocument> collection;
    private static List<string> installedPackages;
    private static MongoClientSettings clientSettings = null;
    private static string[] stringSep = { "~*" };

    // These 3 variables are server based i.e. dependent on MongoDB database
    private static string unityPackageEventName = "unity_package_";
    private static string installEventName = "added";
    private static string removeEventName = "removed";

    // These variables for local to Unity session
    private static string packagePrefixEventName = "protoKit_analytics_package_";
    private static string packageInstallEventName = "protoKit_analytics_package_added";
    private static string packageRemoveEventName = "protoKit_analytics_package_removed";
    private static string packageAvailableEventName = "protoKit_analytics_available_packages";
    private static string eventQueueEventName = "protokit_analytics_event_queue";
    private static string eventQueueNumEventName = "protokit_analytics_event_queue_num";

    public static bool Connected { get; private set; }

    static PackageAnalytics() {
      PackageManagerExtensions.RegisterExtension(new PackageAnalytics());
      dateTime = string.Empty;
      machineName = Environment.MachineName;
      unityVersion = Application.unityVersion;

      // client configuration for connection
      clientSettings = new MongoClientSettings {
        Server = new MongoServerAddress(serverIP, serverPort),
        ClusterConfigurator = builder => {
          builder.ConfigureCluster(settings => settings.With(serverSelectionTimeout: TimeSpan.FromSeconds(2)));
        }
      };
      ReadChangesAndSendLog();
    }

    public static bool MDBConnect() {
      try {
        client = new MongoClient(clientSettings);
        if (client.ListDatabaseNames().ToList<string>().Count > 0) {
          Connected = true;
          database = client.GetDatabase("AnalyticsDB");
          collection = database.GetCollection<BsonDocument>("AnalyticsCollection");
          machineName = Environment.MachineName;
          unityVersion = Application.unityVersion;
        } else {
          Connected = false;
        }
      } catch {
        Debug.LogWarning("Connect to VPN to start recieving internal packages through package manager.");
        Connected = false;
      }

      return Connected;
    }

    public static async void ReadChangesAndSendLog() {
      Connected = await Task.Run(MDBConnect);
      if (!string.IsNullOrEmpty(SessionState.GetString(packageInstallEventName, string.Empty))) {
        List<string> diffPackageList = await FindPackageChange();
        CategorizeAndSendEvents(installEventName, diffPackageList);
      } else if (!string.IsNullOrEmpty(SessionState.GetString(packageRemoveEventName, string.Empty))) {
        List<string> diffPackageList = await FindPackageChange();
        CategorizeAndSendEvents(removeEventName, diffPackageList);
      }
    }

    public static async void CategorizeAndSendEvents(string eventType, List<string> diffPackageList) {
      string packageChanged = SessionState.GetString(packagePrefixEventName + eventType, string.Empty);
      if (!string.IsNullOrEmpty(packageChanged)) {
        UnityEditor.PackageManager.PackageInfo packageInfo = await GetPackageInfo(packageChanged);
        if (packageInfo != null) {
          await SendEvent(unityPackageEventName + eventType, packageInfo.name, packageInfo.version, machineName);
          diffPackageList.Remove(packageInfo.packageId);

          foreach (var dependInfo in packageInfo.dependencies) {
            string dependPackageId = dependInfo.name + "@" + dependInfo.version;
            if (diffPackageList.Any(item => item == dependPackageId)) {
              await SendEvent(unityPackageEventName + eventType + "_asDependency", dependInfo.name, dependInfo.version, machineName);
              diffPackageList.Remove(dependPackageId);
            }
          }

          foreach (var item in diffPackageList) {
            string[] packageId = item.Split('@');
            await SendEvent(unityPackageEventName + eventType + "_asIndirectDependency", packageId[0], packageId[1], machineName);
          }
        }

        SessionState.SetString(packagePrefixEventName + eventType, string.Empty);
      }
    }

    public static async Task<List<string>> FindPackageChange() {
      List<string> packageWereAvailable = new List<string>();

      // Read PlayerPref to retrieve stored package names
      if (!string.IsNullOrEmpty(SessionState.GetString(packageAvailableEventName, string.Empty))) {
        string[] availablePackages = SessionState.GetString(packageAvailableEventName, string.Empty).Split(',');
        foreach (var item in availablePackages) {
          if (!string.IsNullOrEmpty(item)) {
            packageWereAvailable.Add(item);
          }
        }
      }

      // Store installed package names
      installedPackages = await InstalledPackageList();

      var list1 = packageWereAvailable.Except(installedPackages);
      var list2 = installedPackages.Except(packageWereAvailable);
      List<string> diff = list1.Concat(list2).ToList();

      // Store installed package names in PlayerPref
      string availPackages = string.Empty;
      foreach (var item in installedPackages) {
        availPackages += item + ",";
      }

      SessionState.SetString(packageAvailableEventName, availPackages);
      return diff;
    }

    public VisualElement CreateExtensionUI() {
      return new IMGUIContainer();
    }

    public void OnPackageAddedOrUpdated(UnityEditor.PackageManager.PackageInfo packageInfo) {
      SessionState.SetString(packageInstallEventName, packageInfo.packageId);
    }

    public void OnPackageRemoved(UnityEditor.PackageManager.PackageInfo packageInfo) {
      SessionState.SetString(packageRemoveEventName, packageInfo.packageId);
    }

    public void OnPackageSelectionChange(UnityEditor.PackageManager.PackageInfo packageInfo) {
    }

    public static async Task<UnityEditor.PackageManager.PackageInfo> GetPackageInfo(string packageId) {
      bool offlineMode = false;

      if (!Connected) {
        offlineMode = true;
      }

      UnityEditor.PackageManager.Requests.SearchRequest searchReq = Client.Search(packageId, offlineMode);
      while (!searchReq.IsCompleted) {
        await Task.Delay(10);
      }

      if (searchReq.Status == StatusCode.Failure) {
        return null;
      }

      return searchReq.Result[0];
    }

    public static async Task<List<string>> InstalledPackageList() {
      bool offlineMode = false;
      if (!Connected) {
        offlineMode = true;
      }

      UnityEditor.PackageManager.Requests.ListRequest searchReq = Client.List(offlineMode, true);

      while (!searchReq.IsCompleted) {
        await Task.Delay(10);
      }

      if (searchReq.Status == StatusCode.Failure) {
        return null;
      }

      UnityEditor.PackageManager.PackageCollection packageCollection = searchReq.Result;
      List<string> packageIdList = new List<string>();
      foreach (var package in packageCollection) {
        packageIdList.Add(package.packageId);
      }

      return packageIdList;
    }

    public static async Task<bool> SendEvent(string action, string packageName, string packageVersion, string machineId) {
      // If user in not connected to VPN while starting Unity and
      // later connects to VPN, following MDBConnect() gives another try
      // to connect to MongoDB server before sending or failing to send data.
      dateTime = DateTime.Now.Year + "-" + DateTime.Now.Month + "-" + DateTime.Now.Day +
          "-" + DateTime.Now.Hour + "-" + DateTime.Now.Minute + "-" + DateTime.Now.Second;

      var entry = new BsonDocument
                  {
                    { "action", action },
                    { "packageName", packageName },
                    { "packageVersion", packageVersion },
                    { "machineId", machineId },
                    { "unityVersion", unityVersion },
                    { "dateTime", dateTime },
                    { "year", DateTime.Now.Year },
                    { "month", DateTime.Now.Month },
                    { "day", DateTime.Now.Day },
                    { "hour", DateTime.Now.Hour },
                    { "minute", DateTime.Now.Minute },
                    { "second", DateTime.Now.Second },
                    { "projectPath", Application.dataPath.ToString() },
                    { "projectName", Application.productName.ToString() }
                  };
      try {
        await collection.InsertOneAsync(entry);
        await SendEventQueue();
        return true;
      } catch {
        AddToEventQueue(
                        action,
                        packageName,
                        packageVersion,
                        machineId,
                        unityVersion,
                        dateTime,
                        DateTime.Now.Year,
                        DateTime.Now.Month,
                        DateTime.Now.Day,
                        DateTime.Now.Hour,
                        DateTime.Now.Minute,
                        DateTime.Now.Second,
                        Application.dataPath.ToString(),
                        Application.productName.ToString());
        return false;
      }
    }

    public static async Task<bool> SendEvent(string action, UnityEditor.PackageManager.PackageInfo packageInfo, string machineId) {
      return await SendEvent(action, packageInfo.name, packageInfo.version, machineId);
    }

    public static void AddToEventQueue(
        string action, string packageName, string packageVersion, string machineId, string unityVersion, string dateTime, int year, int month, int day, int hour, int minute, int second, string projectPath, string projectName) {
      int numOfEventsStored = 0;
      if (PlayerPrefs.HasKey(eventQueueNumEventName)) {
        numOfEventsStored = PlayerPrefs.GetInt(eventQueueNumEventName);
      }

      numOfEventsStored += 1;
      string eventInfo = action + stringSep[0] + packageName + stringSep[0] + packageVersion + stringSep[0] + machineId + stringSep[0] +
          unityVersion + stringSep[0] + dateTime + stringSep[0] + year.ToString() + stringSep[0] +
          month.ToString() + stringSep[0] + day.ToString() + stringSep[0] + hour.ToString() + stringSep[0] +
          minute.ToString() + stringSep[0] + second.ToString() + stringSep[0] + projectPath + stringSep[0] + projectName;

      PlayerPrefs.SetString(eventQueueEventName + numOfEventsStored.ToString(), eventInfo);
      PlayerPrefs.SetInt(eventQueueNumEventName, numOfEventsStored);
      PlayerPrefs.Save();
    }

    public static async Task<bool> SendEventQueue() {
      if (PlayerPrefs.HasKey(eventQueueNumEventName)) {
        int numOfEventsStored = PlayerPrefs.GetInt(eventQueueNumEventName);
        for (int i = 0; i < numOfEventsStored; i++) {
          string eventInfo = PlayerPrefs.GetString(eventQueueEventName + numOfEventsStored.ToString(), string.Empty);
          string[] evnt = eventInfo.Split(stringSep, System.StringSplitOptions.RemoveEmptyEntries);

          var entry = new BsonDocument
          {
            { "action", evnt[0] },
            { "packageName", evnt[1] },
            { "packageVersion", evnt[2] },
            { "machineId", evnt[3] },
            { "unityVersion", evnt[4] },
            { "dateTime", evnt[5] },
            { "year", Int32.Parse(evnt[6]) },
            { "month", Int32.Parse(evnt[7]) },
            { "day", Int32.Parse(evnt[8]) },
            { "hour", Int32.Parse(evnt[9]) },
            { "minute", Int32.Parse(evnt[10]) },
            { "second", Int32.Parse(evnt[11]) },
            { "projectPath", evnt[12] },
            { "projectName", evnt[13] }
          };

          try {
            await collection.InsertOneAsync(entry);
          } catch {
            return false;
          }

          PlayerPrefs.DeleteKey(eventQueueEventName + numOfEventsStored.ToString());

          numOfEventsStored -= 1;
          PlayerPrefs.SetInt(eventQueueNumEventName, numOfEventsStored);
          PlayerPrefs.Save();
        }
      }

      return true;
    }
  }
}
