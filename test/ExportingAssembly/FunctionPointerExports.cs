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
    public unsafe class FunctionPointerExports
    {
        public delegate void FunctionPointerVoidDelegate(delegate* unmanaged<void> fptr);

        [DNNE.Export]
        public static void FunctionPointerVoid(delegate* unmanaged<void> fptr)
        {
            fptr();
        }

        [UnmanagedCallersOnly]
        public static void UnmanagedFunctionPointerVoid(delegate* unmanaged<void> fptr)
        {
            fptr();
        }

        public delegate void FunctionPointerStdcallIntIntVoidDelegate(delegate* unmanaged[Stdcall]<int, int> fptr, int a, int b);

        [DNNE.Export]
        public static void FunctionPointerStdcallIntIntVoid(delegate* unmanaged[Stdcall]<int, int, void> fptr, int a, int b)
        {
            fptr(a, b);
        }

        [UnmanagedCallersOnly]
        public static void UnmanagedFunctionPointerStdcallIntIntVoid(delegate* unmanaged[Stdcall]<int, int, void> fptr, int a, int b)
        {
            fptr(a, b);
        }
    }
}
