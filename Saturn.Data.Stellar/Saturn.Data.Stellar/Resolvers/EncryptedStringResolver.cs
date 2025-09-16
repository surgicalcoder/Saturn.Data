using MessagePack;
using MessagePack.Formatters;
using System.Collections.Generic;
using System;
using GoLive.Saturn.Data.Entities;

public class EncryptedStringResolver : IFormatterResolver
{
    public static readonly EncryptedStringResolver Instance = new();

    public IMessagePackFormatter<T> GetFormatter<T>()
    {
        if (typeof(T) == typeof(EncryptedString))
        {
            return (IMessagePackFormatter<T>)(object)new EncryptedStringFormatter();
        }
        return null;
    }
}