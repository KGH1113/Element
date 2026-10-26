using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace ImplResourcePack.Editor
{
  public static class FontBundleBuilder
  {
    internal const string BundleName = "implresourcepackfont";

    [MenuItem("Impl Resource Pack/Build Font Bundles")]
    public static void BuildAll()
    {
      FontAssetGenerator.GenerateAll();

      string projectRoot =
        Directory.GetParent(Application.dataPath)?.FullName
        ?? throw new InvalidOperationException("Unity project root could not be resolved.");
      string repositoryRoot =
        Directory.GetParent(projectRoot)?.FullName
        ?? throw new InvalidOperationException("Repository root could not be resolved.");
      string runtimeRoot = Path.Combine(repositoryRoot, "ImplResourcePack", "Assets", "Font");
      string scratchRoot = Path.Combine(projectRoot, "Library", "ImplResourcePackFontBundles");

      AssetBundleBuild definition = new()
      {
        assetBundleName = BundleName,
        assetNames = new[] { FontAssetGenerator.PrimaryAssetPath, FontAssetGenerator.KoreanAssetPath },
      };

      BuildOne(definition, scratchRoot, runtimeRoot, "Windows", BuildTarget.StandaloneWindows64);
      BuildOne(definition, scratchRoot, runtimeRoot, "Linux", BuildTarget.StandaloneLinux64);
      BuildOne(definition, scratchRoot, runtimeRoot, "Mac", BuildTarget.StandaloneOSX);

      AssetDatabase.Refresh();
      Debug.Log("ImplResourcePack font bundles were built at " + runtimeRoot);
    }

    private static void BuildOne(
      AssetBundleBuild definition,
      string scratchRoot,
      string runtimeRoot,
      string platformDirectory,
      BuildTarget target
    )
    {
      string scratchDirectory = Path.Combine(scratchRoot, platformDirectory);
      Directory.CreateDirectory(scratchDirectory);

      AssetBundleManifest manifest = BuildPipeline.BuildAssetBundles(
        scratchDirectory,
        new[] { definition },
        BuildAssetBundleOptions.ChunkBasedCompression,
        target
      );
      if (manifest == null)
        throw new InvalidOperationException("AssetBundle build failed for " + target);

      string source = Path.Combine(scratchDirectory, BundleName);
      if (!File.Exists(source))
        throw new FileNotFoundException("Built AssetBundle was not found.", source);

      string destinationDirectory = Path.Combine(runtimeRoot, platformDirectory);
      Directory.CreateDirectory(destinationDirectory);
      File.Copy(source, Path.Combine(destinationDirectory, BundleName), true);
    }
  }
}
