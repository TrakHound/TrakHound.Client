// Copyright (c) 2026 TrakHound Inc., All Rights Reserved.
// TrakHound Inc. licenses this file to you under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using TrakHound.Api;
using Xunit;

namespace TrakHound.Client.Tests
{
    /// <summary>
    /// On net48 these calls go to the shims in Polyfills, and on net10.0 to .NET itself. Both runs expect the same results, so they check
    /// that each shim behaves like the .NET member it replaces
    /// </summary>
    public class ShimTests
    {
        [Fact]
        public void HashesMatchKnownValues()
        {
            Assert.Equal("900150983cd24fb0d6963f7d28e17f72", "abc".ToMD5Hash());
            Assert.Equal("a9993e364706816aba3e25717850c26c9cd0d89d", "abc".ToSHA1Hash());
            Assert.Equal("ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad", "abc".ToSHA256Hash());
        }

        [Fact]
        public void HexConvertsBothWays()
        {
            Assert.Equal("00ff10ab", Convert.ToHexStringLower(new byte[] { 0x00, 0xff, 0x10, 0xab }));
            Assert.Equal(new byte[] { 0x00, 0xff, 0x10, 0xab }, Convert.FromHexString("00FF10ab"));
            Assert.Throws<FormatException>(() => Convert.FromHexString("abc"));
            Assert.Throws<FormatException>(() => Convert.FromHexString("zz"));
        }

        [Fact]
        public void RandomStringUsesOnlyTheChoices()
        {
            var s = RandomNumberGenerator.GetString("AB".AsSpan(), 200);
            Assert.Equal(200, s.Length);
            Assert.All(s, c => Assert.Contains(c, "AB"));
            Assert.Contains('A', s);
            Assert.Contains('B', s);

            Assert.Equal(20, StringFunctions.RandomString(20).Length);
        }

        [Fact]
        public void NumbersAndDatesMatchDotNet()
        {
            Assert.Equal(3, Math.Clamp(5, 0, 3));
            Assert.Equal(0, Math.Clamp(-1, 0, 3));
            Assert.True(double.IsFinite(1.5));
            Assert.False(double.IsFinite(double.NaN));
            Assert.False(float.IsFinite(float.PositiveInfinity));
            Assert.Equal(-12, int.Parse("x-12".AsSpan(1), System.Globalization.NumberStyles.AllowLeadingSign, System.Globalization.CultureInfo.InvariantCulture));
            Assert.Equal(new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc), DateTime.UnixEpoch);
            Assert.Equal(DateTimeKind.Utc, DateTime.UnixEpoch.Kind);
            Assert.True(Enum.IsDefined(TrakHoundApiMethod.PATCH));
            Assert.False(Enum.IsDefined((TrakHoundApiMethod)99));
        }

        [Fact]
        public void StringsMatchDotNet()
        {
            Assert.Equal("1,2,3", string.Join(',', new List<int> { 1, 2, 3 }));
            Assert.Equal("b/c", string.Join('/', new[] { "a", "b", "c" }, 1, 2));
            Assert.Equal(new[] { "a", "b", "" }, "a\rb\r".Split("\r"));
            Assert.True("/route".StartsWith('/'));
            Assert.True("select 1;".EndsWith(';'));
            Assert.False("".StartsWith('/'));
            Assert.Equal("a/b/c", Url.Combine("a", "/b/", "c"));
        }

        [Fact]
        public void CollectionsMatchDotNet()
        {
            var chunks = Enumerable.Range(1, 5).Chunk(2).ToList();
            Assert.Equal(3, chunks.Count);
            Assert.Equal(new[] { 5 }, chunks[2]);

            Assert.True(new List<int> { 1, 2 }.TryGetNonEnumeratedCount(out var count));
            Assert.Equal(2, count);
            Assert.False(Enumerable.Range(1, 2).Where(o => o > 0).TryGetNonEnumeratedCount(out _));

            var dictionary = new Dictionary<string, string> { ["a"] = "1" };
            Assert.False(dictionary.TryAdd("a", "2"));
            Assert.True(dictionary.TryAdd("b", "2"));
            Assert.Equal("1", dictionary.GetValueOrDefault("a"));
            Assert.Null(dictionary.GetValueOrDefault("c"));
        }

        [Fact]
        public void PathsMatchDotNet()
        {
            var root = Path.Combine(Path.GetTempPath(), "trakhound-client");
            Assert.Equal(Path.Combine("b", "c.txt"), Path.GetRelativePath(Path.Combine(root, "a"), Path.Combine(root, "a", "b", "c.txt")));
            Assert.Equal(Path.Combine("..", "d"), Path.GetRelativePath(Path.Combine(root, "a"), Path.Combine(root, "d")));
            Assert.Equal(".", Path.GetRelativePath(root, root));
        }

        [Fact]
        public async Task FilesAreWrittenAndMoved()
        {
            var directory = Path.Combine(Path.GetTempPath(), "trakhound-client-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            try
            {
                var source = Path.Combine(directory, "source.json");
                var destination = Path.Combine(directory, "destination.json");

                await File.WriteAllTextAsync(source, "{\"a\":1}");
                File.WriteAllText(destination, "old");
                File.Move(source, destination, true);

                Assert.False(File.Exists(source));
                Assert.Equal("{\"a\":1}", File.ReadAllText(destination));
                // No BOM, like .NET
                Assert.Equal((byte)'{', File.ReadAllBytes(destination)[0]);
            }
            finally
            {
                Directory.Delete(directory, true);
            }
        }

        [Fact]
        public async Task WaitAsyncTimesOutOrCancels()
        {
            Assert.True(await Task.FromResult(true).WaitAsync(TimeSpan.FromSeconds(1), CancellationToken.None));

            var never = new TaskCompletionSource<bool>();
            await Assert.ThrowsAsync<TimeoutException>(() => never.Task.WaitAsync(TimeSpan.FromMilliseconds(50), CancellationToken.None));

            using (var cancellation = new CancellationTokenSource(50))
            {
                await Assert.ThrowsAnyAsync<OperationCanceledException>(() => never.Task.WaitAsync(Timeout.InfiniteTimeSpan, cancellation.Token));
            }
        }

        [Fact]
        public void ApiMethodsConvertToHttpMethods()
        {
            Assert.Equal("PATCH", TrakHoundApiMethod.PATCH.ToHttpMethod().Method);
            Assert.Equal("CONNECT", TrakHoundApiMethod.CONNECT.ToHttpMethod().Method);
            Assert.Equal(System.Net.Http.HttpMethod.Get, TrakHoundApiMethod.GET.ToHttpMethod());
        }

        [Fact]
        public void RegexHelpersWork()
        {
            Assert.Equal(TimeSpan.FromMinutes(90), "1h30m".ToTimeSpan());
            Assert.True("<b>bold</b>".IsHtml());
            Assert.False("plain".IsHtml());
        }
    }
}
