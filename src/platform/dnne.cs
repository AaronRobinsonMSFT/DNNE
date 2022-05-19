// Copyright 2022 Aaron R Robinson, Martin Wetzko
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

namespace DNNE
{
    internal class ExportAttribute : Attribute
    {
        public ExportAttribute() { }
        public string EntryPoint { get; set; }
    }

    internal class C99TypeAttribute : Attribute
    {
        public C99TypeAttribute(string code) { }
    }

    internal class C99DeclCodeAttribute : Attribute
    {
        public C99DeclCodeAttribute(string code) { }
    }

    /// <summary>
    /// Win32 'BOOL' equivalent
    /// </summary>
    public struct WinBool
    {
        int b;

        public static implicit operator bool(WinBool b)
        {
            return b.b != 0;
        }

        public static implicit operator WinBool(bool b)
        {
            return new WinBool { b = b ? 1 : 0 };
        }
    }

    /// <summary>
    /// C99 'bool' equivalent
    /// </summary>
    public struct CBool
    {
        byte b;

        public static implicit operator bool(CBool b)
        {
            return b.b != 0;
        }

        public static implicit operator CBool(bool b)
        {
            return new CBool { b = b ? (byte)0x1 : (byte)0x0 };
        }
    }
}