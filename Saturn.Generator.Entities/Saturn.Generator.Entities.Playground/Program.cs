// See https://aka.ms/new-console-template for more information

using Saturn.Generator.Entities.Playground;

FourthItem fr = new();
Console.WriteLine(fr.Changes is null ? "no changes" : "changes available");
