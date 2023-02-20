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

#include "dnne.h"

// Must define the assembly name
#ifndef DNNE_ASSEMBLY_NAME
    #error Target assembly name must be defined. Set 'DNNE_ASSEMBLY_NAME'.
#endif

#if !defined(DNNE_WINDOWS) || !defined(_MSC_VER) || !defined(__cplusplus)
    #error .NET Framework v4.x support requires Windows and MSVC C++.
#endif

#include <cassert>

#define NOMINMAX
#include <Windows.h>
#include <mscoree.h>
#include <metahost.h>

// Modified copy of ICLRPrivRuntime definition
MIDL_INTERFACE("BC1B53A8-DCBC-43B2-BB17-1E4061447AE9")
ICLRPrivRuntime : public IUnknown
{
public:
    virtual HRESULT STDMETHODCALLTYPE Reserved1() = 0;
    virtual HRESULT STDMETHODCALLTYPE Reserved2() = 0;
    virtual HRESULT STDMETHODCALLTYPE CreateDelegate(
        /* [in] */ DWORD appDomainID,
        /* [in] */ LPCWSTR wszAssemblyName,
        /* [in] */ LPCWSTR wszClassName,
        /* [in] */ LPCWSTR wszMethodName,
        /* [out] */ LPVOID* fnPtr) = 0;
};

namespace
{
    failure_fn failure_fptr;
}

DNNE_EXTERN_C DNNE_API void DNNE_CALLTYPE set_failure_callback(failure_fn cb)
{
    failure_fptr = cb;
}

// Provide mechanism for users to override default behavior of certain functions.
//  - See https://stackoverflow.com/questions/51656838/attribute-weak-and-static-libraries
//  - See https://devblogs.microsoft.com/oldnewthing/20200731-00/?p=104024
#define DNNE_DEFAULT_IMPL(methodName, ...) default_ ## methodName(__VA_ARGS__)

// List of overridable APIs
// Note the default calling convention is cdecl on x86 for DNNE_APIs.
// This means, on x86, we mangle the alternative name in the linker command.
#ifdef _M_IX86
    #pragma comment(linker, "/alternatename:_dnne_abort=_default_dnne_abort")
#else
    #pragma comment(linker, "/alternatename:dnne_abort=default_dnne_abort")
#endif

DNNE_EXTERN_C DNNE_API void DNNE_DEFAULT_IMPL(dnne_abort, enum failure_type type, int error_code)
{
    abort();
}

#define DNNE_TOSTRING2(s) #s
#define DNNE_TOSTRING(s) DNNE_TOSTRING2(s)
#define DNNE_NORETURN __declspec(noreturn)

namespace
{
    DNNE_NORETURN void noreturn_failure(enum failure_type type, int error_code)
    {
        if (failure_fptr != nullptr)
            failure_fptr(type, error_code);

        // Nothing to do if the runtime failed to load.
        dnne_abort(type, error_code);

        // Don't trust anything the user can override.
        abort();
    }

    using dnne_lock_handle = volatile long;
    #define DNNE_LOCK_OPEN (0)

    void enter_lock(dnne_lock_handle* lock)
    {
        while (InterlockedCompareExchange(lock, -1, DNNE_LOCK_OPEN) != DNNE_LOCK_OPEN)
        {
            Sleep(1 /* milliseconds */);
        }
    }

    void exit_lock(dnne_lock_handle* lock)
    {
        InterlockedExchange(lock, DNNE_LOCK_OPEN);
    }

    dnne_lock_handle _prepare_lock = DNNE_LOCK_OPEN;

    ICLRPrivRuntime* _host;
    DWORD _appDomainId;

    #define IF_FAILURE_RETURN_OR_ABORT(ret_maybe, type, rc, lock) \
    { \
        if (FAILED(rc)) \
        { \
            exit_lock(lock); \
            if (ret_maybe) \
            { \
                *ret_maybe = rc; \
                return; \
            } \
            noreturn_failure(type, rc); \
        } \
    }

    void prepare_runtime(int* ret)
    {
        HRESULT hr = S_OK;

        // Lock and check if the needed export was already acquired.
        enter_lock(&_prepare_lock);
        if (_host == nullptr)
        {
            HRESULT hr;
            ICLRMetaHost* metahost;
            hr = CLRCreateInstance(CLSID_CLRMetaHost, IID_ICLRMetaHost, (void**)&metahost);
            IF_FAILURE_RETURN_OR_ABORT(ret, failure_load_runtime, hr, &_prepare_lock);

            ICLRRuntimeInfo* runtimeInfo;
            hr = metahost->GetRuntime(DNNE_STR("v4.0.30319"), IID_ICLRRuntimeInfo, (void**)&runtimeInfo);
            IF_FAILURE_RETURN_OR_ABORT(ret, failure_load_runtime, hr, &_prepare_lock);

            ICLRRuntimeHost* runtimeHost;
            hr = runtimeInfo->GetInterface(CLSID_CLRRuntimeHost, IID_ICLRRuntimeHost, (void**)&runtimeHost);
            IF_FAILURE_RETURN_OR_ABORT(ret, failure_load_runtime, hr, &_prepare_lock);

            // Release all other CLR resources
            (void)metahost->Release();
            (void)runtimeInfo->Release();

            // Start the runtime
            hr = runtimeHost->Start();
            IF_FAILURE_RETURN_OR_ABORT(ret, failure_load_runtime, hr, &_prepare_lock);

            (void)runtimeHost->GetCurrentAppDomainId(&_appDomainId);
            (void)runtimeHost->QueryInterface(__uuidof(ICLRPrivRuntime), (void**)&_host);
            (void)runtimeHost->Release();
            assert(_host != nullptr);
        }
        exit_lock(&_prepare_lock);

        if (ret != nullptr && FAILED(hr))
            *ret = hr;
    }
}

DNNE_EXTERN_C DNNE_API void DNNE_CALLTYPE preload_runtime(void)
{
    prepare_runtime(nullptr);
}

DNNE_EXTERN_C DNNE_API int DNNE_CALLTYPE try_preload_runtime(void)
{
    int ret = DNNE_SUCCESS;
    prepare_runtime(&ret);
    return ret;
}

void* get_callable_managed_function(
    const WCHAR* dotnet_type,
    const WCHAR* dotnet_type_method,
    const WCHAR* /* Not used - dotnet_delegate_type */)
{
    assert(dotnet_type && dotnet_type_method);

    // Store the current error state to reset it when
    // we exit this function. This being done because this
    // API is an implementation detail of the export but
    // can result in side-effects during export resolution.
    DWORD curr_error = GetLastError();

    // Check if the runtime has already been prepared.
    if (_host == nullptr)
    {
        prepare_runtime(nullptr);
        assert(_host != nullptr);
    }

    // Function pointer to managed function
    void* func = nullptr;
    HRESULT hr = _host->CreateDelegate(
        _appDomainId,
        DNNE_STR(DNNE_TOSTRING(DNNE_ASSEMBLY_NAME)),
        dotnet_type,
        dotnet_type_method,
        &func);

    if (FAILED(hr))
        noreturn_failure(failure_load_export, hr);

    // Now that the export has been resolved, reset
    // the error state to hide this implementation detail.
    SetLastError(curr_error);
    return func;
}

void* get_fast_callable_managed_function(
    const WCHAR* dotnet_type,
    const WCHAR* dotnet_type_method)
{
    noreturn_failure(failure_load_export, E_NOTIMPL);
}
