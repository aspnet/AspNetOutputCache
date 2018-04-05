﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See the License.txt file in the project root for full license information.

namespace Microsoft.AspNet.OutputCache {
    using System;
    using System.Collections;
    using System.Globalization;
    using System.Security.Cryptography;

    class InvariantComparer : IComparer {
        private readonly CompareInfo _mCompareInfo;
        public static InvariantComparer Default = new InvariantComparer();

        private InvariantComparer() {
            _mCompareInfo = CultureInfo.InvariantCulture.CompareInfo;
        }

        public int Compare(object a, object b) {
            string sa = a as string;
            string sb = b as string;
            if (sa != null && sb != null)
                return _mCompareInfo.Compare(sa, sb);
            return Comparer.Default.Compare(a, b);
        }
    }

    static class CryptoUtil {
        /// <summary>
        /// Computes the SHA256 hash of a given input.
        /// </summary>
        /// <param Name="input">The input over which to compute the hash.</param>
        /// <param name="input"></param>
        /// <returns>The binary hash (32 bytes) of the input.</returns>
        public static byte[] ComputeHash(byte[] input) {
            return ComputeHash(input, 0, input.Length);
        }

        /// <summary>
        /// Computes the SHA256 hash of a given segment in a buffer.
        /// </summary>
        /// <param Name="buffer">The buffer over which to compute the hash.</param>
        /// <param Name="offset">The offset at which to begin computing the hash.</param>
        /// <param Name="count">The number of bytes in the buffer to include in the hash.</param>
        /// <param name="buffer"></param>
        /// <param name="offset"></param>
        /// <param name="count"></param>
        /// <returns>The binary hash (32 bytes) of the buffer segment.</returns>
        public static byte[] ComputeHash(byte[] buffer, int offset, int count) {

            using (SHA256 sha256 = new SHA256Cng()) {
                return sha256.ComputeHash(buffer, offset, count);
            }
        }
    }

    static class HttpDate {
        private static readonly int[] s_tensDigit = { 0, 10, 20, 30, 40, 50, 60, 70, 80, 90 };
        private static readonly string[] s_months = {
            "Jan", "Feb", "Mar", "Apr",
            "May", "Jun", "Jul", "Aug",
            "Sep", "Oct", "Nov", "Dec"
        };
        // Custom table for make_month() for mapping "Apr" to 4
        private static readonly sbyte[] s_monthIndexTable = {
            -1, (sbyte) 'A', 2, 12, -1, -1, -1, 8, // A to G
            -1, -1, -1, -1, 7, -1, (sbyte) 'N', -1, // H to O
            9, -1, (sbyte) 'R', -1, 10, -1, 11, -1, // P to W
            -1, 5, -1, -1, -1, -1, -1, -1, // X to Z
            -1, (sbyte) 'A', 2, 12, -1, -1, -1, 8, // a to g
            -1, -1, -1, -1, 7, -1, (sbyte) 'N', -1, // h to o
            9, -1, (sbyte) 'R', -1, 10, -1, 11, -1, // p to w
            -1, 5, -1, -1, -1, -1, -1, -1 // x to z
        };
        public static DateTime UtcParse(string time) {
            int i;
            int year, month, day, hour, minute, second;
            if (time == null) {
                throw new ArgumentNullException(nameof(time));
            }
            if ((i = time.IndexOf(',')) != -1) {
                //
                // Thursday, 10-Jun-93 01:29:59 GMT
                // or: Thu, 10 Jan 1993 01:29:59 GMT */
                //
                int length = time.Length - i;
                while (--length > 0 && time[++i] == ' ') { }
                if (time[i + 2] == '-') {
                    /* First format */
                    if (length < 18) {
                        throw new FormatException("UtilParseDateTimeBad");
                    }
                    day = Atoi2(time, i);
                    month = make_month(time, i + 3);
                    year = Atoi2(time, i + 7);
                    if (year < 50) {
                        year += 2000;
                    }
                    else {
                        year += 1900;
                    }
                    hour = Atoi2(time, i + 10);
                    minute = Atoi2(time, i + 13);
                    second = Atoi2(time, i + 16);
                }
                else {
                    /* Second format */
                    if (length < 20) {
                        throw new FormatException("UtilParseDateTimeBad");
                    }
                    day = Atoi2(time, i);
                    month = make_month(time, i + 3);
                    year = Atoi2(time, i + 7) * 100 + Atoi2(time, i + 9);
                    hour = Atoi2(time, i + 12);
                    minute = Atoi2(time, i + 15);
                    second = Atoi2(time, i + 18);
                }
            }
            else {
                /* Try the other format:  Wed Jun 09 01:29:59 1993 GMT */
                i = -1;
                int length = time.Length + 1;
                while (--length > 0 && time[++i] == ' ') { }
                if (length < 24) {
                    throw new FormatException("UtilParseDateTimeBad");
                }
                day = Atoi2(time, i + 8);
                month = make_month(time, i + 4);
                year = Atoi2(time, i + 20) * 100 + Atoi2(time, i + 22);
                hour = Atoi2(time, i + 11);
                minute = Atoi2(time, i + 14);
                second = Atoi2(time, i + 17);
            }
            return new DateTime(year, month, day, hour, minute, second, DateTimeKind.Utc);
        }

        private static int Atoi2(string s, int startIndex) {
            try {
                int tens = s[0 + startIndex] - '0';
                int ones = s[1 + startIndex] - '0';

                return s_tensDigit[tens] + ones;
            }
            catch {
                throw new FormatException("Atio2Badstring");
            }
        }

        private static int make_month(string s, int startIndex) {
            //
            // use the third character as the index
            //
            int i = (s[2 + startIndex] - 0x40) & 0x3F;
            sbyte monthIndex = s_monthIndexTable[i];
            if (monthIndex >= 13) {
                //
                // ok, we need to look at the second character
                //
                switch (monthIndex) {
                    case (sbyte)'N':
                        //
                        // we got an N which we need to resolve further
                        //
                        //
                        // if s[1] is 'u' then Jun, if 'a' then Jan
                        //
                        monthIndex =
                            (sbyte)(s_monthIndexTable[(s[1 + startIndex] - 0x40) & 0x3f] == (sbyte)'A' ? 1 : 6);
                        break;
                    case (sbyte)'R':
                        //
                        // if s[1] is 'a' then March, if 'p' then April
                        //
                        monthIndex =
                            (sbyte)(s_monthIndexTable[(s[1 + startIndex] - 0x40) & 0x3f] == (sbyte)'A' ? 3 : 4);
                        break;
                    default:
                        throw new FormatException("MakeMonthBadstring");
                }
            }
            string monthstring = s_months[monthIndex - 1];
            if ((s[0 + startIndex] == monthstring[0]) &&
                (s[1 + startIndex] == monthstring[1]) &&
                (s[2 + startIndex] == monthstring[2])) {
                return (monthIndex);
            }
            if ((char.ToUpper(s[0 + startIndex], CultureInfo.InvariantCulture) == monthstring[0]) &&
                (char.ToLower(s[1 + startIndex], CultureInfo.InvariantCulture) == monthstring[1]) &&
                (char.ToLower(s[2 + startIndex], CultureInfo.InvariantCulture) == monthstring[2])) {
                return monthIndex;
            }
            throw new FormatException("MakeMonthBadstring");
        }
     }

    static class StringUtil {
        public static bool StringArrayEquals(string[] a, string[] b) {
            if ((a == null) != (b == null)) {
                return false;
            }
            if (a == null) {
                return true;
            }
            int n = a.Length;
            if (n != b.Length) {
                return false;
            }
            for (int i = 0; i < n; i++) {
                if (a[i] != b[i]) {
                    return false;
                }
            }
            return true;
        }
    }
}