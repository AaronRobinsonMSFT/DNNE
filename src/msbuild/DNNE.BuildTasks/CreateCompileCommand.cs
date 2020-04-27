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
using System.Runtime.InteropServices;

namespace DNNE.BuildTasks
{
    public class CreateCompileCommand : Task
    {
        public static MessageImportance DevImportance = MessageImportance.Low;

        [Required]
        public string AssemblyName { get; set; }

        [Required]
        public string NetHostPath { get; set; }

        [Required]
        public string PlatformPath { get; set; }

        [Required]
        public ITaskItem Source { get; set; }

        [Required]
        public string OutputName { get; set; }

        [Required]
        public string OutputPath { get; set; }

        [Required]
        public string Architecture { get; set; }

        [Required]
        public string Configuration { get; set; }

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
    OutputName:     {OutputName}
    OutputPath:     {OutputPath}

Native Build:
    Architecture:   {Architecture}
    Configuration:  {Configuration}
    ");

            string command = string.Empty;
            string commandArguments = string.Empty;
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                Windows.ConstructCommandLine(this, out command, out commandArguments);
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                throw new NotImplementedException("Linux native build");
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                throw new NotImplementedException("OSX native build");
            }
            else
            {
                throw new NotSupportedException("Unknown native build environment");
            }

            this.Command = command;
            this.CommandArguments = commandArguments;

            return true;
        }

        public void Report(MessageImportance import, string msg)
        {
            this.Log.LogMessage(import, msg);
        }
    }
}
