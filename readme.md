# .NET Native Exports

Prototype supporting native exports for a .NET managed assembly.

This work is inspired by work in the [Xamarian][xamarin_embed_link] and [CoreRT][corert_feature_link] projects.

## Requirements

* [.NET 5.0+](https://dotnet.microsoft.com/).
* [C99](https://en.cppreference.com/w/c/language/history) compatible compiler.

## API

See [`src/platform/nexport.h`](./src/platform/nexport.h) for complete details.

The `NEXPORT_ASSEMBLY_NAME` must be set during compilation to indicate the name of the managed assembly to load. The assembly name should not include the extension. For example, if the binary on disk is called `ClassLib.dll`, the expected assembly name is `ClassLib`.

The `set_failure_callback()` function can be used prior to calling an export to set a callback in the event runtime load fails.

Failure to load the runtime or find an export results in the native library calling [`abort()`](https://en.cppreference.com/w/c/program/abort).

## Exporting a managed function (manual)

1) Adorn the desired managed function with [`NativeCallableAttribute`](https://github.com/dotnet/runtime/issues/32462).
    - Optionally set the `NativeCallableAttribute.EntryPoint` property to indicate the name of the native export.
    - If the `NativeCallableAttribute.EntryPoint` property is `null`, the name of the mananged function is used. This default name will not include the namespace or class containing the function.
    - All "`.`"s in the name will be converted to "`_`".

1) Set the `<EnableDynamicLoading>true</EnableDynamicLoading>` property in the managed project containing the methods to export. This will produce a `*.runtimeconfig.json` that is needed to activate the runtime during export dispatch.

1) Run the [dnne-gen](./src/dnne-gen) tool on the managed assembly.

1) Take the generated source from `dnne-gen` and the dnne [platform](./src/platform) source to compile a native binary with the desired native exports.

1) Deploy the native binary, managed assembly and associated `*.json` files for consumption from a native process.

# References

[dotnet repo](https://github.com/dotnet/runtime)

[`nethost` example](https://github.com/dotnet/samples/tree/master/core/hosting/HostWithHostFxr)

<!-- Links -->
[xamarin_embed_link]: https://docs.microsoft.com/xamarin/tools/dotnet-embedding/release-notes/preview/0.4
[corert_feature_link]: https://github.com/dotnet/corert/tree/master/samples/NativeLibrary