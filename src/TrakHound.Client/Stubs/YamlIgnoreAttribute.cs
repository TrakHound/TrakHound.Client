// Copyright (c) 2026 TrakHound Inc., All Rights Reserved.
// TrakHound Inc. licenses this file to you under the Apache License, Version 2.0.

using System;

namespace YamlDotNet.Serialization
{
    /// <summary>
    /// Stands in for YamlDotNet's attribute, which the synced Service and Router Configurations use to leave properties out of their YAML files.
    /// The client never reads or writes those files, so it doesn't depend on YamlDotNet (internal, so it doesn't clash with an app's own YamlDotNet)
    /// </summary>
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false)]
    internal sealed class YamlIgnoreAttribute : Attribute { }
}
