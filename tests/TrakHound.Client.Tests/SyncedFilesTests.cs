// Copyright (c) 2026 TrakHound Inc., All Rights Reserved.
// TrakHound Inc. licenses this file to you under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Xunit;

namespace TrakHound.Client.Tests
{
    /// <summary>
    /// The files in Synced are copies from the TrakHound Framework. A change made to one here would be lost at the next sync (and would
    /// make it differ from what the Instance runs), so this fails when one doesn't match the hash sync.ps1 recorded for it
    /// </summary>
    public class SyncedFilesTests
    {
        [Fact]
        public void SyncedFilesMatchSyncedFrom()
        {
            var repository = FindRepository();
            var syncedRoot = Path.Combine(repository, "src", "TrakHound.Client", "Synced");

            var hashes = new Dictionary<string, string>();
            foreach (var line in File.ReadAllLines(Path.Combine(repository, "sync", "SYNCED_FROM")))
            {
                var match = Regex.Match(line, "^([0-9a-f]{64})  (.+)$");
                if (match.Success) hashes[match.Groups[2].Value] = match.Groups[1].Value;
            }
            Assert.NotEmpty(hashes);

            var problems = new List<string>();
            foreach (var entry in hashes)
            {
                var path = Path.Combine(syncedRoot, entry.Key.Replace('/', Path.DirectorySeparatorChar));
                if (!File.Exists(path)) problems.Add($"missing: {entry.Key}");
                else if (GetNormalizedHash(path) != entry.Value) problems.Add($"edited: {entry.Key}");
            }

            var root = syncedRoot.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            foreach (var path in Directory.GetFiles(syncedRoot, "*.cs", SearchOption.AllDirectories))
            {
                var file = path.Substring(root.Length).Replace(Path.DirectorySeparatorChar, '/');
                if (!hashes.ContainsKey(file)) problems.Add($"not synced: {file}");
            }

            Assert.True(problems.Count == 0, "Synced files don't match SYNCED_FROM (make the change in the Framework and run sync/sync.ps1):\n" + string.Join("\n", problems));
        }

        // The same hash as sync.ps1: SHA-256 of the UTF-8 text with LF line endings
        private static string GetNormalizedHash(string path)
        {
            var text = File.ReadAllText(path).Replace("\r\n", "\n");
            using (var sha = SHA256.Create())
            {
                return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(text))).Replace("-", "").ToLowerInvariant();
            }
        }

        private static string FindRepository()
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory != null && !File.Exists(Path.Combine(directory.FullName, "sync", "SYNCED_FROM")))
            {
                directory = directory.Parent;
            }

            return directory?.FullName ?? throw new InvalidOperationException("The repository (with sync/SYNCED_FROM) wasn't found above " + AppContext.BaseDirectory);
        }
    }
}
