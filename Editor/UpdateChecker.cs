using UnityEditor;
using Wagenheimer.PackageHub.Editor;

namespace Wagenheimer.IAPHelper.Editor
{
    public static class UpdateChecker
    {
        [MenuItem("Tools/Wagenheimer/IAP Helper/Check for Updates...", priority = 129)]
        public static void CheckForUpdateMenu() => CheckForUpdate(true);

        public static void CheckForUpdate(bool force = false)
        {
            PackageHubWindow.OpenToPackage("com.wagenheimer.iaphelper");
        }
    }
}
