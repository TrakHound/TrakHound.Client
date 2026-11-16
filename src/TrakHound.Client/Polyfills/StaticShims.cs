// Copyright (c) 2026 TrakHound Inc., All Rights Reserved.
// TrakHound Inc. licenses this file to you under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace TrakHound
{
    // The static members that the synced files use and .NET Standard 2.0 doesn't have, added with C# 14 extension blocks so the synced files
    // build unchanged. Only compiled for .NET Standard 2.0, and internal so apps never see them. Each one behaves like the .NET member it replaces
    internal static class StaticShims
    {
        extension(Math)
        {
            public static int Clamp(int value, int min, int max)
            {
                if (min > max) throw new ArgumentException($"'{min}' cannot be greater than {max}.");
                return value < min ? min : value > max ? max : value;
            }
        }

        extension(double)
        {
            public static bool IsFinite(double d) => !double.IsNaN(d) && !double.IsInfinity(d);
        }

        extension(float)
        {
            public static bool IsFinite(float f) => !float.IsNaN(f) && !float.IsInfinity(f);
        }

        extension(int)
        {
            public static int Parse(ReadOnlySpan<char> s, NumberStyles style, IFormatProvider provider) => int.Parse(s.ToString(), style, provider);
        }

        extension(DateTime)
        {
            public static DateTime UnixEpoch => new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        }

        extension(Enum)
        {
            public static bool IsDefined<TEnum>(TEnum value) where TEnum : struct, Enum => Enum.IsDefined(typeof(TEnum), value);
        }

        extension(string)
        {
            public static string Join<T>(char separator, IEnumerable<T> values) => string.Join(separator.ToString(), values);

            public static string Join(char separator, params string[] value) => string.Join(separator.ToString(), value);

            public static string Join(char separator, string[] value, int startIndex, int count) => string.Join(separator.ToString(), value, startIndex, count);
        }

        extension(Convert)
        {
            public static string ToHexStringLower(byte[] inArray)
            {
                if (inArray == null) throw new ArgumentNullException(nameof(inArray));

                var s = new StringBuilder(inArray.Length * 2);
                foreach (var b in inArray) s.Append(b.ToString("x2", CultureInfo.InvariantCulture));
                return s.ToString();
            }

            public static byte[] FromHexString(string s)
            {
                if (s == null) throw new ArgumentNullException(nameof(s));
                if (s.Length % 2 != 0) throw new FormatException("The input is not a valid hex string as its length is not a multiple of 2.");

                var bytes = new byte[s.Length / 2];
                for (var i = 0; i < bytes.Length; i++)
                {
                    if (!byte.TryParse(s.Substring(i * 2, 2), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out bytes[i]))
                    {
                        throw new FormatException("The input is not a valid hex string as it contains a non-hex character.");
                    }
                }
                return bytes;
            }
        }

        extension(MD5)
        {
            public static byte[] HashData(byte[] source)
            {
                using (var algorithm = MD5.Create()) return algorithm.ComputeHash(source ?? throw new ArgumentNullException(nameof(source)));
            }
        }

        extension(RandomNumberGenerator)
        {
            public static string GetString(ReadOnlySpan<char> choices, int length)
            {
                if (choices.IsEmpty) throw new ArgumentException("The choices can't be empty", nameof(choices));
                if (length < 0) throw new ArgumentOutOfRangeException(nameof(length));

                var result = new char[length];
                var buffer = new byte[4];
                using (var random = RandomNumberGenerator.Create())
                {
                    // Rejection sampling, so every choice is equally likely
                    var range = (uint)choices.Length;
                    var limit = uint.MaxValue - (uint.MaxValue % range);
                    for (var i = 0; i < length; i++)
                    {
                        uint value;
                        do
                        {
                            random.GetBytes(buffer);
                            value = BitConverter.ToUInt32(buffer, 0);
                        }
                        while (value >= limit);

                        result[i] = choices[(int)(value % range)];
                    }
                }
                return new string(result);
            }
        }

        extension(File)
        {
            public static void Move(string sourceFileName, string destFileName, bool overwrite)
            {
                if (overwrite && File.Exists(destFileName)) File.Delete(destFileName);
                File.Move(sourceFileName, destFileName);
            }

            public static async Task WriteAllTextAsync(string path, string contents, CancellationToken cancellationToken = default)
            {
                cancellationToken.ThrowIfCancellationRequested();

                // UTF-8 without a BOM, like File.WriteAllTextAsync
                using (var writer = new StreamWriter(path, false, new UTF8Encoding(false)))
                {
                    await writer.WriteAsync(contents).ConfigureAwait(false);
                }
            }
        }

        extension(Path)
        {
            public static string GetRelativePath(string relativeTo, string path)
            {
                if (relativeTo == null) throw new ArgumentNullException(nameof(relativeTo));
                if (path == null) throw new ArgumentNullException(nameof(path));

                var from = Path.GetFullPath(relativeTo).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                var to = Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                if (string.Equals(from, to, StringComparison.OrdinalIgnoreCase)) return ".";

                // Different roots (ex. other drives) can't be made relative
                if (!string.Equals(Path.GetPathRoot(from), Path.GetPathRoot(to), StringComparison.OrdinalIgnoreCase)) return to;

                var relative = new Uri(from + Path.DirectorySeparatorChar).MakeRelativeUri(new Uri(to));
                return Uri.UnescapeDataString(relative.ToString()).Replace('/', Path.DirectorySeparatorChar);
            }
        }
    }

    // Each HashData is in its own class: in one class they would compile to static methods with the same signature
    internal static class Sha1Shims
    {
        extension(SHA1)
        {
            public static byte[] HashData(byte[] source)
            {
                using (var algorithm = SHA1.Create()) return algorithm.ComputeHash(source ?? throw new ArgumentNullException(nameof(source)));
            }
        }
    }

    internal static class Sha256Shims
    {
        extension(SHA256)
        {
            public static byte[] HashData(byte[] source)
            {
                using (var algorithm = SHA256.Create()) return algorithm.ComputeHash(source ?? throw new ArgumentNullException(nameof(source)));
            }
        }
    }
}
