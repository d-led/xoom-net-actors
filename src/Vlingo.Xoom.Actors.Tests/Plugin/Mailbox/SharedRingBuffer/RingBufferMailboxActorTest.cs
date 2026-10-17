// Copyright © 2012-2023 VLINGO LABS. All rights reserved.
//
// This Source Code Form is subject to the terms of the
// Mozilla Public License, v. 2.0. If a copy of the MPL
// was not distributed with this file, You can obtain
// one at https://mozilla.org/MPL/2.0/.

using System;
using Vlingo.Xoom.Actors.Plugin;
using Vlingo.Xoom.Actors.Plugin.Completes;
using Vlingo.Xoom.Actors.Plugin.Mailbox.SharedRingBuffer;
using Vlingo.Xoom.Actors.TestKit;
using Vlingo.Xoom.Common;
using Xunit;

namespace Vlingo.Xoom.Actors.Tests.Plugin.Mailbox.SharedRingBuffer
{
    public class RingBufferMailboxActorTest : ActorsTest
    {
        private const int MailboxSize = 64;
        private const int MaxCount = 1024;

        [Fact]
        public void TestBasicDispatch()
        {
            Init(MailboxSize);
            var testResults = new TestResults(MaxCount);
            var countTaker = World.ActorFor<ICountTaker>(
                Definition.Has<CountTakerActor>(
                    Definition.Parameters(testResults), "testRingMailbox", "countTaker-1"));

            for (var count = 1; count <= MaxCount; ++count)
            {
                countTaker.Take(count);
            }

            Assert.Equal(MaxCount, testResults.GetHighest());
        }

        private void Init(int mailboxSize)
        {
            var properties = new Properties();
            properties.SetProperty("plugin.name.testRingMailbox", "true");
            properties.SetProperty("plugin.testRingMailbox.classname", "Vlingo.Xoom.Actors.Plugin.Mailbox.SharedRingBuffer.SharedRingBufferMailboxPlugin");
            properties.SetProperty("plugin.testRingMailbox.defaultMailbox", "false");
            properties.SetProperty("plugin.testRingMailbox.size", $"{mailboxSize}");
            properties.SetProperty("plugin.testRingMailbox.fixedBackoff", "2");
            properties.SetProperty("plugin.testRingMailbox.numberOfDispatchersFactor", "1.0");
            properties.SetProperty("plugin.testRingMailbox.dispatcherThrottlingCount", "20");

            var provider = new SharedRingBufferMailboxPlugin();
            var pluginProperties = new PluginProperties("testRingMailbox", properties);
            var plugin = new PooledCompletesPlugin();
            plugin.Configuration.BuildWith(World.Configuration, pluginProperties);

            provider.Start(World);
        }

        private class CountTakerActor : Actor, ICountTaker
        {
            private ICountTaker _self;
            private readonly TestResults _testResults;

            public CountTakerActor(TestResults testResults)
            {
                _testResults = testResults;
                _self = SelfAs<ICountTaker>();
            }

            public void Take(int count)
            {
                if (_testResults.IsHighest(count))
                {
                    _testResults.SetHighest(count);
                }
            }
        }

        private class TestResults
        {
            private readonly AccessSafely _accessSafely;

            public TestResults(int happenings)
            {
                var highest = new AtomicInteger(0);
                _accessSafely = AccessSafely
                    .AfterCompleting(happenings)
                    .WritingWith<int>("highest", highest.Set)
                    .ReadingWith("highest", highest.Get)
                    .ReadingWith<int, bool>("isHighest", count => count > highest.Get());
            }

            public void SetHighest(int value) => _accessSafely.WriteUsing("highest", value);

            public int GetHighest() => _accessSafely.ReadFrom<int>("highest");

            public bool IsHighest(int value) => _accessSafely.ReadFromNow<int, bool>("isHighest", value);
        }
    }
}
