// Copyright 2023 Aaron R Robinson
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

//
// This example demonstrates how to compile and consume a DNNE generated binary.
// There are two broad categories.
//   1) Load the generated native binary (for example, SampleNE.[dll|so|dylib]) via LoadLibrary()/dlopen()
//      and then lookup the export using GetProcAddress()/dlsym().
//      See ./test/ImportingProcess for an example using CMake.
//
//   2) Include generated header file (for example, SampleNE.h) and link against the export lib on Windows
//      (for example, SampleNE.lib) or the .so/dylib on Linux/macOS.
//      For example:
//          Windows: cl.exe -I ..\bin\Debug\netX.0 main.c /link ..\bin\Debug\netX.0\SampleNE.lib /out:main.exe
//          Linux/macOS: clang -I ../bin/Debug/netX.0 main.c -o main ../bin/Debug/netX.0/SampleNE.dylib
//      The above commands will result in a compiled binary. The managed assembly and generated native binary
//      will need to be copied locally in order to run the scenario.
//

#include <stdlib.h>
#include <stdio.h>
#include <SampleNE.h>

int main(int ac, char** av)
{
    printf("Calling managed export\n");
    int a = FancyName(ac);
    printf("Called managed with argument count: %d\n", a);
    return EXIT_SUCCESS;
}
