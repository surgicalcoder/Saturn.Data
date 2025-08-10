using System.Reflection;
using HarmonyLib;
using LiteDB;
using LiteDB.Engine;

namespace Saturn.Data.LiteDb.RuntimePatcher;

[HarmonyPatch]
public static class ConnectionStringPatch
{
    // Target the internal method via reflection
    static MethodBase TargetMethod()
    {
        var type = typeof(ConnectionString);
        return type.GetMethod("CreateEngine", BindingFlags.Instance | BindingFlags.NonPublic);
    }

    // The postfix runs after the original method.
    static void Postfix(ConnectionString __instance, ref ILiteEngine __result, Action<EngineSettings> engineSettingsAction = null)
    {
        // Check if the connection type is Shared and replace the result
        if (__instance.Connection == ConnectionType.Shared)
        {
            var settings = new EngineSettings()
            {
                Filename = __instance.Filename,
                Password = __instance.Password,
                InitialSize = __instance.InitialSize,
                ReadOnly = __instance.ReadOnly,
                Collation = __instance.Collation,
                Upgrade = __instance.Upgrade,
                AutoRebuild = __instance.AutoRebuild
            };
            engineSettingsAction?.Invoke(settings);

            // Replace with your custom engine
            __result = new EnhancedSharedEngine(settings);
        }
    }
}