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
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Xunit;

namespace DNNE.UnitTests
{
    public class Consumption
    {
        [Fact]
        public void ValidateDnneProjectAssets()
        {
            string currDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

            // The following native assets are expected to flow with a project reference
            // to a managed project using DNNE to create a native exporting binary.
            string[] assets = new[]
            {
                Path.Combine(currDirectory, nameof(ExportingAssembly.ExportingAssemblyNE) + GetSystemExtension()),
                Path.Combine(currDirectory, nameof(ExportingAssembly.ExportingAssemblyNE) + ".h"),
                Path.Combine(currDirectory, "dnne.h"),
            };

            // On Windows we also propagate the PDB symbols file.
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                assets = assets.Append(Path.Combine(currDirectory, nameof(ExportingAssembly.ExportingAssemblyNE) + ".pdb")).ToArray();

            foreach (var a in assets)
            {
                Assert.True(File.Exists(a));
            }

            static string GetSystemExtension()
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                    return ".dll";
                if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                    return ".dylib";
                return ".so";
            }
        }

        [Fact]
        public unsafe void FunctionPointerExports()
        {
            ExportingAssembly.FunctionPointerExports.FunctionPointerVoid(&VoidVoid);
            ExportingAssembly.FunctionPointerExports.UnmanagedFunctionPointerVoid(&VoidVoid);
            ExportingAssembly.FunctionPointerExports.FunctionPointerStdcallIntIntVoid(&IntIntVoid, 3, 4);
            ExportingAssembly.FunctionPointerExports.UnmanagedFunctionPointerStdcallIntIntVoid(&IntIntVoid, 3, 4);

            [UnmanagedCallersOnly]
            static void VoidVoid()
            { }

            [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvStdcall) })]
            static void IntIntVoid(int a, int b)
            { }
        }

        [Fact]
        public void InstanceExports()
        {
            IntPtr inst = ExportingAssembly.InstanceExports.MyClass_ctor();
            Assert.True(inst != IntPtr.Zero);

            Assert.Equal(0, ExportingAssembly.InstanceExports.MyClass_getNumber(inst));

            int num = 29;
            ExportingAssembly.InstanceExports.MyClass_setNumber(inst, num);

            Assert.Equal(num, ExportingAssembly.InstanceExports.MyClass_getNumber(inst));

            ExportingAssembly.InstanceExports.MyClass_printNumber(inst);
            ExportingAssembly.InstanceExports.MyClass_dtor(inst);
        }

        [Fact]
        public void IntExports()
        {
            Assert.Equal(3 * 4, ExportingAssembly.IntExports.IntInt(4));
            Assert.Equal(3 * 4, ExportingAssembly.IntExports.UnmanagedIntInt(4));
            Assert.Equal(3 * 5, ExportingAssembly.IntExports.IntIntInt(3, 5));
            Assert.Equal(3 * 5, ExportingAssembly.IntExports.UnmanagedIntIntInt(3, 5));
            Assert.Equal(27, ExportingAssembly.IntExports.VoidInt());
            Assert.Equal(27, ExportingAssembly.IntExports.UnmanagedVoidInt());
            ExportingAssembly.IntExports.IntVoid(33);
            ExportingAssembly.IntExports.UnmanagedIntVoid(33);
            ExportingAssembly.IntExports.UnmanagedIntVoidCdecl(33);
        }

        [Fact]
        public void MiscExports()
        {
            ExportingAssembly.MiscExports.SetViaEntryPointProperty();
            ExportingAssembly.MiscExports.UnmanagedSetViaEntryPointProperty();

            try
            {
                ExportingAssembly.MiscExports.OnlyOnWindows();
                Assert.True(RuntimeInformation.IsOSPlatform(OSPlatform.Windows));
            }
            catch (EntryPointNotFoundException)
            {
                Assert.False(RuntimeInformation.IsOSPlatform(OSPlatform.Windows));
            }
            try
            {
                ExportingAssembly.MiscExports.OnlyOnOSX();
                Assert.True(RuntimeInformation.IsOSPlatform(OSPlatform.OSX));
            }
            catch (EntryPointNotFoundException)
            {
                Assert.False(RuntimeInformation.IsOSPlatform(OSPlatform.OSX));
            }
            try
            {
                ExportingAssembly.MiscExports.OnlyOnFreeBSD();
                Assert.True(RuntimeInformation.IsOSPlatform(OSPlatform.FreeBSD));
            }
            catch (EntryPointNotFoundException)
            {
                Assert.False(RuntimeInformation.IsOSPlatform(OSPlatform.FreeBSD));
            }
            try
            {
                ExportingAssembly.MiscExports.OnlyOnLinux();
                Assert.True(RuntimeInformation.IsOSPlatform(OSPlatform.Linux));
            }
            catch (EntryPointNotFoundException)
            {
                Assert.False(RuntimeInformation.IsOSPlatform(OSPlatform.Linux));
            }

            ExportingAssembly.MiscExports.ManuallySetPlatform();
            Assert.Throws<EntryPointNotFoundException>(() => ExportingAssembly.MiscExports.NeverSupportedPlatform());
            ExportingAssembly.MiscExports.NeverUnsupportedPlatform();

            int c = 29;
            {
                var d = new ExportingAssembly.MiscExports.Data()
                {
                    a = -1,
                    b = -1,
                    c = c
                };
                Assert.Equal(c, ExportingAssembly.MiscExports.ReturnDataCMember(d));
            }
            {
                var d = new ExportingAssembly.MiscExports.Data()
                {
                    a = -1,
                    b = -1,
                    c = c
                };
                Assert.Equal(c, ExportingAssembly.MiscExports.ReturnRefDataCMember(ref d));
            }
        }

        [Fact]
        public void RealExports()
        {
            Assert.Equal(3 * 3, ExportingAssembly.RealExports.SingleSingle(3));
            Assert.Equal(3 * 3, ExportingAssembly.RealExports.UnmanagedSingleSingle(3));
            Assert.Equal(3 * 4, ExportingAssembly.RealExports.SingleSingleSingle(3, 4));
            Assert.Equal(3 * 4, ExportingAssembly.RealExports.UnmanagedSingleSingleSingle(3, 4));
            Assert.Equal(27, ExportingAssembly.RealExports.VoidSingle());
            Assert.Equal(27, ExportingAssembly.RealExports.UnmanagedVoidSingle());
            ExportingAssembly.RealExports.SingleVoid(27);
            ExportingAssembly.RealExports.UnmanagedSingleVoid(27);

            Assert.Equal(3 * 3, ExportingAssembly.RealExports.DoubleDouble(3));
            Assert.Equal(3 * 3, ExportingAssembly.RealExports.UnmanagedDoubleDouble(3));
            Assert.Equal(3 * 4, ExportingAssembly.RealExports.DoubleDoubleDouble(3, 4));
            Assert.Equal(3 * 4, ExportingAssembly.RealExports.UnmanagedDoubleDoubleDouble(3, 4));
            Assert.Equal(27, ExportingAssembly.RealExports.VoidDouble());
            Assert.Equal(27, ExportingAssembly.RealExports.UnmanagedVoidDouble());
            ExportingAssembly.RealExports.DoubleVoid(27);
            ExportingAssembly.RealExports.UnmanagedDoubleVoid(27);
        }

        [Fact]
        public void StringExports()
        {
            Assert.Equal(26, ExportingAssembly.StringExports.StringInt("DNNE - .Net Native Exports"));
            Assert.Equal(26, ExportingAssembly.StringExports.UnmanagedStringInt("DNNE - .Net Native Exports"));
            Assert.Equal(7, ExportingAssembly.StringExports.StringStringInt("DNNE - .Net Native Exports", ".Net"));
            Assert.Equal(7, ExportingAssembly.StringExports.UnmanagedStringStringInt("DNNE - .Net Native Exports", ".Net"));
            Assert.Equal(-1, ExportingAssembly.StringExports.StringStringInt("DNNE - .Net Native Exports", "..Net"));
            Assert.Equal(-1, ExportingAssembly.StringExports.UnmanagedStringStringInt("DNNE - .Net Native Exports", "..Net"));
            ExportingAssembly.StringExports.StringVoid("DNNE");
            ExportingAssembly.StringExports.UnmanagedStringVoid("DNNE");
            ExportingAssembly.StringExports.UnmanagedStringVoidCdecl("DNNE");
        }

        [Fact]
        public void WStringExports()
        {
            Assert.Equal(26, ExportingAssembly.WStringExports.WStringInt("DNNE - .Net Native Exports"));
            Assert.Equal(26, ExportingAssembly.WStringExports.UnmanagedWStringInt("DNNE - .Net Native Exports"));
            Assert.Equal(7, ExportingAssembly.WStringExports.WStringStringInt("DNNE - .Net Native Exports", ".Net"));
            Assert.Equal(7, ExportingAssembly.WStringExports.UnmanagedWStringStringInt("DNNE - .Net Native Exports", ".Net"));
            Assert.Equal(-1, ExportingAssembly.WStringExports.WStringStringInt("DNNE - .Net Native Exports", "..Net"));
            Assert.Equal(-1, ExportingAssembly.WStringExports.UnmanagedWStringStringInt("DNNE - .Net Native Exports", "..Net"));
            ExportingAssembly.WStringExports.WStringVoid("DNNE");
            ExportingAssembly.WStringExports.UnmanagedWStringVoid("DNNE");
            ExportingAssembly.WStringExports.UnmanagedWStringVoidCdecl("DNNE");
        }

        [Fact]
        public unsafe void UnsafeExports()
        {
            Assert.True(null == ExportingAssembly.UnsafeExports.VoidVoidPointer());
            Assert.True(null == ExportingAssembly.UnsafeExports.UnmanagedVoidVoidPointer());
            Assert.True(null == ExportingAssembly.UnsafeExports.VoidIntPointer());
            Assert.True(null == ExportingAssembly.UnsafeExports.UnmanagedVoidIntPointer());
            Assert.True(null == ExportingAssembly.UnsafeExports.VoidIntPtrPointer());
            Assert.True(null == ExportingAssembly.UnsafeExports.UnmanagedVoidIntPtrPointer());
            Assert.True(null == ExportingAssembly.UnsafeExports.VoidUIntPtrPointer());
            Assert.True(null == ExportingAssembly.UnsafeExports.UnmanagedVoidUIntPtrPointer());
            Assert.True(IntPtr.Zero == ExportingAssembly.UnsafeExports.VoidIntPtr());
            Assert.True(IntPtr.Zero == ExportingAssembly.UnsafeExports.UnmanagedVoidIntPtr());
            Assert.True(UIntPtr.Zero == ExportingAssembly.UnsafeExports.VoidUIntPtr());
            Assert.True(UIntPtr.Zero == ExportingAssembly.UnsafeExports.UnmanagedVoidUIntPtr());
        }

        [Fact]
        public void NestedClassExports()
        {
            ExportingAssembly.NestedClassExports.Nested1_VoidVoid();
            ExportingAssembly.NestedClassExports.Nested1_UnmanagedVoidVoid();
            ExportingAssembly.NestedClassExports.Nested2_VoidVoid();
            ExportingAssembly.NestedClassExports.Nested2_UnmanagedVoidVoid();
        }
    }
}
