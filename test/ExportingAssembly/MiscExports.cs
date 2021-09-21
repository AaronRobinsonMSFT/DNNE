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
using System.Runtime.Versioning;

[assembly: SupportedOSPlatform("SET_ASSEMBLY_PLATFORM")]
[assembly: UnsupportedOSPlatform("UNSET_ASSEMBLY_PLATFORM1")]
[assembly: UnsupportedOSPlatform("UNSET_ASSEMBLY_PLATFORM2")]
[module: SupportedOSPlatform("SET_MODULE_PLATFORM")]
[module: UnsupportedOSPlatform("UNSET_MODULE_PLATFORM1")]
[module: UnsupportedOSPlatform("UNSET_MODULE_PLATFORM2")]
[module: UnsupportedOSPlatform("UNSET_MODULE_PLATFORM3")]

namespace ExportingAssembly
{
    [SupportedOSPlatform("SET_TYPE_PLATFORM")]
    [UnsupportedOSPlatform("UNSET_TYPE_PLATFORM1")]
    [UnsupportedOSPlatform("UNSET_TYPE_PLATFORM2")]
    [UnsupportedOSPlatform("UNSET_TYPE_PLATFORM3")]
    [UnsupportedOSPlatform("UNSET_TYPE_PLATFORM4")]
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

        [UnmanagedCallersOnly]
        [DNNE.C99DeclCode(
@"#define SET_ASSEMBLY_PLATFORM
#define SET_MODULE_PLATFORM
#define SET_TYPE_PLATFORM")]
        [SupportedOSPlatform("windows")]
        public static void OnlyOnWindows()
        {
        }

        [UnmanagedCallersOnly]
        [SupportedOSPlatform("osx")]
        public static void OnlyOnOSX()
        {
        }

        [UnmanagedCallersOnly]
        [SupportedOSPlatform("linux")]
        public static void OnlyOnLinux()
        {
        }

        [UnmanagedCallersOnly]
        [SupportedOSPlatform("freebsd")]
        public static void OnlyOnFreeSBD()
        {
        }

        [UnmanagedCallersOnly]
        [DNNE.C99DeclCode("#define __SET_PLATFORM__")]
        [SupportedOSPlatform("__SET_PLATFORM__")]
        [SupportedOSPlatform("SET_METHOD_PLATFORM")]
        [UnsupportedOSPlatform("UNSET_METHOD_PLATFORM1")]
        [UnsupportedOSPlatform("UNSET_METHOD_PLATFORM2")]
        [UnsupportedOSPlatform("UNSET_METHOD_PLATFORM3")]
        [UnsupportedOSPlatform("UNSET_METHOD_PLATFORM4")]
        [UnsupportedOSPlatform("UNSET_METHOD_PLATFORM5")]
        public static void ManuallySetPlatform()
        {
        }

        [UnmanagedCallersOnly]
        [SupportedOSPlatform("__NEVER_SUPPORTED_PLATFORM__")]
        public static void NeverSupportedPlatform()
        {
        }

        [UnmanagedCallersOnly]
        [UnsupportedOSPlatform("__NEVER_UNSUPPORTED_PLATFORM__")]
        public static void NeverUnsupportedPlatform()
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
