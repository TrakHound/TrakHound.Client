// Copyright (c) 2026 TrakHound Inc., All Rights Reserved.
// TrakHound Inc. licenses this file to you under the Apache License, Version 2.0.

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TrakHound.Http;

namespace TrakHound
{
    // The instance methods that the synced files use and .NET Standard 2.0 doesn't have, as extension methods (C# uses an extension method
    // when no instance overload fits). Only compiled for .NET Standard 2.0, and internal so apps never see them. Each one behaves like the
    // .NET method it replaces, except where a comment says otherwise
    internal static class InstanceShims
    {
        public static bool StartsWith(this string s, char value) => s.Length > 0 && s[0] == value;

        public static bool EndsWith(this string s, char value) => s.Length > 0 && s[s.Length - 1] == value;

        public static string[] Split(this string s, string separator, StringSplitOptions options = StringSplitOptions.None)
        {
            if (string.IsNullOrEmpty(separator)) return options == StringSplitOptions.RemoveEmptyEntries && s.Length == 0 ? new string[0] : new[] { s };
            return s.Split(new[] { separator }, options);
        }

        public static string GetString(this Encoding encoding, ReadOnlySpan<byte> bytes) => encoding.GetString(bytes.ToArray());

        public static int GetBytes(this Encoding encoding, string s, byte[] bytes) => encoding.GetBytes(s, 0, s.Length, bytes, 0);


        public static TValue GetValueOrDefault<TKey, TValue>(this IReadOnlyDictionary<TKey, TValue> dictionary, TKey key)
        {
            if (dictionary == null) throw new ArgumentNullException(nameof(dictionary));
            return dictionary.TryGetValue(key, out var value) ? value : default;
        }

        public static bool TryAdd<TKey, TValue>(this Dictionary<TKey, TValue> dictionary, TKey key, TValue value)
        {
            if (dictionary.ContainsKey(key)) return false;
            dictionary.Add(key, value);
            return true;
        }

        public static IEnumerable<TSource[]> Chunk<TSource>(this IEnumerable<TSource> source, int size)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (size < 1) throw new ArgumentOutOfRangeException(nameof(size));
            return ChunkIterator(source, size);
        }

        private static IEnumerable<TSource[]> ChunkIterator<TSource>(IEnumerable<TSource> source, int size)
        {
            var chunk = new List<TSource>(size);
            foreach (var item in source)
            {
                chunk.Add(item);
                if (chunk.Count == size)
                {
                    yield return chunk.ToArray();
                    chunk.Clear();
                }
            }
            if (chunk.Count > 0) yield return chunk.ToArray();
        }

        public static bool TryGetNonEnumeratedCount<TSource>(this IEnumerable<TSource> source, out int count)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));

            switch (source)
            {
                case ICollection<TSource> collection: count = collection.Count; return true;
                case IReadOnlyCollection<TSource> readOnlyCollection: count = readOnlyCollection.Count; return true;
                case ICollection nonGenericCollection: count = nonGenericCollection.Count; return true;
            }

            count = 0;
            return false;
        }


        public static async Task<TResult> WaitAsync<TResult>(this Task<TResult> task, TimeSpan timeout, CancellationToken cancellationToken)
        {
            if (task.IsCompleted) return await task.ConfigureAwait(false);

            using (var delayCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
            {
                var delay = Task.Delay(timeout, delayCancellation.Token);
                if (await Task.WhenAny(task, delay).ConfigureAwait(false) == task)
                {
                    delayCancellation.Cancel();
                    return await task.ConfigureAwait(false);
                }
            }

            cancellationToken.ThrowIfCancellationRequested();
            throw new TimeoutException();
        }


        // The response is already received when these are called (the request is sent with the token), so the token is only checked before
        // reading the content: .NET Standard 2.0 has no way to cancel the read itself
        public static Task<byte[]> ReadAsByteArrayAsync(this HttpContent content, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return content.ReadAsByteArrayAsync();
        }

        public static Task<Stream> ReadAsStreamAsync(this HttpContent content, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return content.ReadAsStreamAsync();
        }

        public static Task<string> ReadAsStringAsync(this HttpContent content, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return content.ReadAsStringAsync();
        }


        public static Task SendAsync(this WebSocket webSocket, byte[] buffer, WebSocketMessageType messageType, bool endOfMessage, CancellationToken cancellationToken)
        {
            return webSocket.SendAsync(new ArraySegment<byte>(buffer), messageType, endOfMessage, cancellationToken);
        }

        // .NET converts a byte[] to an ArraySegment<byte> implicitly, .NET Standard 2.0 doesn't
        public static Task<bool> Send(this TrakHoundWebSocketsClient client, byte[] messageBody)
        {
            return client.Send(new ArraySegment<byte>(messageBody));
        }
    }
}
