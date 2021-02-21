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
using Microsoft.Build.Utilities;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

namespace DNNE.BuildTasks
{
    public class CreateCompileCommand : Task
    {
#if DEBUG
        public static MessageImportance DevImportance = MessageImportance.High;
#else
        public static MessageImportance DevImportance = MessageImportance.Low;
#endif

        [Required]
        public string AssemblyName { get; set; }

        [Required]
        public string NetHostPath { get; set; }

        [Required]
        public string PlatformPath { get; set; }

        [Required]
        public string Source { get; set; }

        [Required]
        public string OutputName { get; set; }

        [Required]
        public string OutputPath { get; set; }

        [Required]
        public string RuntimeID { get; set; }

        [Required]
        public string Architecture { get; set; }

        [Required]
        public string Configuration { get; set; }

        // Optional
        public string CommandOverride { get; set; }

        // Optional
        public string ExportsDefFile { get; set; }

        // Used to ensure the supplied path is absolute and
        // can be supplied as-is in a command line scenario.
        internal string AbsoluteExportsDefFilePath
        {
            get
            {
                if (string.IsNullOrEmpty(ExportsDefFile))
                {
                    return null;
                }

                return Path.GetFullPath(ExportsDefFile);
            }
        }

        // Optional
        public ITaskItem[] AdditionalIncludeDirectories { get; set; }

        // Used to avoid null cases
        // N.B. The ToString() method on an ITaskItem returns the escaped
        // item spec. This is typically not what is desired if being passed
        // to a command line tool. In order to get the unescaped item spec use
        // the ITaskItem.ItemSpec property.
        internal IEnumerable<ITaskItem> SafeAdditionalIncludeDirectories
        {
            get => AdditionalIncludeDirectories ?? Enumerable.Empty<ITaskItem>();
        }

        [Output]
        public string Command { get; set; }

        [Output]
        public string CommandArguments { get; set; }

        public override bool Execute()
        {
            Log.LogMessage(DevImportance,
$@"
DNNE Native Compilation:
    AssemblyName:   {AssemblyName}
    NetHostPath:    {NetHostPath}
    PlatformPath:   {PlatformPath}
    Source:         {Source}
    ExportsDefFile: {ExportsDefFile}
    OutputName:     {OutputName}
    OutputPath:     {OutputPath}

Native Build:
    RuntimeID:      {RuntimeID}
    Architecture:   {Architecture}
    Configuration:  {Configuration}
    ");

            string command;
            string commandArguments;
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                Windows.ConstructCommandLine(this, out command, out commandArguments);
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                Linux.ConstructCommandLine(this, out command, out commandArguments);
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                macOS.ConstructCommandLine(this, out command, out commandArguments);
            }
            else
            {
                throw new NotSupportedException("Unknown native build environment");
            }

            this.Command = string.IsNullOrEmpty(this.CommandOverride) ? command : this.CommandOverride;
            this.CommandArguments = commandArguments;

            return true;
        }

        public void Report(MessageImportance import, string msg)
        {
            this.Log.LogMessage(import, msg);
        }
    }
}
