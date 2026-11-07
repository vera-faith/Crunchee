#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using UnityEditor.Callbacks;

namespace Crunchee.EditorTools
{
    /// <summary>
    /// Post-build fixups for the exported Xcode project. Unity does not write
    /// ITSAppUsesNonExemptEncryption; without it the build lands in a Missing
    /// Compliance hold on App Store Connect, so we write it here (false -
    /// Crunchee uses only exempt/platform crypto) instead of editing
    /// Builds/iOS/Info.plist by hand each time.
    /// </summary>
    public static class IosPostBuild
    {
        [PostProcessBuild]
        public static void OnPostProcessBuild(BuildTarget target, string path)
        {
            if (target != BuildTarget.iOS) return;
            var plistPath = System.IO.Path.Combine(path, "Info.plist");
            var plist = new UnityEditor.iOS.Xcode.PlistDocument();
            plist.ReadFromFile(plistPath);
            plist.root.SetBoolean("ITSAppUsesNonExemptEncryption", false);
            plist.WriteToFile(plistPath);
            Debug.Log("[Crunchee] post-build: set ITSAppUsesNonExemptEncryption=false in " + plistPath);
        }
    }
}
#endif
