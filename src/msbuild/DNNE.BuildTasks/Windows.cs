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
using System.IO;
using System.Text;

namespace DNNE.BuildTasks
{
    public class Windows
    {
        private static Lazy<string> g_VsInstallPath = new Lazy<string>(GetLatestVSWithVCInstallPath, true);
        private static Lazy<WinSDK> g_WinSdk = new Lazy<WinSDK>(GetLatestWinSDK, true);

        public static void ConstructCommandLine(CreateCompileCommand export, out string command, out string commandArguments)
        {
            export.Report(MessageImportance.Low, $"Building for Windows");

            WinSDK winSdkDir = g_WinSdk.Value;
            string vsInstall = g_VsInstallPath.Value;
            string vcToolDir = GetVCToolsRootDir(vsInstall);
            export.Report(CreateCompileCommand.DevImportance, $"VS Install: {vsInstall}\nVC Tools: {vcToolDir}");

            bool isDebug = IsDebug(export.Configuration);
            bool is64Bit = Is64BitTarget(export.Architecture);

            var archDir = is64Bit ? "x64" : "x86";

            // WinSDK inc and lib paths
            var winIncDir = Path.Combine(winSdkDir.Root, "Include", winSdkDir.Version);
            var sharedIncDir = Path.Combine(winIncDir, "shared");
            var umIncDir = Path.Combine(winIncDir, "um");
            var ucrtIncDir = Path.Combine(winIncDir, "ucrt");
            var umLibDir = Path.Combine(winSdkDir.Root, "Lib", winSdkDir.Version, "um", archDir);
            var ucrtLibDir = Path.Combine(winSdkDir.Root, "Lib", winSdkDir.Version, "ucrt", archDir);

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
            compilerFlags.Append($"/TC /GS /Zi ");
            compilerFlags.Append($"/D DNNE_ASSEMBLY_NAME={export.AssemblyName} ");
            compilerFlags.Append($"/I \"{vcIncDir}\" /I \"{export.PlatformPath}\" /I \"{export.NetHostPath}\" ");
            compilerFlags.Append($"/I \"{sharedIncDir}\" /I \"{umIncDir}\" /I \"{ucrtIncDir}\" ");
            compilerFlags.Append($"\"{export.Source}\" \"{Path.Combine(export.PlatformPath, "platform.c")}\" ");

            // Set linker flags
            linkerFlags.Append($"/DLL ");
            linkerFlags.Append($"/LIBPATH:\"{libDir}\" ");
            linkerFlags.Append($"/LIBPATH:\"{umLibDir}\" /LIBPATH:\"{ucrtLibDir}\" ");
            linkerFlags.Append($"\"{Path.Combine(export.NetHostPath, "nethost.lib")}\" ");
            //linkerFlags.Append($"\"{Path.Combine(export.NetHostPath, "libnethost.lib")}\" ");
            linkerFlags.Append($"/out:\"{Path.Combine(export.OutputPath, export.OutputName)}\" ");

            command = Path.Combine(binDir, "cl.exe");
            commandArguments = $"{compilerFlags} /link {linkerFlags}";
        }

        public static bool Is64BitTarget(string arch)
        {
            return arch switch
            {
                "AnyCPU" => IntPtr.Size == 8,
                "x64" => true,
                _ => false,
            };
        }

        public static bool IsDebug(string config)
        {
            return "Debug".Equals(config);
        }

        public static void SetConfigurationBasedFlags(bool isDebug, ref StringBuilder compiler, ref StringBuilder linker)
        {
            if (isDebug)
            {
                compiler.Append($"/Od /MTd /LDd ");
                linker.Append($"");
            }
            else
            {
                compiler.Append($"/O2 /MT /LD ");
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

                Version latestMaybe;
                if (!Version.TryParse(verDir, out latestMaybe)
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
                int ret;
                var el = new ISetupInstance[1];
                enumInst.Next(1, el, out ret);
                if (ret != 1)
                {
                    break;
                }

                var vsInst = (ISetupInstance2)el[0];
                ISetupPackageReference[] pkgs = vsInst.GetPackages();
                foreach (var n in pkgs)
                {
                    var ver = new Version(vsInst.GetInstallationVersion());
                    if (n.GetId().Equals("Microsoft.VisualStudio.Component.VC.Tools.x86.x64"))
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
                throw new Exception("Visual Studio with VC Tools package must be installed.");
            }

            return latestVsInstance.GetInstallationPath();
        }

        private class WinSDK
        {
            public string Root;
            public string Version;
        }

        private static WinSDK GetLatestWinSDK()
        {
            string win10sdkRoot;
            string latestSdkVersionStr = null;
            var latestSdkVersion = new Version();
            using (var kits = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows Kits\Installed Roots"))
            {
                win10sdkRoot = (string)kits.GetValue("KitsRoot10");

                // Find the latest version
                foreach (var verMaybe in kits.GetSubKeyNames())
                {
                    Version latestMaybe;
                    if (!Version.TryParse(verMaybe, out latestMaybe)
                        || latestMaybe < latestSdkVersion)
                    {
                        continue;
                    }

                    latestSdkVersion = latestMaybe;
                    latestSdkVersionStr = verMaybe;
                }
            }

            if (string.IsNullOrEmpty(latestSdkVersionStr) || string.IsNullOrEmpty(win10sdkRoot))
            {
                throw new Exception("Unknown Win10 SDK version found.");
            }

            return new WinSDK() { Root = win10sdkRoot, Version = latestSdkVersionStr };
        }
    }
}
