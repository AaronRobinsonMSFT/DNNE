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
using Microsoft.VisualStudio.Setup.Configuration;
using Microsoft.Win32;

using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace DNNE.BuildTasks
{
    public class Windows
    {
        private static readonly Lazy<string> g_VsInstallPath = new Lazy<string>(GetLatestVSWithVCInstallPath, true);
        private static readonly Lazy<WinSDK> g_WinSdk = new Lazy<WinSDK>(GetLatestWinSDK, true);

        public static void ConstructCommandLine(CreateCompileCommand export, out string command, out string commandArguments)
        {
            export.Report(MessageImportance.Low, $"Building for Windows");

            WinSDK winSdk = g_WinSdk.Value;
            string vsInstall = g_VsInstallPath.Value;
            string vcToolDir = GetVCToolsRootDir(vsInstall);
            export.Report(CreateCompileCommand.DevImportance, $"VS Install: {vsInstall}\nVC Tools: {vcToolDir}\nWinSDK Version: {winSdk.Version}");

            bool isDebug = IsDebug(export.Configuration);

            string archDir = ConvertToVCArchSubDir(export.Architecture, export.RuntimeID);

            // VC inc and lib paths
            var vcIncDir = Path.Combine(vcToolDir, "include");
            var libDir = Path.Combine(vcToolDir, "lib", archDir);

            // For now we assume building always happens on a x64 machine.
            var binDir = Path.Combine(vcToolDir, "bin\\Hostx64", archDir);

            // Create arguments
            var compilerFlags = new StringBuilder();
            var linkerFlags = new StringBuilder();
            SetConfigurationBasedFlags(isDebug, ref compilerFlags, ref linkerFlags);

            // Set compiler flags
            compilerFlags.Append($"/TC /MT /GS /Zi ");
            compilerFlags.Append($"/D DNNE_ASSEMBLY_NAME={export.AssemblyName} /D DNNE_COMPILE_AS_SOURCE ");
            compilerFlags.Append($"/I \"{vcIncDir}\" /I \"{export.PlatformPath}\" /I \"{export.NetHostPath}\" ");

            // Add WinSDK inc paths
            foreach (var incPath in winSdk.IncPaths)
            {
                compilerFlags.Append($"/I \"{incPath}\" ");
            }

            // Add user defined inc paths last - these will be searched last on MSVC.
            // https://docs.microsoft.com/cpp/build/reference/i-additional-include-directories#remarks
            foreach (var incPath in export.SafeAdditionalIncludeDirectories)
            {
                compilerFlags.Append($"/I \"{incPath.ItemSpec}\" ");
            }

            compilerFlags.Append($"\"{export.Source}\" \"{Path.Combine(export.PlatformPath, "platform.c")}\" ");

            // Set linker flags
            linkerFlags.Append($"/DLL /LTCG ");
            linkerFlags.Append($"/LIBPATH:\"{libDir}\" ");

            // Add WinSDK lib paths
            foreach (var libPath in winSdk.LibPaths)
            {
                linkerFlags.Append($"/LIBPATH:\"{Path.Combine(libPath, archDir)}\" ");
            }

            linkerFlags.Append($"\"{Path.Combine(export.NetHostPath, "libnethost.lib")}\" Advapi32.lib ");
            linkerFlags.Append($"/IGNORE:4099 "); // libnethost.lib doesn't ship PDBs so linker warnings occur.

            // Define artifact names
            var outputPath = Path.Combine(export.OutputPath, export.OutputName);
            var impLibPath = Path.ChangeExtension(outputPath, ".lib");
            linkerFlags.Append($"/IMPLIB:\"{impLibPath}\" /OUT:\"{outputPath}\" ");

            command = Path.Combine(binDir, "cl.exe");
            commandArguments = $"{compilerFlags} /link {linkerFlags}";
        }

        private static string ConvertToVCArchSubDir(string arch, string rid)
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

        private static string GetLatestVSWithVCInstallPath()
        {
            var setupConfig = new SetupConfiguration();
            IEnumSetupInstances enumInst = setupConfig.EnumInstances();

            var latestVersion = new Version();
            ISetupInstance latestVsInstance = null;
            // Find latest install with VC tools
            while (true)
            {
                var el = new ISetupInstance[1];
                enumInst.Next(1, el, out int ret);
                if (ret != 1)
                {
                    break;
                }

                var vsInst = (ISetupInstance2)el[0];
                var ver = new Version(vsInst.GetInstallationVersion());

                ISetupPackageReference[] pkgs = vsInst.GetPackages();
                foreach (var n in pkgs)
                {
                    var pkgId = n.GetId();
                    if (pkgId.Equals("Microsoft.VisualStudio.Component.VC.Tools.x86.x64")
                        || pkgId.Equals("Microsoft.VisualStudio.Component.VC.Tools.ARM64"))
                    {
                        if (latestVersion < ver)
                        {
                            latestVersion = ver;
                            latestVsInstance = vsInst;
                        }
                        break;
                    }
                }
            }

            if (latestVsInstance is null)
            {
                throw new Exception("Visual Studio with VC Tools package (x86, x64, or ARM64) must be installed.");
            }

            return latestVsInstance.GetInstallationPath();
        }

        private class WinSDK
        {
            public string Version;
            public IEnumerable<string> IncPaths;
            public IEnumerable<string> LibPaths;
        }

        private class VersionDescendingOrder : IComparer<Version>
        {
            public int Compare(Version x, Version y)
            {
                return y.CompareTo(x);
            }
        }

        private static WinSDK GetLatestWinSDK()
        {
            using (var kits = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows Kits\Installed Roots"))
            {
                string win10sdkRoot = (string)kits.GetValue("KitsRoot10");

                // Sort the entries in descending order as
                // to defer to the latest version.
                var versions = new SortedList<Version, string>(new VersionDescendingOrder());

                // Collect the possible SDK versions.
                foreach (var verMaybe in kits.GetSubKeyNames())
                {
                    if (!Version.TryParse(verMaybe, out Version versionMaybe))
                    {
                        continue;
                    }

                    versions.Add(versionMaybe, verMaybe);
                }

                // Find the latest version of the SDK.
                foreach (var tgtVerMaybe in versions)
                {
                    // WinSDK inc and lib paths
                    var incDir = Path.Combine(win10sdkRoot, "Include", tgtVerMaybe.Value);
                    var libDir = Path.Combine(win10sdkRoot, "Lib", tgtVerMaybe.Value);
                    if (!Directory.Exists(incDir) || !Directory.Exists(libDir))
                    {
                        continue;
                    }

                    var sharedIncDir = Path.Combine(incDir, "shared");
                    var umIncDir = Path.Combine(incDir, "um");
                    var ucrtIncDir = Path.Combine(incDir, "ucrt");
                    var umLibDir = Path.Combine(libDir, "um");
                    var ucrtLibDir = Path.Combine(libDir, "ucrt");

                    return new WinSDK()
                    {
                        Version = tgtVerMaybe.Value,
                        IncPaths = new[] { sharedIncDir, umIncDir, ucrtIncDir },
                        LibPaths = new[] { umLibDir, ucrtLibDir },
                    };
                }
            }

            throw new Exception("No Win10 SDK version found.");
        }
    }
}
