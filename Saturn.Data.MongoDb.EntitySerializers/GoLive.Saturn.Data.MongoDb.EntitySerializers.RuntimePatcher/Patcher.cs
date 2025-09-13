using System.Reflection;
using HarmonyLib;

namespace GoLive.Saturn.Data.EntitySerializers.RuntimePatcher;

public static class Patcher
{
    public static void PatchMongoDB()
    {
        var harmony = new Harmony("GoLive.Saturn.Data.MongoDb.EntitySerializers.RuntimePatcher");

        // Get the type and method to patch
        var targetType = Type.GetType("MongoDB.Driver.Linq.Linq3Implementation.Ast.Expressions.AstGetFieldExpression, MongoDB.Driver");
        if (targetType == null)
        {
            throw new Exception("Type 'MongoDB.Driver.Linq.Linq3Implementation.Ast.Expressions.AstGetFieldExpression' not found.");
        }

        var targetMethod = targetType.GetMethod("ConvertToFieldPath", BindingFlags.Public | BindingFlags.Instance);
        if (targetMethod == null)
        {
            throw new Exception("Method 'ConvertToFieldPath' not found.");
        }

        // Create the patch method
        var patchMethod = typeof(Patcher).GetMethod(nameof(ConvertToFieldPathPrefix), BindingFlags.NonPublic | BindingFlags.Static);

        // Apply the patch
        harmony.Patch(targetMethod, new HarmonyMethod(patchMethod));
    }
    
    
    // Patch method (prefix)
    private static bool ConvertToFieldPathPrefix(object __instance, ref string __result)
    {
        var inputField = __instance.GetType().GetField("_input", BindingFlags.NonPublic | BindingFlags.Instance);
        if (inputField == null)
        {
            throw new Exception("Field '_input' not found.");
        }

        var _input = inputField.GetValue(__instance);

        if (HasSafeFieldName(__instance, out var fieldName))
        {
            var astVarExpressionType = Type.GetType("MongoDB.Driver.Linq.Linq3Implementation.Ast.Expressions.AstVarExpression, MongoDB.Driver");

            if (_input != null && astVarExpressionType != null && astVarExpressionType.IsInstanceOfType(_input))
            {
                var isCurrentProperty = astVarExpressionType.GetProperty("IsCurrent", BindingFlags.Public | BindingFlags.Instance);
                var varNameProperty = astVarExpressionType.GetProperty("Name", BindingFlags.Public | BindingFlags.Instance);

                if (isCurrentProperty == null || varNameProperty == null)
                {
                    throw new Exception("Properties 'IsCurrent' or 'Name' not found on AstVarExpression.");
                }

                var isCurrent = (bool)isCurrentProperty.GetValue(_input);
                var varName = (string)varNameProperty.GetValue(_input);

                __result = isCurrent ? $"${fieldName}" : $"$${varName}.{fieldName}";
                return false; // Skip the original method
            }

            var inputPath = ConvertToFieldPath(_input);

            if (fieldName == "_____")
            {
                __result = inputPath;
                return false; // Skip the original method
            }

            __result = $"{inputPath}.{fieldName}";
            return false; // Skip the original method
        }

        return true; // Continue with the original method
    }

    private static bool HasSafeFieldName(object _this, out string fieldName)
    {
        var method = _this.GetType().GetMethod("HasSafeFieldName", BindingFlags.Public | BindingFlags.Instance);
        if (method == null)
        {
            throw new Exception("Method 'HasSafeFieldName' not found.");
        }
        var parameters = new object[] { null };
        var result = (bool)method.Invoke(_this, parameters);
        fieldName = (string)parameters[0];
        return result;
    }

    private static string ConvertToFieldPath(object instance)
    {
        if (instance == null) return null;
        var method = instance.GetType().GetMethod("ConvertToFieldPath", BindingFlags.Public | BindingFlags.Instance);
        if (method == null)
        {
            throw new Exception($"Method 'ConvertToFieldPath' not found on type '{instance.GetType()}'.");
        }
        return (string)method.Invoke(instance, null);
    }
}