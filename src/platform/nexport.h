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

#ifndef __NEXPORT_H__
#define __NEXPORT_H__

// Must define the assembly name
#ifndef NEXPORT_ASSEMBLY_NAME
    #error Target assembly name must be defined. Set 'NEXPORT_ASSEMBLY_NAME'.
#endif

// Check if we are on Windows
#ifdef _WIN32
    #define NE_WINDOWS
#endif

// Define some platform macros
#ifdef NE_WINDOWS
    #define NE_API __declspec(dllexport)
    #define NE_CALLTYPE __stdcall
    #define _NE_STR(s1) L ## s1
    #define NE_STR(s) _NE_STR(s)
#else
    #define NE_API __attribute__((__visibility__("default")))
    #define NE_CALLTYPE 
    #define NE_STR(s) s
#endif

// Include the official nethost API
#define NETHOST_USE_AS_STATIC
#include <nethost.h>

//
// Public exports
//
enum failure_type
{
    failure_load_runtime = 1,
    failure_load_export,
};
typedef void (NE_CALLTYPE* failure_fn)(enum failure_type type, int error_code);

// Provide a callback for any catastrophic failures.
// The provided callback will be the last call prior to a rude-abort of the process.
NE_API void NE_CALLTYPE set_failure_callback(failure_fn cb);

#endif // __NEXPORT_H__
