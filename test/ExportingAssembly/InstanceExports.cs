// Copyright 2021 Aaron R Robinson
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

namespace ExportingAssembly
{
    using System;
    using System.Runtime.InteropServices;

    /// <summary>
    /// Projection of C-style exports for manipulating a Type instance.
    /// </summary>
    /// <remarks>
    /// This code could be auto generated using tooling. The tooling
    /// would also generate marshalling of arguments and return values.
    ///
    /// The C++ code to consume this could also be auto generated. The
    /// below example illustrates one possible way to wrap the exports
    /// to create a natural C++ experience.
    /// <code>
    /// #ifndef _MYCLASS_HPP_
    /// #define _MYCLASS_HPP_
    /// class MyClass
    /// {
    ///     intptr_t _inst;
    /// 
    /// public:
    ///     MyClass()
    ///     {
    ///         _inst = MyClass_ctor();
    ///     }
    ///     ~MyClass()
    ///     {
    ///         MyClass_dtor(_inst);
    ///     }
    ///     int32_t get_Number()
    ///     {
    ///         return MyClass_getNumber(_inst);
    ///     }
    ///     void set_Number(int32_t num)
    ///     {
    ///         MyClass_setNumber(_inst, num);
    ///     }
    ///     void printNumber()
    ///     {
    ///         MyClass_printNumber(_inst);
    ///     }
    ///     // Delete copy functions since a copy export isn't defined.
    ///     MyClass(const MyClass&) = delete;
    ///     MyClass& operator=(const MyClass&) = delete;
    /// };
    /// #endif // _MYCLASS_HPP_
    /// </code>
    /// </remarks>
    public class InstanceExports
    {
        [UnmanagedCallersOnly(EntryPoint = "MyClass_ctor")]
        public static IntPtr CreateMyClass()
        {
            var obj = new MyClass();
            return GCHandle.ToIntPtr(GCHandle.Alloc(obj));
        }

        [UnmanagedCallersOnly(EntryPoint = "MyClass_dtor")]
        public static void ReleaseMyClass(IntPtr inst)
        {
            GCHandle.FromIntPtr(inst).Free();
        }

        [UnmanagedCallersOnly(EntryPoint = "MyClass_getNumber")]
        public static int MyClassGetNumber(IntPtr inst)
        {
            return As<MyClass>(inst).Number;
        }

        [UnmanagedCallersOnly(EntryPoint = "MyClass_setNumber")]
        public static void MyClassSetNumber(IntPtr inst, int number)
        {
            As<MyClass>(inst).Number = number;
        }

        [UnmanagedCallersOnly(EntryPoint = "MyClass_printNumber")]
        public static void MyClassPrintNumber(IntPtr inst)
        {
            As<MyClass>(inst).PrintNumber();
        }

        private static T As<T>(IntPtr ptr) where T : class
        {
            return (T)GCHandle.FromIntPtr(ptr).Target;
        }
    }

    /// <summary>
    /// Type exposed to native code.
    /// </summary>
    public class MyClass
    {
        private int number;

        public MyClass()
        {
            this.number = 0;
        }

        public int Number { get; set; }

        public void PrintNumber() => Console.WriteLine(this.number);
    }
}
