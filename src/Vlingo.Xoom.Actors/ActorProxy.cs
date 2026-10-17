// Copyright © 2012-2023 VLINGO LABS. All rights reserved.
//
// This Source Code Form is subject to the terms of the
// Mozilla Public License, v. 2.0. If a copy of the MPL
// was not distributed with this file, You can obtain
// one at https://mozilla.org/MPL/2.0/.

using System;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using Vlingo.Xoom.Common.Compiler;
using static Vlingo.Xoom.Common.Compiler.DynaNaming;

namespace Vlingo.Xoom.Actors;

internal static class ActorProxy
{
    private static readonly object CreateForMutex = new object();

    public static T CreateFor<T>(Actor actor, IMailbox mailbox)
        => (T)CreateFor(typeof(T), actor, mailbox);

    public static object CreateFor(Type protocol, Actor actor, IMailbox mailbox)
    {
        var cacheKeyProtocol = GetProtocolGenericDefinition(protocol);

        var maybeCachedProxy = actor.LifeCycle.Environment.LookUpProxy(cacheKeyProtocol);

        if (maybeCachedProxy != null)
        {
            return maybeCachedProxy;
        }

        object newProxy;

        lock (CreateForMutex)
        {
            var proxyClassnameForLookup = FullyQualifiedClassNameFor(protocol, "__Proxy", true);
            var proxyClassnameForGeneration = FullyQualifiedClassNameFor(protocol, "__Proxy");

            try
            {
                newProxy = TryCreate(actor, mailbox, protocol, proxyClassnameForLookup);
            }
            catch (Exception e)
            {
                actor.Logger.Error($"Proxy creation failed because of '{e.Message}' but still trying", e);
                newProxy = TryGenerateCreate(protocol, actor, mailbox, proxyClassnameForGeneration, proxyClassnameForLookup);
            }

            actor.LifeCycle.Environment.CacheProxy(cacheKeyProtocol, newProxy);
        }

        return newProxy;
    }

    private static Type GetProtocolGenericDefinition(Type protocol)
        => protocol.IsGenericType ? protocol.GetGenericTypeDefinition() : protocol;
    
    private static bool IsGeneratingForMain(World world) => 
        !world.ResolveDynamic<bool>(InternalActorProxyForTestId);

    /// <summary>
    /// Answers the <c>Type</c> of the proxy: first from the assembly of the protocol it is created
    /// for, and then from the <c>DynaClassLoader</c>, which only searches assemblies named
    /// <c>Vlingo*</c>. Without the first step a proxy compiled into the application's own assembly
    /// is never found and one is compiled at run time instead.
    /// </summary>
    private static Type? LoadProxyClassFor(string targetClassname, Actor actor, Type protocol)
        => protocol.Assembly.GetType(targetClassname, throwOnError: false)
            ?? ClassLoaderFor(actor).LoadClass(targetClassname);

    private static object TryCreate(Actor actor, IMailbox mailbox, Type protocol, string targetClassname)
    {
        var proxyClass = LoadProxyClassFor(targetClassname, actor, protocol);
        if (proxyClass != null && proxyClass.IsGenericTypeDefinition)
        {
            var genericTypeParams = protocol.GetGenericArguments();
            proxyClass = proxyClass.MakeGenericType(genericTypeParams);
        }

        return TryCreateWithProxyClass(proxyClass!, actor, mailbox);
    }

    private static object TryCreateWithProxyClass(Type proxyClass, Actor actor, IMailbox mailbox)
    {
        var instance = Activator.CreateInstance(proxyClass, actor, mailbox);
        if (instance == null)
        {
            throw new ArgumentException($"Cannot create an instance for proxy class '{proxyClass.FullName}'");
        }
            
        return instance;
    }

    private static object TryGenerateCreate(Type protocol, Actor actor, IMailbox mailbox, string targetClassName, string lookupTypeName)
    {
        try
        {
            var generator = IsGeneratingForMain(actor.Stage.World) ?
                ProxyGenerator.ForTest(true, actor.Logger) :
                ProxyGenerator.ForMain(true, actor.Logger);
            return TryGenerateCreate(protocol, actor, mailbox, generator, targetClassName, lookupTypeName);
        }
        catch (Exception e)
        {
            actor.Logger.Error($"Trying generate proxy but it failed because of '{e.Message}' but still trying", e);
            try
            {
                var generator = ProxyGenerator.ForTest(true, actor.Logger);
                return TryGenerateCreate(protocol, actor, mailbox, generator, targetClassName, lookupTypeName);
            }
            catch (Exception etest)
            {
                throw new ArgumentException($"Actor proxy {protocol.Name} not created for main or test: {etest.Message}", etest);
            }
        }
    }

    private static object TryGenerateCreate(Type protocol, Actor actor, IMailbox mailbox, ProxyGenerator generator, string targetClassName, string lookupTypeName)
    {
        try
        {
            var originalProtocol = protocol;

            if (protocol.IsGenericType && !protocol.IsGenericTypeDefinition)
            {
                protocol = protocol.GetGenericTypeDefinition();
            }

            // Allows to determine if async/await keyword was used
            var asyncAwaitedMethods = actor.GetType().GetTypeInfo().ImplementedInterfaces
                .Select(ii => actor.GetType().GetInterfaceMap(ii))
                .Where(im => im.InterfaceType == originalProtocol)
                .SelectMany(im => im.TargetMethods)
                .Where(IsAsyncStateMachine)
                .ToArray();
            var result = generator.GenerateFor(protocol, asyncAwaitedMethods);
            var input = new Input(
                protocol,
                targetClassName,
                lookupTypeName,
                result.Source,
                result.SourceFile,
                ClassLoaderFor(actor),
                generator.Type,
                true);

            var proxyCompiler = new DynaCompiler();
            var proxyClass = proxyCompiler.Compile(input);
            if (proxyClass != null && proxyClass.IsGenericTypeDefinition)
            {
                proxyClass = proxyClass.MakeGenericType(originalProtocol.GetGenericArguments());
            }
                
            return TryCreateWithProxyClass(proxyClass!, actor, mailbox);
        }
        catch (Exception e)
        {
            throw new ArgumentException($"Actor proxy {protocol.Name} not created because: {e.Message}", e);
        }
    }

    private static DynaClassLoader ClassLoaderFor(Actor actor)
    {
        // The stage and the world are read through the environment on purpose: a secured actor
        // cannot provide its stage, and creating a proxy must not depend on that.
        var world = actor.LifeCycle.Environment.Stage.World;
        var classLoader = world.ClassLoader;
        if (classLoader == null)
        {
            classLoader = new DynaClassLoader();
            world.ClassLoader = classLoader;
        }

        return classLoader;
    }
        
    private static bool IsAsyncStateMachine(MethodInfo? methodInfo)
    {
        var attType = typeof(AsyncStateMachineAttribute);

        // Obtain the custom attribute for the method. 
        // The value returned contains the StateMachineType property. 
        // Null is returned if the attribute isn't present for the method. 
        if (methodInfo != null)
        {
            return methodInfo.GetCustomAttribute(attType) is AsyncStateMachineAttribute;
        }

        return false;
    }
    
    internal static readonly string InternalActorProxyForTestId = Guid.NewGuid().ToString();

    private static bool _initializingForTest;
    
    internal static void InitializeForTest() => _initializingForTest = true;
    
    internal static bool IsInitializingForTest() => _initializingForTest;
    
    internal static void SetProxyTypeTo(World world)
    {
        world.RegisterDynamic(InternalActorProxyForTestId, _initializingForTest);
        _initializingForTest = false;
    }
}