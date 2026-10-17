// Copyright © 2012-2023 VLINGO LABS. All rights reserved.
//
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using Xunit;

// The actors runtime keeps process-wide state: whether the runtime is under test, whether a proxy is
// being initialized for a test, and which proxy type a world uses. Tests that run in parallel can
// reset that state for each other, which made the suite fail intermittently; run them one at a time.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
