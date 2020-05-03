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

namespace ExportingAssembly
{
    public class RealExports
    {
        public delegate float SingleSingleDelegate(float a);

        [DNNE.Export]
        public static float SingleSingle(float a)
        {
            return a * 3;
        }

        [UnmanagedCallersOnly]
        public static float UnmanagedSingleSingle(float a)
        {
            return SingleSingle(a);
        }

        public delegate float SingleSingleSingleDelegate(float a, float b);

        [DNNE.Export]
        public static float SingleSingleSingle(float a, float b)
        {
            return a * b;
        }

        [UnmanagedCallersOnly]
        public static float UnmanagedSingleSingleSingle(float a, float b)
        {
            return SingleSingleSingle(a, b);
        }

        public delegate float VoidSingleDelegate();

        [DNNE.Export]
        public static float VoidSingle()
        {
            return 27;
        }

        [UnmanagedCallersOnly]
        public static float UnmanagedVoidSingle()
        {
            return VoidSingle();
        }

        public delegate void SingleVoidDelegate(float a);

        [DNNE.Export]
        public static void SingleVoid(float a)
        {
        }

        [UnmanagedCallersOnly]
        public static void UnmanagedSingleVoid(float a)
        {
            SingleVoid(a);
        }

        public delegate double DoubleDoubleDelegate(double a);

        [DNNE.Export]
        public static double DoubleDouble(double a)
        {
            return a * 3;
        }

        [UnmanagedCallersOnly]
        public static double UnmanagedDoubleDouble(double a)
        {
            return DoubleDouble(a);
        }

        public delegate double DoubleDoubleDoubleDelegate(double a, double b);

        [DNNE.Export]
        public static double DoubleDoubleDouble(double a, double b)
        {
            return a * b;
        }

        [UnmanagedCallersOnly]
        public static double UnmanagedDoubleDoubleDouble(double a, double b)
        {
            return DoubleDoubleDouble(a, b);
        }

        public delegate double VoidDoubleDelegate();

        [DNNE.Export]
        public static double VoidDouble()
        {
            return 27;
        }

        [UnmanagedCallersOnly]
        public static double UnmanagedVoidDouble()
        {
            return VoidDouble();
        }

        public delegate void DoubleVoidDelegate(double a);

        [DNNE.Export]
        public static void DoubleVoid(double a)
        {
        }

        [UnmanagedCallersOnly]
        public static void UnmanagedDoubleVoid(double a)
        {
            DoubleVoid(a);
        }
    }
}
