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
            ConstructClangCommandLine(export, out command, out commandArguments);
        }

        public static void ConstructClangCommandLine(CreateCompileCommand export, out string command, out string commandArguments)
        {
            bool isDebug = IsDebug(export.Configuration);

            // Create arguments
            var compilerFlags = new StringBuilder();
            SetConfigurationBasedFlags(isDebug, ref compilerFlags);

            // Set compiler flags
            compilerFlags.Append($"-shared -fpic ");
            compilerFlags.Append($"-D DNNE_ASSEMBLY_NAME={export.AssemblyName} -D DNNE_COMPILE_AS_SOURCE ");

            if (export.IsSelfContained)
            {
                compilerFlags.Append($"-D DNNE_SELF_CONTAINED_RUNTIME ");
            }

            compilerFlags.Append($"-I \"{export.PlatformPath}\" -I \"{export.NetHostPath}\" ");

            // Add user defined inc paths last - these will be searched last on clang.
            // https://clang.llvm.org/docs/ClangCommandLineReference.html#include-path-management
            foreach (var incPath in export.SafeAdditionalIncludeDirectories)
            {
                compilerFlags.Append($"-I \"{incPath.ItemSpec}\" ");
            }

            compilerFlags.Append($"-o \"{Path.Combine(export.OutputPath, export.OutputName)}\" ");

            if (!string.IsNullOrEmpty(export.UserDefinedCompilerFlags))
            {
                compilerFlags.Append($"{export.UserDefinedCompilerFlags} ");
            }

            compilerFlags.Append($"\"{export.Source}\" \"{Path.Combine(export.PlatformPath, "platform.c")}\" ");
            compilerFlags.Append($"-lstdc++ ");
            compilerFlags.Append($"\"{Path.Combine(export.NetHostPath, "libnethost.a")}\" ");

            if (!string.IsNullOrEmpty(export.UserDefinedLinkerFlags))
            {
                compilerFlags.Append($"{export.UserDefinedLinkerFlags} ");
            }

            command = "clang";
            commandArguments = compilerFlags.ToString();
        }

        private static bool IsDebug(string config)
        {
            return "Debug".Equals(config);
        }

        private static void SetConfigurationBasedFlags(bool isDebug, ref StringBuilder compiler)
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
