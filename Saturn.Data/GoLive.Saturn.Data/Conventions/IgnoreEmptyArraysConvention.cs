﻿using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Conventions;

namespace GoLive.Saturn.Data.Conventions;

public class IgnoreEmptyArraysConvention : ConventionBase, IMemberMapConvention
{
    //List<> implements the majority of the common generic interfaces IEnumerable<T>, ICollection<T>, etc. so it should be the default concrete implementation to use
    private static readonly Type DefaultType = typeof(List<>);

    //Set up mapping dictionary to go from interface type to concrete type for the interfaces that List<> doesn't implement
    private static readonly IReadOnlyDictionary<Type, Type> InterfaceToConcreteMap = new Dictionary<Type, Type>
    {
        { typeof(ISet<>), typeof(HashSet<>) },
        { typeof(IProducerConsumerCollection<>), typeof(ConcurrentBag<>) }
    };

    public void Apply(BsonMemberMap memberMap)
    {
        var typ = memberMap.MemberType;

        if (!typeof(IEnumerable).IsAssignableFrom(memberMap.MemberType) || //Allow IEnumerable
            typeof(string) == memberMap.MemberType //But not String
            // || typeof(IDictionary).IsAssignableFrom(memberMap.MemberType)
           ) //Or Dictionary (concrete classes only see below)
        {
            return;
        }

        //*NOTE Microsoft was too stupid to make the generic dictionary interfaces implement IDictonary even though every single concrete class does
        //      They were also too stupid to make generic IDictionary implement IReadOnlyDictionary even though every single concrete class does I believe this should catch all
        if (memberMap.MemberType.IsGenericType && memberMap.MemberType.IsInterface)
        {
            var genericType = memberMap.MemberType.GetGenericTypeDefinition();

            if (genericType == typeof(IDictionary<,>) || genericType == typeof(IReadOnlyDictionary<,>))
            {
                return;
            }
        }

        if (memberMap.MemberType.IsArray) //Load Empty Array
        {
            memberMap.SetDefaultValue(() => Array.CreateInstance(memberMap.MemberType.GetElementType(), 0));
        }
        else if (!memberMap.MemberType.IsInterface) //Create ConcreteType directly
        {
            memberMap.SetDefaultValue(() => Activator.CreateInstance(memberMap.MemberType));
        }
        else if (memberMap.MemberType.IsGenericType) //Generic Interface type
        {
            var interfaceType = memberMap.MemberType.GetGenericTypeDefinition();
            var concreteType = InterfaceToConcreteMap.ContainsKey(interfaceType)
                ? InterfaceToConcreteMap[interfaceType]
                : DefaultType;
            memberMap.SetDefaultValue(() => Activator.CreateInstance(concreteType.MakeGenericType(memberMap.MemberType.GetGenericArguments())));
        }
        else //This should just be the antique non generic interfaces like ICollection, IEnumerable, etc.
        {
            memberMap.SetDefaultValue(() => Activator.CreateInstance(typeof(List<object>)));
        }

        memberMap.SetShouldSerializeMethod(instance =>
        {
            var value = (IEnumerable)memberMap.Getter(instance);

            return value?.GetEnumerator().MoveNext() ?? false;
        });
    }
}