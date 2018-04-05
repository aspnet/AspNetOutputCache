// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See the License.txt file in the project root for full license information.

namespace Microsoft.AspNet.OutputCache {
    using System;
    using System.Threading.Tasks;
    using System.Threading;

    // Wraps a Task class, optionally overriding the State object (since the Task Asynchronous Pattern doesn't normally use them).
    sealed class TaskWrapperAsyncResult : IAsyncResult {
        private bool _forceCompletedSynchronously;

        public TaskWrapperAsyncResult(Task task, object asyncState) {
            Task = task;
            AsyncState = asyncState;
        }

        public object AsyncState { get; }

        public WaitHandle AsyncWaitHandle => ((IAsyncResult) Task).AsyncWaitHandle;

        public bool CompletedSynchronously
            => _forceCompletedSynchronously || ((IAsyncResult) Task).CompletedSynchronously;

        public bool IsCompleted => ((IAsyncResult) Task).IsCompleted;

        public Task Task { get; }

        public void ForceCompletedSynchronously() {
            _forceCompletedSynchronously = true;
        }
    }
}