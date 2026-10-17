// Copyright © 2012-2023 VLINGO LABS. All rights reserved.
//
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using System;
using System.Threading.Tasks;
using Vlingo.Xoom.Common;
using Xunit;

namespace Vlingo.Xoom.Actors.Tests
{
    public class TaskActorProtocolTest : ActorsTest
    {
        [Fact]
        public async Task TestThatMailboxDoesntDeliverWhileAwaiting()
        {
            var call = new BlockedCall();
            var usesTask = World.ActorFor<IUsesTask>(() => new UsesTaskActor(call));

            var one = usesTask.GetOne();
            call.AwaitEntered();
            usesTask.Change(10);
            call.Release();

            Assert.Equal(1, await one);
        }

        [Fact]
        public async Task TestThatExceptionIsCorrectlyHandled()
        {
            var usesTask = World.ActorFor<IUsesTask>(() => new UsesFailingTaskActor());

            var thrown = await Assert.ThrowsAsync<Exception>(() => usesTask.GetOne());

            Assert.Equal("Cannot perform the operation", thrown.Message);
        }

        [Fact]
        public async Task TestThatSchedulesLongRunningOperations()
        {
            var call = new BlockedCall();
            var usesTask = World.ActorFor<IUsesTask>(() => new UsesLongRunningTaskActor(call));

            var one = usesTask.GetOne();
            call.AwaitEntered();
            call.Release();

            Assert.Equal(1, await one);
        }

        [Fact]
        public async Task TestThatSchedulesLongRunningOperationsAndSuspendMailbox()
        {
            var call = new BlockedCall();
            var usesTask = World.ActorFor<IUsesTask>(() => new UsesLongRunningTaskActor(call));

            var one = usesTask.GetOne();
            call.AwaitEntered();
            usesTask.Change(10);
            call.Release();

            Assert.Equal(1, await one);
        }

        [Fact]
        public async Task TestThatTaskLikeCompletesCanBeAwaited()
        {
            var call = new BlockedCall();
            var usesTask = World.ActorFor<IUsesTask>(() => new UsesTaskActor(call));

            var two = usesTask.GetTwoAsync();
            call.AwaitEntered();
            call.Release();

            Assert.Equal(2, await two);
        }

        [Fact]
        public async Task TestThatMailboxDoesntDeliverForCompletesWhileAwaiting()
        {
            var call = new BlockedCall();
            var usesTask = World.ActorFor<IUsesTask>(() => new UsesTaskActor(call));

            var first = usesTask.GetTwoAsync();
            call.AwaitEntered();
            usesTask.Change(10);
            call.Release();

            Assert.Equal(2, await first);
            Assert.Equal(11, await usesTask.GetTwoAsync());
        }

        [Fact]
        public async Task TestThatSchedulesLongRunningOperationsForCompletesAndSuspendMailbox()
        {
            var call = new BlockedCall();
            var usesTask = World.ActorFor<IUsesTask>(() => new UsesLongRunningTaskActor(call));

            var two = usesTask.GetTwoAsync();
            call.AwaitEntered();
            usesTask.Change(10);
            call.Release();

            Assert.Equal(2, await two);
        }

    }

    public class UsesTaskActor : Actor, IUsesTask
    {
        private readonly BlockedCall _call;
        private int _one = 1;

        public UsesTaskActor(BlockedCall call) => _call = call;

        public async Task<int> GetOne()
        {
            await Task.Run(() => _call.Enter());
            return _one;
        }

        public async ICompletes<int> GetTwoAsync()
        {
            await Task.Run(() => _call.Enter());
            return await Completes().With(_one + 1);
        }

        public void Change(int newOne) => _one = newOne;
    }

    public class UsesFailingTaskActor : Actor, IUsesTask
    {
        private const string Failure = "Cannot perform the operation";

        public Task<int> GetOne() => Task.FromException<int>(new Exception(Failure));

        public async ICompletes<int> GetTwoAsync()
        {
            await Task.Run(() => { });
            throw new Exception(Failure);
        }

        public void Change(int newOne)
        {
        }
    }

    public class UsesLongRunningTaskActor : Actor, IUsesTask
    {
        private readonly BlockedCall _call;
        private int _one = 1;

        public UsesLongRunningTaskActor(BlockedCall call) => _call = call;

        public Task<int> GetOne() =>
            Task.Factory.StartNew(() =>
            {
                _call.Enter();
                return _one;
            }, TaskCreationOptions.LongRunning);

        public async ICompletes<int> GetTwoAsync()
        {
            var value = await Task.Factory.StartNew(() =>
            {
                _call.Enter();
                return _one;
            }, TaskCreationOptions.LongRunning);

            return await Completes().With(value + 1);
        }

        public void Change(int newOne) => _one = newOne;
    }

    public interface IUsesTask
    {
        void Change(int newOne);

        Task<int> GetOne();

        ICompletes<int> GetTwoAsync();
    }
}
