using System;

namespace GoLive.Saturn.Generator.Entities.Resources;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public class NoGenerateDtoAttribute : Attribute
{
}
