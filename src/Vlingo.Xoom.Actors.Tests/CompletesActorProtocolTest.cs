// Copyright © 2012-2023 VLINGO LABS. All rights reserved.
//
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using System;
using Vlingo.Xoom.Actors.TestKit;
using Vlingo.Xoom.Common;
using Xunit;

namespace Vlingo.Xoom.Actors.Tests
{
    public class CompletesActorProtocolTest : ActorsTest
    {
        private const string Hello = "Hello, Completes!";
        private const string Prefix = "*** ";

        [Fact]
        public void TestReturnsCompletesForSideEffects()
        {
            var testResults = TestResults.AfterCompleting(2);
            var uc = World.ActorFor<IUsesCompletes>(typeof(UsesCompletesActor), testResults);

            uc.GetHello().AndThenConsume(hello => testResults.SetGreeting(hello.Greeting));
            uc.GetOne().AndThenConsume(value => testResults.SetValue(value));

            Assert.Equal(Hello, testResults.GetGreeting());
            Assert.Equal(1, testResults.GetValue());
        }

        [Fact]
        public void TestAfterAndThenCompletesForSideEffects()
        {
            var greetingsTestResults = TestResults.AfterCompleting(1);

            var uc = World.ActorFor<IUsesCompletes>(typeof(UsesCompletesActor), greetingsTestResults);
            var helloCompletes = uc.GetHello();
            helloCompletes
                .AndThen(hello => new Hello(Prefix + hello.Greeting))
                .AndThenConsume(hello => greetingsTestResults.SetGreeting(hello.Greeting));

            Assert.NotEqual(Hello, greetingsTestResults.GetGreeting());
            Assert.NotEqual(Hello, helloCompletes.Outcome.Greeting);
            Assert.Equal(Prefix + Hello, greetingsTestResults.GetGreeting());
            Assert.Equal(Prefix + Hello, helloCompletes.Outcome.Greeting);

            var valueTestResults = TestResults.AfterCompleting(1);
            var one = uc.GetOne();
            one
                .AndThen(value => value + 1)
                .AndThenConsume(value => valueTestResults.SetValue(value));

            Assert.NotEqual(1, valueTestResults.GetValue());
            Assert.NotEqual(1, one.Outcome);
            Assert.Equal(2, valueTestResults.GetValue());
            Assert.Equal(2, one.Outcome);
        }

        [Fact]
        public void TestThatVoidReturnTypeThrowsException()
        {
            var testResults = TestResults.AfterCompleting(1);
            var uc = World.ActorFor<IUsesCompletes>(typeof(UsesCompletesActor), testResults);
            uc.CompletesNotSupportedForVoidReturnType();
            Assert.True(testResults.GetExceptionThrown());
        }

        private class UsesCompletesActor : Actor, IUsesCompletes
        {
            private readonly TestResults _results;

            public UsesCompletesActor(TestResults results) => _results = results;

            public void CompletesNotSupportedForVoidReturnType()
            {
                try
                {
                    Completes().With("Must throw exception");
                    _results.Received.WriteUsing("exceptionThrown", false);
                }
                catch (Exception)
                {
                    _results.Received.WriteUsing("exceptionThrown", true);
                }
            }

            public ICompletes<Hello> GetHello() => Completes().With(new Hello(Hello));

            public ICompletes<int> GetOne() => Completes().With(1);
        }

        private class TestResults
        {
            private readonly AtomicReference<string> _receivedGreeting = new AtomicReference<string>(null);
            private readonly AtomicInteger _receivedValue = new AtomicInteger(0);
            internal readonly AccessSafely Received;
            private readonly AtomicBoolean _exceptionThrown = new AtomicBoolean(false);

            private TestResults(AccessSafely received) => Received = received;

            public static TestResults AfterCompleting(int times)
            {
                var testResults = new TestResults(AccessSafely.AfterCompleting(times));
                testResults.Received.WritingWith<string>("receivedGreeting", s => testResults._receivedGreeting.Set(s));
                testResults.Received.ReadingWith("receivedGreeting", testResults._receivedGreeting.Get);
                testResults.Received.WritingWith<int>("receivedValue", v => testResults._receivedValue.Set(v));
                testResults.Received.ReadingWith("receivedValue", testResults._receivedValue.Get);
                testResults.Received.WritingWith<bool>("exceptionThrown", e => testResults._exceptionThrown.Set(e));
                testResults.Received.ReadingWith("exceptionThrown", testResults._exceptionThrown.Get);
                return testResults;
            }

            public void SetGreeting(string greeting) => Received.WriteUsing("receivedGreeting", greeting);

            public void SetValue(int value) => Received.WriteUsing("receivedValue", value);

            public string GetGreeting() => Received.ReadFrom<string>("receivedGreeting");

            public int GetValue() => Received.ReadFrom<int>("receivedValue");

            public bool GetExceptionThrown() => Received.ReadFrom<bool>("exceptionThrown");
        }
    }

    public class Hello
    {
        public readonly string Greeting;

        public Hello(string greeting) => Greeting = greeting;

        public override string ToString() => $"Hello[{Greeting}]";
    }

    public interface IUsesCompletes
    {
        ICompletes<Hello> GetHello();
        ICompletes<int> GetOne();
        void CompletesNotSupportedForVoidReturnType();
    }
}
