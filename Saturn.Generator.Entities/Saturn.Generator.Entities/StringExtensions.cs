using System;
using Microsoft.CodeAnalysis;

namespace GoLive.Saturn.Generator.Entities;

public static class StringExtensions
{
    public static string FirstCharToUpper(this string input)
    {
        switch (input)
        {
            case null: throw new ArgumentNullException(nameof(input));
            case "": throw new ArgumentException($"{nameof(input)} cannot be empty", nameof(input));
            default: return input[0].ToString().ToUpper() + input.Substring(1);
        }
    }
        
    public static string MakeFullyQualified(this string Path, AdditionalText configFilePath)
    {
        var fullPath = System.IO.Path.Combine(configFilePath.Path, Path);
        return System.IO.Path.GetFullPath(fullPath);
    }
    public static string FirstCharToLower(this string input)
    {
        if (string.IsNullOrEmpty(input) || char.IsLower(input[0]))
        {
            return input;
        }

        return $"{char.ToLower(input[0])}{input.Substring(1)}";
    }
    
}