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
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace DNNE.BuildTasks
{
    public class Windows
    {
        public static void ConstructCommandLine(CreateCompileCommand export, out string command, out string commandArguments)
        {
            export.Report(MessageImportance.Low, $"Building for Windows");

            string vcArch = ConvertToVCArchString(export.Architecture, export.RuntimeID);
            string vcvarsallInfo = GetVcvarsallInfo(vcArch, export.FindVcvarsallPath);

            string[] parts = vcvarsallInfo.Trim().Split('#');
            if (parts.Length < 4)
            {
                throw new Exception($"Unexpected output from findvcvarsall.bat: '{vcvarsallInfo}'.");
            }

            string vsInstall = parts[0].Trim();
            string cppToolsDir = parts[1].Trim();
            string compilerPath = Path.Combine(cppToolsDir, "cl.exe");
            string vcToolDir = GetVCToolsRootDir(vsInstall);
            List<string> vcvarsallLibPaths = [];
            List<string> vcvarsallIncludePaths = [];

            foreach (string libPath in parts[2].Split([';'], StringSplitOptions.RemoveEmptyEntries))
            {
                vcvarsallLibPaths.Add(libPath.Trim());
            }

            foreach (string includePath in parts[3].Split([';'], StringSplitOptions.RemoveEmptyEntries))
            {
                vcvarsallIncludePaths.Add(includePath.Trim());
            }

            export.Report(CreateCompileCommand.DevImportance, $"VS Install: {vsInstall}\nVC Tools: {vcToolDir}\nCompiler: {compilerPath}");

            bool isDebug = IsDebug(export.Configuration);

            // VC inc and lib paths
            var vcIncDir = Path.Combine(vcToolDir, "include");
            var libDir = Path.Combine(vcToolDir, "lib", vcArch);

            string compileAsFlag;
            string hostLib;
            string platformTU;
            if (export.IsTargetingNetFramework)
            {
                // Targeting .NET Framework means we compile everything as C++.
                compileAsFlag = "/TP";
                hostLib = "mscoree.lib";
                platformTU = Path.Combine(export.PlatformPath, "platform_v4.cpp");
            }
            else
            {
                compileAsFlag = "/TC";
                hostLib = $"\"{Path.Combine(export.NetHostPath, "libnethost.lib")}\"";
                platformTU = Path.Combine(export.PlatformPath, "platform.c");
            }

            // Create arguments
            var compilerFlags = new StringBuilder();
            var linkerFlags = new StringBuilder();
            SetConfigurationBasedFlags(isDebug, ref compilerFlags, ref linkerFlags);

            // Set compiler flags
            compilerFlags.Append($"{compileAsFlag} /MT /GS /Zi ");
            compilerFlags.Append($"/D DNNE_ASSEMBLY_NAME={export.AssemblyName} /D DNNE_COMPILE_AS_SOURCE ");

            // Check if user supplied a def file.
            string exportsDefFile = export.AbsoluteExportsDefFilePath;
            if (!string.IsNullOrEmpty(exportsDefFile))
            {
                // The macro needs to be empty, not just defined.
                compilerFlags.Append($"/D DNNE_API_OVERRIDE= ");
            }

            if (export.IsSelfContained)
            {
                compilerFlags.Append($"/D DNNE_SELF_CONTAINED_RUNTIME ");
            }

            if (export.IsTargetingNetFramework)
            {
                compilerFlags.Append($"/D DNNE_TARGET_NET_FRAMEWORK ");
            }

            compilerFlags.Append($"/I \"{vcIncDir}\" /I \"{export.PlatformPath}\" /I \"{export.NetHostPath}\" ");

            foreach (var incPath in vcvarsallIncludePaths)
            {
                compilerFlags.Append($"/I \"{incPath}\" ");
            }

            // Add user defined inc paths last - these will be searched last on MSVC.
            // https://docs.microsoft.com/cpp/build/reference/i-additional-include-directories#remarks
            foreach (var incPath in export.SafeAdditionalIncludeDirectories)
            {
                compilerFlags.Append($"/I \"{incPath.ItemSpec}\" ");
            }

            if (!string.IsNullOrEmpty(export.UserDefinedCompilerFlags))
            {
                compilerFlags.Append($"{export.UserDefinedCompilerFlags} ");
            }

            // Set linker flags
            linkerFlags.Append($"/DLL ");

            if (!string.IsNullOrEmpty(exportsDefFile))
            {
                linkerFlags.Append($"/DEF:\"{exportsDefFile}\" ");
            }

            linkerFlags.Append($"/LIBPATH:\"{libDir}\" ");

            foreach (var libPath in vcvarsallLibPaths)
            {
                linkerFlags.Append($"/LIBPATH:\"{libPath}\" ");
            }

            linkerFlags.Append($"{hostLib} Advapi32.lib ");
            linkerFlags.Append($"/IGNORE:4099 "); // libnethost.lib doesn't ship PDBs so linker warnings occur.

            // Define artifact names
            var outputPath = Path.Combine(export.OutputPath, export.OutputName);
            var impLibPath = Path.ChangeExtension(outputPath, ".lib");
            linkerFlags.Append($"/IMPLIB:\"{impLibPath}\" /OUT:\"{outputPath}\" ");

            if (!string.IsNullOrEmpty(export.UserDefinedLinkerFlags))
            {
                linkerFlags.Append($"{export.UserDefinedLinkerFlags} ");
            }

            command = compilerPath;
            commandArguments = $"{compilerFlags} \"{export.Source}\" \"{platformTU}\" /link {linkerFlags}";
        }

        private static string GetVcvarsallInfo(string vcArch, string findVcvarsallPath)
        {
            if (string.IsNullOrWhiteSpace(findVcvarsallPath))
            {
                throw new Exception("Required path to findvcvarsall.bat was not provided.");
            }

            string scriptPath = Path.GetFullPath(findVcvarsallPath);
            if (!File.Exists(scriptPath))
            {
                throw new Exception($"Required script not found: '{scriptPath}'.");
            }

            using Process process = new();
            process.StartInfo = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = $"/d /c \"\"{scriptPath}\" {vcArch}\"",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            };

            process.Start();
            string output = process.StandardOutput.ReadToEnd();
            string error = process.StandardError.ReadToEnd();
            process.WaitForExit();

            if (process.ExitCode != 0)
            {
                throw new Exception($"findvcvarsall.bat failed with exit code {process.ExitCode}. {error}");
            }

            return output.Trim();
        }

        private static string ConvertToVCArchString(string arch, string rid)
        {
            return arch.ToLower() switch
            {
                "x64" or "amd64" => "x64",
                "x86" => "x86",
                "arm64" => "arm64",
                "msil" => rid.Contains("x64") // e.g. win-x86, win-x64, win-arm64 etc
                            ? "x64"
                            : rid.Contains("arm64")
                                ? "arm64"
                                : "x86",
                _ => RuntimeInformation.ProcessArchitecture switch // Fallback is the process
                {
                    Architecture.X64 => "x64",
                    Architecture.X86 => "x86",
                    Architecture.Arm64 => "arm64",
                    _ => throw new Exception("Unsupported target architecture")
                }
            };
        }

        private static bool IsDebug(string config)
        {
            return "Debug".Equals(config);
        }

        private static void SetConfigurationBasedFlags(bool isDebug, ref StringBuilder compiler, ref StringBuilder linker)
        {
            if (isDebug)
            {
                compiler.Append($"/Od /LDd ");
                linker.Append($"");
            }
            else
            {
                compiler.Append($"/O2 /LD ");
                linker.Append($"");
            }
        }

        private static string GetVCToolsRootDir(string vsInstallDir)
        {
            var vcToolsRoot = Path.Combine(vsInstallDir, "VC\\Tools\\MSVC\\");

            var latestToolVersion = new Version();
            string latestPath = null;
            foreach (var dirMaybe in Directory.EnumerateDirectories(vcToolsRoot))
            {
                var verDir = Path.GetFileName(dirMaybe);

                if (!Version.TryParse(verDir, out Version latestMaybe)
                    || latestMaybe < latestToolVersion)
                {
                    continue;
                }

                latestToolVersion = latestMaybe;
                latestPath = dirMaybe;
            }

            return latestPath ?? throw new Exception("Unknown VC Tools version found.");
        }
    }
}
