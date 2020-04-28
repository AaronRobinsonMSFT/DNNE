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

using Microsoft.Build.Framework;

using System;
using System.IO;
using System.Text;

namespace DNNE.BuildTasks
{
    public class macOS
    {
        public static void ConstructCommandLine(CreateCompileCommand export, out string command, out string commandArguments)
        {
            export.Report(MessageImportance.Low, $"Building for macOS");

            bool isDebug = IsDebug(export.Configuration);
            bool is64Bit = Is64BitTarget(export.Architecture);

            // Create arguments
            var compilerFlags = new StringBuilder();
            SetConfigurationBasedFlags(isDebug, ref compilerFlags);

            // Set compiler flags
            compilerFlags.Append($"-shared -fpic ");
            compilerFlags.Append($"-D DNNE_ASSEMBLY_NAME={export.AssemblyName} ");
            compilerFlags.Append($"-I \"{export.PlatformPath}\" -I \"{export.NetHostPath}\" ");
            compilerFlags.Append($"-lstdc++ ");
            compilerFlags.Append($"-o \"{Path.Combine(export.OutputPath, export.OutputName)}\" ");
            compilerFlags.Append($"\"{Path.Combine(export.NetHostPath, "libnethost.a")}\" ");
            compilerFlags.Append($"\"{export.Source}\" \"{Path.Combine(export.PlatformPath, "platform.c")}\" ");

            command = "clang";
            commandArguments = compilerFlags.ToString();
        }

        public static bool Is64BitTarget(string arch)
        {
            return arch switch
            {
                "AnyCPU" => true,
                _ => throw new Exception("Unknown architecture. Only support 'AnyCPU'."),
            };
        }

        public static bool IsDebug(string config)
        {
            return "Debug".Equals(config);
        }

        public static void SetConfigurationBasedFlags(bool isDebug, ref StringBuilder compiler)
        {
            if (isDebug)
            {
                compiler.Append($"-g -O0 ");
            }
            else
            {
                compiler.Append($"-O2 ");
            }
        }
    }
}
