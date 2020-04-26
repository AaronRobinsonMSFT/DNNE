# .NET Native Exports

Prototype supporting native exports for a .NET managed assembly.

This work is inspired by work in the [Xamarian][xamarin_embed_link] and [CoreRT][corert_feature_link] projects.

## Requirements

* [.NET 5.0+](https://dotnet.microsoft.com/).
* [C99](https://en.cppreference.com/w/c/language/history) compatible compiler.

## Exporting details

- The exported function must be marked `static` and `public`.

- The type exporting the function cannot be a nested type.

- A `Delegate` of the appropriate type and name must be at the same scope as the export. The naming convention is `<METHODNAME>Delegate` For example:

    ```CSharp
    public class Exports
    {
        public delegate int MyExportDelegate(int a);

        [DNNE.Export(EntryPoint = "FancyName")]
        public static int MyExport(int a)
        {
            return a;
        }
    }
    ```

- The [`NativeCallableAttribute`](https://github.com/dotnet/runtime/issues/32462) is currently not supported. This is a limitation until [C# functions pointers][csharp_funcptr_link] are implemented. Once this attribute is supported, the above `Delegate` requirement can be lifted for functions marked with `NativeCallableAttribute`.

<a name="nativeapi"></a>

## Native API

The native API is defined in [`src/platform/dnne.h`](./src/platform/dnne.h).

The `DNNE_ASSEMBLY_NAME` must be set during compilation to indicate the name of the managed assembly to load. The assembly name should not include the extension. For example, if the managed assembly on disk is called `ClassLib.dll`, the expected assembly name is `ClassLib`.

The export source will need to be linked against the [`nethost`](https://docs.microsoft.com/dotnet/core/tutorials/netcore-hosting#create-a-host-using-nethosth-and-hostfxrh) library as either a static lib (`libnethost.[lib|a]`) or dynamic/shared library (`nethost.lib`). If the latter linking is performed, the `nethost.[dll|so]` will need to be deployed with the export binary or be on the path at run time.

The `set_failure_callback()` function can be used prior to calling an export to set a callback in the event runtime load or export discovery fails.

Failure to load the runtime or find an export results in the native library calling [`abort()`](https://en.cppreference.com/w/c/program/abort).

## Exporting a managed function

1) Add the following attribute definition to the managed project:

    ``` CSharp
    namespace DNNE
    {
        internal class ExportAttribute : Attribute
        {
            public ExportAttribute() { }
            public string EntryPoint { get; set; }
        }
    }
    ```

2) Adorn the desired managed function with the `DNNE.ExportAttribute`.
    - Optionally set the `DNNE.ExportAttribute.EntryPoint` property to indicate the name of the native export.
    - If the `EntryPoint` property is `null`, the name of the mananged function is used. This default name will not include the namespace or class containing the function.
    - User supplied values in `EntryPoint` will not be modified or validated in any manner. This string will be consume by a C compiler and should therefore adhere to the [C language's restrictions on function names](https://en.cppreference.com/w/c/language/functions).

3) Set the `<EnableDynamicLoading>true</EnableDynamicLoading>` property in the managed project containing the methods to export. This will produce a `*.runtimeconfig.json` that is needed to activate the runtime during export dispatch.

### Generate using NuGet package

**Note** Currently limited to Windows only

4) Include the DNNE NuGet package in the managed project. The NuGet is currently not on a feed, so a local path must be set as a NuGet source after the package is built.

    ```xml
    <ItemGroup>
      <PackageReference Include="DNNE" Version="1.0.0" />
    </ItemGroup>
    ```

5) The DNNE NuGet package can be built locally by running `pack` on [`dnne-gen.csproj`](./src/dnne-gen/dnne-gen.csproj).

    `> dotnet pack dnne-gen.csproj`

6) Build the project from a Visual Studio Developer command prompt for the desired bitness. The generated native binary will be in the output directory with the managed binary. The native binary will a `NE` suffix and the system extension for dynamic/shared native libraries (i.e. `.dll`, `.so`, `.dylib`).

7) Deploy the native binary, managed assembly and associated `*.json` files for consumption from a native process.

### Generate manually

4) Run the [dnne-gen](./src/dnne-gen) tool on the managed assembly.

5) Take the generated source from `dnne-gen` and the DNNE [platform](./src/platform) source to compile a native binary with the desired native exports. See the [Native API](#nativeapi) section for build details.

6) Deploy the native binary, managed assembly and associated `*.json` files for consumption from a native process.

# Additional References

[dotnet repo](https://github.com/dotnet/runtime)

[`nethost` example](https://github.com/dotnet/samples/tree/master/core/hosting/HostWithHostFxr)

<!-- Links -->
[xamarin_embed_link]: https://docs.microsoft.com/xamarin/tools/dotnet-embedding/release-notes/preview/0.4
[corert_feature_link]: https://github.com/dotnet/corert/tree/master/samples/NativeLibrary
[csharp_funcptr_link]: https://github.com/dotnet/csharplang/blob/master/proposals/function-pointers.md