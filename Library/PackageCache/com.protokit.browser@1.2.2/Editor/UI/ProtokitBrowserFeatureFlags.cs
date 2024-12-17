// (c) Meta Platforms, Inc. and affiliates. Confidential and proprietary.

namespace Protokit.Browser {

  /// <summary>
  /// A collection of flags that are used to turn on/off different features
  /// of the browser.  Useful for developing new features and not releasing
  /// them yet to the public.
  /// </summary>
  public static class ProtokitBrowserFeatureFlags {

    /// <summary>
    /// Should the browser surface the concept of "supported" packages or not in
    /// the filter options.
    /// </summary>
    public static readonly bool SupportedPackagesFilter = false;

    /// <summary>
    /// Should the header of the browser show the "more" button that includes 
    /// a few global actions.
    /// </summary>
    public static readonly bool ShowMoreButtonInHeader = true;

    /// <summary>
    /// Should the browser allow you to queue up package actions to apply all at once.
    /// </summary>
    public static readonly bool AllowPackageActionQueueing = true;

    /// <summary>
    /// Should the browser show the "Category" filter in the side bar.
    /// </summary>
    public static readonly bool ShowCategoryFilter = false;

  }
}
