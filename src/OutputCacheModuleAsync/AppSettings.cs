// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See the License.txt file in the project root for full license information.

namespace Microsoft.AspNet.OutputCache {
    using System;
    using System.Collections.Specialized;
    using System.Configuration;
    using System.Web.Configuration;
    using System.Web.Hosting;

    static class AppSettings {
        private const string AllowLegacyOutputCacheKeysSetting =
            "aspnet:AllowLegacyOutputCacheKeys";

        private static volatile bool _settingsInitialized;
        private static readonly object _appSettingsLock = new object();
        private static bool _allowLegacyOutputCacheKeys;

        private static void EnsureSettingsLoaded() {
            if (_settingsInitialized) {
                return;
            }

            lock (_appSettingsLock) {
                if (_settingsInitialized) {
                    return;
                }

                NameValueCollection settings = null;
                try {
                    settings = GetAppSettingsSection();
                }
                finally {
                    _allowLegacyOutputCacheKeys = GetBooleanValue(
                        settings, AllowLegacyOutputCacheKeysSetting, false);
                    _settingsInitialized = true;
                }
            }
        }

        private static NameValueCollection GetAppSettingsSection() {
            if (!HostingEnvironment.IsHosted) {
                return ConfigurationManager.AppSettings;
            }

            return WebConfigurationManager.GetSection("appSettings")
                as NameValueCollection;
        }

        internal static bool GetBooleanValue(
            NameValueCollection settings, string key, bool defaultValue) {

            bool value;
            return settings != null && Boolean.TryParse(settings[key], out value)
                ? value
                : defaultValue;
        }

        internal static bool AllowLegacyOutputCacheKeys {
            get {
                EnsureSettingsLoaded();
                return _allowLegacyOutputCacheKeys;
            }
        }
    }
}
