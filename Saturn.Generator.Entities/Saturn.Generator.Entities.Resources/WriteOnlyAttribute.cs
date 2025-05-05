using System;

namespace GoLive.Saturn.Generator.Entities.Resources;

[AttributeUsage(AttributeTargets.Field, AllowMultiple = false, Inherited = false)]
public class WriteOnlyAttribute : Attribute { }