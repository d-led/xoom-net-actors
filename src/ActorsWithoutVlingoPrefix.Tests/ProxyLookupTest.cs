// Copyright © 2012-2023 VLINGO LABS. All rights reserved.
//
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using Vlingo.Xoom.Actors;
using Xunit;

namespace ActorsWithoutVlingoPrefix.Tests
{
    /// <summary>
    /// Proves that the proxy of the protocol's own assembly is used, even when that assembly is not
    /// named with the <c>Vlingo</c> prefix that <c>DynaClassLoader</c> searches for.
    /// </summary>
    public class ProxyLookupTest
    {
        [Fact]
        public void ShouldUseTheProxyOfTheProtocolAssembly()
        {
            var world = World.StartWithDefaults("proxy-without-vlingo-prefix");

            var greeter = world.ActorFor<IGreeter>(typeof(GreeterActor));

            Assert.Equal(typeof(Greeter__Proxy), greeter.GetType());

            world.Terminate();
        }
    }

    public interface IGreeter
    {
        void Greet();
    }

    /// <summary>
    /// The proxy that an application would generate while it is compiled.
    /// </summary>
    public class Greeter__Proxy : IGreeter
    {
        public Greeter__Proxy(Actor actor, IMailbox mailbox)
        {
        }

        public void Greet()
        {
        }
    }

    public class GreeterActor : Actor, IGreeter
    {
        public void Greet()
        {
        }
    }
}
