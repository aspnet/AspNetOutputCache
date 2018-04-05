// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See the License.txt file in the project root for full license information.

namespace Microsoft.AspNet.OutputCache {  
    sealed class CacheDirectives {
        public const string NoCache = "no-cache";
        public const string NoStore = "no-store";
        public const string MaxAge = "max-age=";
        public const string MinFresh = "min-fresh=";
    }
}
