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
using System.Runtime.InteropServices;
using System.Text;

namespace DNNE.BuildTasks
{
    public class Rust
    {
        public static void GenerateCrate(CreateCompileCommand export)
        {
            export.Report(MessageImportance.Low, $"Generating Rust crate for {export.AssemblyName}");

            var crateDir = Path.GetDirectoryName(export.Source);

            // Cargo requires semver (major.minor.patch).
            var version = "0.1.0";
            if (!string.IsNullOrEmpty(export.AssemblyVersion))
            {
                var parts = export.AssemblyVersion.Split('.');
                if (parts.Length >= 3)
                {
                    version = $"{parts[0]}.{parts[1]}.{parts[2]}";
                }
                else
                {
                    version = export.AssemblyVersion;
                }
            }

            // Generate Cargo.toml
            var cargoToml = new StringBuilder();
            cargoToml.AppendLine(@$"[package]
name = ""{export.OutputName.ToLowerInvariant()}""
version = ""{version}""
edition = ""2021""

[lib]
path = ""lib.rs""

[lints.rust]
unexpected_cfgs = ""allow""");
            File.WriteAllText(Path.Combine(crateDir, "Cargo.toml"), cargoToml.ToString());

            // Generate build.rs
            var buildRs = new StringBuilder();
            buildRs.AppendLine(@$"fn main() {{
    println!(""cargo:rustc-link-search={export.NetHostPath.Replace("\\", "/")}"");
    println!(""cargo:rustc-link-lib=static=nethost"");");

            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                buildRs.AppendLine("    println!(\"cargo:rustc-link-lib=c++\");");
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                buildRs.AppendLine("    println!(\"cargo:rustc-link-lib=stdc++\");");
            }

            // Emit user-defined --cfg flags
            if (!string.IsNullOrEmpty(export.UserDefinedCompilerFlags))
            {
                foreach (var flag in export.UserDefinedCompilerFlags.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    if (flag.StartsWith("--cfg"))
                    {
                        continue;
                    }

                    buildRs.AppendLine($"    println!(\"cargo:rustc-cfg={flag}\");");
                }
            }

            buildRs.AppendLine("}");
            File.WriteAllText(Path.Combine(crateDir, "build.rs"), buildRs.ToString());
        }
    }
}
