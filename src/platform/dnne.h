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

#ifndef __SRC_PLATFORM_DNNE_H__
#define __SRC_PLATFORM_DNNE_H__

// Define our platform
#ifdef _WIN32
    #define DNNE_WINDOWS
#elif defined(__APPLE__)
    #define DNNE_OSX
#elif defined(__FreeBSD__)
    #define DNNE_FREEBSD
#else
    #define DNNE_LINUX
#endif

// Define some platform macros
#ifdef DNNE_WINDOWS
    #define DNNE_API __declspec(dllexport)
    #define DNNE_CALLTYPE __stdcall
    #define DNNE_CALLTYPE_CDECL __cdecl
    #define DNNE_CALLTYPE_STDCALL __stdcall
    #define DNNE_CALLTYPE_THISCALL __thiscall
    #define DNNE_CALLTYPE_FASTCALL __fastcall
    #define _DNNE_STR(s1) L ## s1
    #define DNNE_STR(s) _DNNE_STR(s)
    #define DNNE_WCHAR wchar_t
#else
    #define DNNE_API __attribute__((__visibility__("default")))
    #define DNNE_CALLTYPE
    #define DNNE_CALLTYPE_CDECL
    #ifdef __i386__
        #define DNNE_CALLTYPE_STDCALL __attribute__((stdcall))
        #define DNNE_CALLTYPE_THISCALL __attribute__((thiscall))
        #define DNNE_CALLTYPE_FASTCALL __attribute__((fastcall))
    #else
        #define DNNE_CALLTYPE_STDCALL
        #define DNNE_CALLTYPE_THISCALL
        #define DNNE_CALLTYPE_FASTCALL
    #endif
    #define DNNE_STR(s) s
    #define DNNE_WCHAR uint16_t
#endif

// Override the DNNE_API macro.
// This is typically used to dictate the export semantics of functions.
#ifdef DNNE_API_OVERRIDE
    #undef DNNE_API
    #define DNNE_API DNNE_API_OVERRIDE
#endif

//
// Public exports
//
#define DNNE_SUCCESS 0

enum failure_type
{
    failure_load_runtime = 1,
    failure_load_export,
};
typedef void (DNNE_CALLTYPE* failure_fn)(enum failure_type type, int error_code);

#ifdef __cplusplus
    #define DNNE_EXTERN_C extern "C"
    DNNE_EXTERN_C
    {
#else
    #define DNNE_EXTERN_C
#endif

// Provide a callback for any catastrophic failures.
// The provided callback will be the last call prior to a rude-abort of the process.
// See dnne_abort().
DNNE_API void DNNE_CALLTYPE set_failure_callback(failure_fn cb);

// Preload the runtime.
// The runtime is lazily loaded whenever the first export is called. This function
// preloads the runtime independent of calling any export and avoids the startup
// cost associated with calling an export for the first time.
// If the runtime fails to load, dnne_abort() will be called.
DNNE_API void DNNE_CALLTYPE preload_runtime(void);

// Attempt to preload the runtime.
// The runtime is lazily loaded whenever the first export is called. This function
// preloads the runtime independent of calling any export and avoids the startup
// cost associated with calling an export for the first time.
// If the runtime fails to load, an error code will be returned.
DNNE_API int DNNE_CALLTYPE try_preload_runtime(void);

// Users can override DNNE's rude-abort behavior by providing their own dnne_abort() at link time.
// It is expected this function will not return. If it does return, the behavior is undefined.
extern DNNE_API void dnne_abort(enum failure_type type, int error_code);

#ifdef __cplusplus
    }
#endif

#endif // __SRC_PLATFORM_DNNE_H__
