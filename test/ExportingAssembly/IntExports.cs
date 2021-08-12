// Copyright 2020 Aaron R Robinson
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
    public class IntExports
    {
        public delegate int IntIntDelegate(int a);

        [DNNE.Export]
        public static int IntInt(int a)
        {
            return a * 3;
        }

        [UnmanagedCallersOnly]
        public static int UnmanagedIntInt(int a)
        {
            return IntInt(a);
        }

        public delegate int IntIntIntDelegate(int a, int b);

        [DNNE.Export]
        public static int IntIntInt(int a, int b)
        {
            return a * b;
        }

        [UnmanagedCallersOnly]
        public static int UnmanagedIntIntInt(int a, int b)
        {
            return IntIntInt(a, b);
        }

        public delegate int VoidIntDelegate();

        [DNNE.Export]
        public static int VoidInt()
        {
            return 27;
        }

        [UnmanagedCallersOnly]
        public static int UnmanagedVoidInt()
        {
            return VoidInt();
        }

        public delegate void IntVoidDelegate(int a);

        [DNNE.Export]
        public static void IntVoid(int a)
        {
        }

        [UnmanagedCallersOnly]
        public static void UnmanagedIntVoid(int a)
        {
            IntVoid(a);
        }

        [UnmanagedCallersOnly(CallConvs = new [] { typeof(CallConvCdecl) })]
        public static void UnmanagedIntVoidCdecl(int a)
        {
            IntVoid(a);
        }
    }
}
