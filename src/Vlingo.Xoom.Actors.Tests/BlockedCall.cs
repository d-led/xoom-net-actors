// Copyright © 2012-2023 VLINGO LABS. All rights reserved.
//
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using System.Threading;

namespace Vlingo.Xoom.Actors.Tests;

/// <summary>
/// Holds an actor inside a call until the test lets it go, so that a test never has to guess how
/// long a call takes. The actor calls <see cref="Enter"/> and blocks; the test awaits
/// <see cref="AwaitEntered"/> and then calls <see cref="Release"/>.
/// </summary>
public sealed class BlockedCall
{
    private readonly SemaphoreSlim _entered = new(0);
    private readonly ManualResetEventSlim _released = new(false);

    /// <summary>Called by the actor: announce that the call has started, then wait for the test.</summary>
    public void Enter()
    {
        _entered.Release();
        _released.Wait();
    }

    /// <summary>Called by the test: answer only once the actor is inside the call.</summary>
    public void AwaitEntered() => _entered.Wait();

    /// <summary>Called by the test: let the actor finish. Later calls pass through unblocked.</summary>
    public void Release() => _released.Set();
}
