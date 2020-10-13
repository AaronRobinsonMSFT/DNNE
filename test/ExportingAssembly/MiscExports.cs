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

using System.Runtime.InteropServices;

namespace ExportingAssembly
{
    public class MiscExports
    {
        public delegate void DontExportNameDelegate();

        [DNNE.Export(EntryPoint = "SetViaEntryPointProperty")]
        public static void DontExportName()
        {
        }

        [UnmanagedCallersOnly(EntryPoint = "UnmanagedSetViaEntryPointProperty")]
        public static void UnmanagedDontExportName()
        {
        }

        public struct Data
        {
            public int a;
            public int b;
            public int c;
        }

        [UnmanagedCallersOnly]
        [DNNE.C99DeclCode("struct T{int a; int b; int c;};")]
        public static int ReturnDataCMember([DNNE.C99Type("struct T")] Data d)
        {
            return d.c;
        }

        [UnmanagedCallersOnly]
        public unsafe static int ReturnRefDataCMember([DNNE.C99Type("struct T*")] Data* d)
        {
            return d->c;
        }
    }
}
