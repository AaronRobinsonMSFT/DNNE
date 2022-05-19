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
    public unsafe class WinBoolExports
    {
        public delegate DNNE.WinBool WinBoolWinBoolDelegate(DNNE.WinBool a);

        [DNNE.Export]
        public static DNNE.WinBool WinBoolWinBool(DNNE.WinBool a)
        {
            return !a;
        }

        [UnmanagedCallersOnly]
        public static DNNE.WinBool UnmanagedWinBoolWinBool(DNNE.WinBool a)
        {
            return WinBoolWinBool(a);
        }

        public delegate DNNE.WinBool WinBoolWinBoolWinBoolDelegate(DNNE.WinBool a, DNNE.WinBool b);

        [DNNE.Export]
        public static DNNE.WinBool WinBoolWinBoolWinBool(DNNE.WinBool a, DNNE.WinBool b)
        {
            return a && b;
        }

        [UnmanagedCallersOnly]
        public static DNNE.WinBool UnmanagedWinBoolWinBoolWinBool(DNNE.WinBool a, DNNE.WinBool b)
        {
            return WinBoolWinBoolWinBool(a, b);
        }

        public delegate void WinBoolVoidDelegate(DNNE.WinBool a);

        [DNNE.Export]
        public static void WinBoolVoid(DNNE.WinBool a)
        {
        }

        [UnmanagedCallersOnly]
        public static void UnmanagedWinBoolVoid(DNNE.WinBool a)
        {
            WinBoolVoid(a);
        }

        [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static void UnmanagedWinBoolVoidCdecl(DNNE.WinBool a)
        {
            WinBoolVoid(a);
        }
    }

    public unsafe class CBoolExports
    {
        public delegate DNNE.CBool CBoolCBoolDelegate(DNNE.CBool a);

        [DNNE.Export]
        public static DNNE.CBool CBoolCBool(DNNE.CBool a)
        {
            return !a;
        }

        [UnmanagedCallersOnly]
        public static DNNE.CBool UnmanagedCBoolCBool(DNNE.CBool a)
        {
            return CBoolCBool(a);
        }

        public delegate DNNE.CBool CBoolCBoolCBoolDelegate(DNNE.CBool a, DNNE.CBool b);

        [DNNE.Export]
        public static DNNE.CBool CBoolCBoolCBool(DNNE.CBool a, DNNE.CBool b)
        {
            return a && b;
        }

        [UnmanagedCallersOnly]
        public static DNNE.CBool UnmanagedCBoolCBoolCBool(DNNE.CBool a, DNNE.CBool b)
        {
            return CBoolCBoolCBool(a, b);
        }

        public delegate void CBoolVoidDelegate(DNNE.CBool a);

        [DNNE.Export]
        public static void CBoolVoid(DNNE.CBool a)
        {
        }

        [UnmanagedCallersOnly]
        public static void UnmanagedCBoolVoid(DNNE.CBool a)
        {
            CBoolVoid(a);
        }

        [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static void UnmanagedCBoolVoidCdecl(DNNE.CBool a)
        {
            CBoolVoid(a);
        }
    }
}
