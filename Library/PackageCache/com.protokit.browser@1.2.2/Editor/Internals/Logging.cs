// (c) Meta Platforms, Inc. and affiliates. Confidential and proprietary.

using System;
using UnityEngine;
using UnityEditor;

namespace Protokit.Browser {

  public static class Logging {

    private static string VERBOSE_MESSAGES_KEY = "ProtoKit_Browser_ShowVerboseMessages";
    public static bool Verbose {
      get => SessionState.GetBool(VERBOSE_MESSAGES_KEY, defaultValue: false);
      set => SessionState.SetBool(VERBOSE_MESSAGES_KEY, value);
    }

    public static void Log(string message) {
      if (Verbose) {
        Debug.Log(message);
      } else {
        //Console logging still goes into Editor.log, which is useful for debugging issues that already
        //happened, even if verbose logging is turned off
        Console.WriteLine(message);
      }
    }

    public static void Warn(string message) {
      Debug.LogWarning(message);
    }

    public static void Err(string message) {
      Debug.LogError(message);
    }

    public static void Err(Exception exception) {
      Debug.LogException(exception);
    }
  }
}
