// Copyright 2021 Aaron R Robinson
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

using System;
using System.Runtime.InteropServices;

namespace DNNE.UnitTests
{
    internal static class ExportingAssembly
    {
        private static class ExportingAssemblyNE { }

        public unsafe static class FunctionPointerExports
        {
            [DllImport(nameof(ExportingAssemblyNE))]
            public static extern void FunctionPointerVoid(delegate* unmanaged<void> fptr);

            [DllImport(nameof(ExportingAssemblyNE))]
            public static extern void UnmanagedFunctionPointerVoid(delegate* unmanaged<void> fptr);

            [DllImport(nameof(ExportingAssemblyNE))]
            public static extern void FunctionPointerStdcallIntIntVoid(delegate* unmanaged[Stdcall]<int, int, void> fptr, int a, int b);

            [DllImport(nameof(ExportingAssemblyNE))]
            public static extern void UnmanagedFunctionPointerStdcallIntIntVoid(delegate* unmanaged[Stdcall]<int, int, void> fptr, int a, int b);
        }

        public static class InstanceExports
        {
            [DllImport(nameof(ExportingAssemblyNE))]
            public static extern IntPtr MyClass_ctor();

            [DllImport(nameof(ExportingAssemblyNE))]
            public static extern void MyClass_dtor(IntPtr inst);

            [DllImport(nameof(ExportingAssemblyNE))]
            public static extern int MyClass_getNumber(IntPtr inst);

            [DllImport(nameof(ExportingAssemblyNE))]
            public static extern void MyClass_setNumber(IntPtr inst, int number);

            [DllImport(nameof(ExportingAssemblyNE))]
            public static extern void MyClass_printNumber(IntPtr inst);
        }

        public static class IntExports
        {
            [DllImport(nameof(ExportingAssemblyNE))]
            public static extern int IntInt(int a);

            [DllImport(nameof(ExportingAssemblyNE))]
            public static extern int UnmanagedIntInt(int a);

            [DllImport(nameof(ExportingAssemblyNE))]
            public static extern int IntIntInt(int a, int b);

            [DllImport(nameof(ExportingAssemblyNE))]
            public static extern int UnmanagedIntIntInt(int a, int b);

            [DllImport(nameof(ExportingAssemblyNE))]
            public static extern int VoidInt();

            [DllImport(nameof(ExportingAssemblyNE))]
            public static extern int UnmanagedVoidInt();

            [DllImport(nameof(ExportingAssemblyNE))]
            public static extern void IntVoid(int a);

            [DllImport(nameof(ExportingAssemblyNE))]
            public static extern void UnmanagedIntVoid(int a);

            [DllImport(nameof(ExportingAssemblyNE), CallingConvention = CallingConvention.Cdecl)]
            public static extern void UnmanagedIntVoidCdecl(int a);
        }

        public unsafe static class MiscExports
        {
            [DllImport(nameof(ExportingAssemblyNE))]
            public static extern void SetViaEntryPointProperty();

            [DllImport(nameof(ExportingAssemblyNE))]
            public static extern void UnmanagedSetViaEntryPointProperty();

            public struct Data
            {
                public int a;
                public int b;
                public int c;
            }

            [DllImport(nameof(ExportingAssemblyNE))]
            public static extern int ReturnDataCMember(Data d);

            [DllImport(nameof(ExportingAssemblyNE))]
            public static extern int ReturnRefDataCMember(ref Data d);
        }

        public static class RealExports
        {
            [DllImport(nameof(ExportingAssemblyNE))]
            public static extern float SingleSingle(float a);

            [DllImport(nameof(ExportingAssemblyNE))]
            public static extern float UnmanagedSingleSingle(float a);

            [DllImport(nameof(ExportingAssemblyNE))]
            public static extern float SingleSingleSingle(float a, float b);

            [DllImport(nameof(ExportingAssemblyNE))]
            public static extern float UnmanagedSingleSingleSingle(float a, float b);

            [DllImport(nameof(ExportingAssemblyNE))]
            public static extern float VoidSingle();

            [DllImport(nameof(ExportingAssemblyNE))]
            public static extern float UnmanagedVoidSingle();

            [DllImport(nameof(ExportingAssemblyNE))]
            public static extern void SingleVoid(float a);

            [DllImport(nameof(ExportingAssemblyNE))]
            public static extern void UnmanagedSingleVoid(float a);

            [DllImport(nameof(ExportingAssemblyNE))]
            public static extern double DoubleDouble(double a);

            [DllImport(nameof(ExportingAssemblyNE))]
            public static extern double UnmanagedDoubleDouble(double a);

            [DllImport(nameof(ExportingAssemblyNE))]
            public static extern double DoubleDoubleDouble(double a, double b);

            [DllImport(nameof(ExportingAssemblyNE))]
            public static extern double UnmanagedDoubleDoubleDouble(double a, double b);

            [DllImport(nameof(ExportingAssemblyNE))]
            public static extern double VoidDouble();

            [DllImport(nameof(ExportingAssemblyNE))]
            public static extern double UnmanagedVoidDouble();

            [DllImport(nameof(ExportingAssemblyNE))]
            public static extern void DoubleVoid(double a);

            [DllImport(nameof(ExportingAssemblyNE))]
            public static extern void UnmanagedDoubleVoid(double a);
        }

        public unsafe static class UnsafeExports
        {
            [DllImport(nameof(ExportingAssemblyNE))]
            public static extern void* VoidVoidPointer();

            [DllImport(nameof(ExportingAssemblyNE))]
            public static extern void* UnmanagedVoidVoidPointer();

            [DllImport(nameof(ExportingAssemblyNE))]
            public static extern int* VoidIntPointer();

            [DllImport(nameof(ExportingAssemblyNE))]
            public static extern int* UnmanagedVoidIntPointer();

            [DllImport(nameof(ExportingAssemblyNE))]
            public static extern IntPtr* VoidIntPtrPointer();

            [DllImport(nameof(ExportingAssemblyNE))]
            public static extern IntPtr* UnmanagedVoidIntPtrPointer();

            [DllImport(nameof(ExportingAssemblyNE))]
            public static extern UIntPtr* VoidUIntPtrPointer();

            [DllImport(nameof(ExportingAssemblyNE))]
            public static extern UIntPtr* UnmanagedVoidUIntPtrPointer();

            [DllImport(nameof(ExportingAssemblyNE))]
            public static extern IntPtr VoidIntPtr();

            [DllImport(nameof(ExportingAssemblyNE))]
            public static extern IntPtr UnmanagedVoidIntPtr();

            [DllImport(nameof(ExportingAssemblyNE))]
            public static extern UIntPtr VoidUIntPtr();

            [DllImport(nameof(ExportingAssemblyNE))]
            public static extern UIntPtr UnmanagedVoidUIntPtr();
        }
    }
}
