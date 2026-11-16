// Copyright (c) 2026 TrakHound Inc., All Rights Reserved.
// TrakHound Inc. licenses this file to you under the Apache License, Version 2.0.

namespace TrakHound.Configurations
{
    /// <summary>
    /// Stands in for the Instance's Secrets, which the synced Service Configuration uses to resolve Secret references in its Parameters.
    /// Secrets are resolved by the Instance that holds them, so on a client a Parameter is returned as it is
    /// </summary>
    internal static class TrakHoundSecrets
    {
        public static object Resolve(object value, string category, string configurationId, string parameterName) => value;
    }
}
