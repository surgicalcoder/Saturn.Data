using HarmonyLib;

namespace Saturn.Data.LiteDb.RuntimePatcher;

public static class Patcher
{
    public static void PatchLiteDB()
    {
        var harmony = new Harmony("Saturn.Data.LiteDb.RuntimePatcher.Patcher.PatchLiteDB");
        harmony.PatchAll();
    }
}