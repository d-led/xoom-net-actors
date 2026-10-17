// Copyright © 2012-2023 VLINGO LABS. All rights reserved.
//
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using System.Threading;
using Vlingo.Xoom.Actors.Plugin.Mailbox.TestKit;
using Vlingo.Xoom.Common;
using Xunit;

namespace Vlingo.Xoom.Actors.Tests;

public class ActorProxyTest
{
    private readonly AtomicReference<string> _underTest = new("UNKNOWN");
    private readonly CountdownEvent _latch = new(1);
    private World _world;

    private readonly Thread _runtimeStartWorldThread;

    public ActorProxyTest()
    {
        // The world is started on a separate thread on purpose: the runtime discovers that it is
        // under test from the assemblies loaded in the process, and this test wants it not to.
        _runtimeStartWorldThread = new Thread(() =>
        {
            TestRuntimeDiscoverer.IsTestDetectionDeactivated = true;
            _world = World.StartWithDefaults("StartForMain");
            _underTest.Set(_world.ResolveDynamic<bool>(ActorProxy.InternalActorProxyForTestId) ? "TRUE" : "FALSE");
            _latch.Signal();
        });
    }

    [Fact]
    public void TestThatActorProxyInitializesForMain()
    {
        try
        {
            _runtimeStartWorldThread.Start();

            _latch.Wait();

            Assert.Equal("FALSE", _underTest.Get());
        }
        finally
        {
            _world?.Terminate();
            TestRuntimeDiscoverer.IsTestDetectionDeactivated = false;
        }
    }

    [Fact]
    public void TestThatActorProxyInitializesForTest()
    {
        try
        {
            TestRuntimeDiscoverer.IsTestDetectionDeactivated = false;
            ActorProxy.InitializeForTest();
            var world = World.StartWithDefaults("StartForTest");

            Assert.True(world.ResolveDynamic<bool>(ActorProxy.InternalActorProxyForTestId));
        }
        finally
        {
            TestRuntimeDiscoverer.IsTestDetectionDeactivated = false;
        }
    }
}
