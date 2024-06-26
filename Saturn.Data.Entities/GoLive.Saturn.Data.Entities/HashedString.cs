﻿using System;

namespace GoLive.Saturn.Data.Entities;

public sealed class HashedString : IUpdatableFrom<HashedString>
{
    public string Decoded { get; set; }
    public string Salt { get; set; }
    public string Hash { get; set; }
    public bool Populated { get; set; }

    public static implicit operator HashedString(string Decoded)
    {
        return new HashedString { Decoded = Decoded };
    }

    public void UpdateFrom(HashedString input)
    {
        if (input == null || input.Populated == false)
        {
            Decoded = null;
            Hash = null;
            Salt = null;

            return;
        }

        if (string.IsNullOrEmpty(input.Decoded))
        {
            return;
        }

        Decoded = input.Decoded;
        Hash = null;
        Salt = null;
    }

    public bool CompareHash(string Input)
    {
        if (string.IsNullOrWhiteSpace(Salt) || string.IsNullOrWhiteSpace(Hash))
        {
            throw new InvalidOperationException("Hash or Salt not populated");
        }

        return Hash == Crypto.Hash.CalculateSHA512($"{Input}{Salt}");
    }
}