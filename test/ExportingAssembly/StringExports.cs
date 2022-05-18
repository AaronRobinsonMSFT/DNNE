// Copyright 2022 Aaron R Robinson, Martin Wetzko
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is furnished
// to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED,
// INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A
// PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT
// HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
// OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE
// SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace ExportingAssembly
{
    public unsafe class StringExports
    {
        public delegate int StringIntDelegate(sbyte* a);

        [DNNE.Export]
        public static int StringInt(sbyte* a)
        {
            return new string(a).Length;
        }

        /// <summary>
        /// Return provided string's length
        /// </summary>
        /// <param name="a">Input value</param>
        /// <returns>string.Length</returns>
        [UnmanagedCallersOnly]
        public static int UnmanagedStringInt(sbyte* a)
        {
            return StringInt(a);
        }

        public delegate int StringStringIntDelegate(sbyte* a, sbyte* b);

        [DNNE.Export]
        public static int StringStringInt(sbyte* a, sbyte* b)
        {
            return new string(a).IndexOf(new string(b));
        }

        [UnmanagedCallersOnly]
        public static int UnmanagedStringStringInt(sbyte* a, sbyte* b)
        {
            return StringStringInt(a, b);
        }

        public delegate void StringVoidDelegate(sbyte* a);

        [DNNE.Export]
        public static void StringVoid(sbyte* a)
        {
        }

        [UnmanagedCallersOnly]
        public static void UnmanagedStringVoid(sbyte* a)
        {
            StringVoid(a);
        }

        [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static void UnmanagedStringVoidCdecl(sbyte* a)
        {
            StringVoid(a);
        }
    }

    public unsafe class WStringExports
    {
        public delegate int WStringIntDelegate(char* a);

        [DNNE.Export]
        public static int WStringInt(char* a)
        {
            return new string(a).Length;
        }

        /// <summary>
        /// Return provided string's length
        /// </summary>
        /// <param name="a">Input value</param>
        /// <returns>string.Length</returns>
        [UnmanagedCallersOnly]
        public static int UnmanagedWStringInt(char* a)
        {
            return WStringInt(a);
        }

        public delegate int WStringStringIntDelegate(char* a, char* b);

        [DNNE.Export]
        public static int WStringStringInt(char* a, char* b)
        {
            return new string(a).IndexOf(new string(b));
        }

        [UnmanagedCallersOnly]
        public static int UnmanagedWStringStringInt(char* a, char* b)
        {
            return WStringStringInt(a, b);
        }

        public delegate void WStringVoidDelegate(char* a);

        [DNNE.Export]
        public static void WStringVoid(char* a)
        {
        }

        [UnmanagedCallersOnly]
        public static void UnmanagedWStringVoid(char* a)
        {
            WStringVoid(a);
        }

        [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static void UnmanagedWStringVoidCdecl(char* a)
        {
            WStringVoid(a);
        }
    }
}
