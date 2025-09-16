using MessagePack;
using MessagePack.Formatters;
using System.Collections.Generic;
using System;
using GoLive.Saturn.Data.Entities;
using Saturn.Data.Stellar.Resolvers;

public class HashedStringResolver : IFormatterResolver
{
    public static readonly HashedStringResolver Instance = new();

    public IMessagePackFormatter<T> GetFormatter<T>()
    {
        if (typeof(T) == typeof(HashedString))
        {
            return (IMessagePackFormatter<T>)(object)new HashedStringFormatter();
        }
        return null;
    }
}
