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

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Xml;

namespace DNNE
{
    class GeneratorException : Exception
    {
        public string AssemblyPath { get; private set; }

        public GeneratorException(string assemblyPath, string message)
            : base(message)
        {
            this.AssemblyPath = assemblyPath;
        }
    }

    class Generator : IDisposable
    {
        public enum OutputLanguage
        {
            C99,
            Rust,
        }

        private bool isDisposed = false;

        private readonly ICustomAttributeTypeProvider<KnownType> typeResolver = new TypeResolver();
        private readonly string assemblyPath;
        private readonly PEReader peReader;
        private readonly MetadataReader mdReader;
        private readonly Scope assemblyScope;
        private readonly Scope moduleScope;
        private readonly IDictionary<TypeDefinitionHandle, Scope> typePlatformScenarios = new Dictionary<TypeDefinitionHandle, Scope>();
        private readonly Dictionary<string, string> loadedXmlDocumentation;
        private readonly OutputLanguage language;

        public Generator(string validAssemblyPath, string xmlDocFile, OutputLanguage language)
        {
            this.language = language;
            this.assemblyPath = validAssemblyPath;
            this.peReader = new PEReader(File.OpenRead(this.assemblyPath));
            this.mdReader = this.peReader.GetMetadataReader(MetadataReaderOptions.None);
            this.loadedXmlDocumentation = Generator.LoadXmlDocumentation(xmlDocFile);

            // Check for platform scenario attributes
            AssemblyDefinition asmDef = this.mdReader.GetAssemblyDefinition();
            this.assemblyScope = this.GetOSPlatformScope(asmDef.GetCustomAttributes());

            ModuleDefinition modDef = this.mdReader.GetModuleDefinition();
            this.moduleScope = this.GetOSPlatformScope(modDef.GetCustomAttributes());
        }

        public void Emit(string outputFile)
        {
            using var generatedCode = new StringWriter();
            Emit(generatedCode);

            // Write the generated code to the output file.
            using (var outputFileStream = new StreamWriter(File.Create(outputFile)))
            {
                outputFileStream.Write(generatedCode.ToString());
            }
        }

        public void Emit(TextWriter outputStream)
        {
            var additionalCodeStatements = new List<string>();
            var exportedMethods = new List<ExportedMethod>();
            foreach (var methodDefHandle in this.mdReader.MethodDefinitions)
            {
                MethodDefinition methodDef = this.mdReader.GetMethodDefinition(methodDefHandle);

                // Only check public static functions
                if (!methodDef.Attributes.HasFlag(MethodAttributes.Public | MethodAttributes.Static))
                {
                    continue;
                }

                var supported = new List<OSPlatform>();
                var unsupported = new List<OSPlatform>();
                var callConv = SignatureCallingConvention.Unmanaged;
                var exportAttrType = ExportType.None;
                string managedMethodName = this.mdReader.GetString(methodDef.Name);
                string exportName = managedMethodName;
                // Check for target attribute
                foreach (var customAttrHandle in methodDef.GetCustomAttributes())
                {
                    CustomAttribute customAttr = this.mdReader.GetCustomAttribute(customAttrHandle);
                    var currAttrType = this.GetExportAttributeType(customAttr);
                    if (currAttrType == ExportType.None)
                    {
                        // Check if method has other supported attributes.
                        if (this.TryGetLanguageDeclCodeAttributeValue(customAttr, out string declCode))
                        {
                            additionalCodeStatements.Add(declCode);
                        }
                        else if (this.TryGetOSPlatformAttributeValue(customAttr, out bool isSupported, out OSPlatform scen))
                        {
                            if (isSupported)
                            {
                                supported.Add(scen);
                            }
                            else
                            {
                                unsupported.Add(scen);
                            }
                        }

                        continue;
                    }

                    exportAttrType = currAttrType;
                    if (exportAttrType == ExportType.Export)
                    {
                        CustomAttributeValue<KnownType> data = customAttr.DecodeValue(this.typeResolver);
                        if (data.NamedArguments.Length == 1)
                        {
                            exportName = (string)data.NamedArguments[0].Value;
                        }
                    }
                    else
                    {
                        Debug.Assert(exportAttrType == ExportType.UnmanagedCallersOnly);
                        CustomAttributeValue<KnownType> data = customAttr.DecodeValue(this.typeResolver);
                        foreach (var arg in data.NamedArguments)
                        {
                            switch (arg.Type)
                            {
                                case KnownType.I4:
                                case KnownType.CallingConvention:
                                    callConv = (CallingConvention)arg.Value switch
                                    {
                                        CallingConvention.Winapi => SignatureCallingConvention.Unmanaged,
                                        CallingConvention.Cdecl => SignatureCallingConvention.CDecl,
                                        CallingConvention.StdCall => SignatureCallingConvention.StdCall,
                                        CallingConvention.ThisCall => SignatureCallingConvention.ThisCall,
                                        CallingConvention.FastCall => SignatureCallingConvention.FastCall,
                                        _ => throw new NotSupportedException($"Unknown CallingConvention: {arg.Value}")
                                    };
                                    break;

                                case KnownType.SystemTypeArray:
                                    if (arg.Value != null)
                                    {
                                        foreach (var cct in (ImmutableArray<CustomAttributeTypedArgument<KnownType>>)arg.Value)
                                        {
                                            Debug.Assert(cct.Type == KnownType.SystemType);
                                            switch ((KnownType)cct.Value)
                                            {
                                                case KnownType.CallConvCdecl:
                                                    callConv = SignatureCallingConvention.CDecl;
                                                    break;
                                                case KnownType.CallConvStdcall:
                                                    callConv = SignatureCallingConvention.StdCall;
                                                    break;
                                                case KnownType.CallConvThiscall:
                                                    callConv = SignatureCallingConvention.ThisCall;
                                                    break;
                                                case KnownType.CallConvFastcall:
                                                    callConv = SignatureCallingConvention.FastCall;
                                                    break;
                                            }
                                        }
                                    }
                                    break;

                                case KnownType.String:
                                    exportName = (string)arg.Value;
                                    break;

                                default:
                                    throw new GeneratorException(this.assemblyPath, $"Method '{managedMethodName}' has unknown Attribute value type.");
                            }
                        }
                    }
                }

                // Didn't find target attribute. Move onto next method.
                if (exportAttrType == ExportType.None)
                {
                    continue;
                }

                // Extract method details
                var typeDef = this.mdReader.GetTypeDefinition(methodDef.GetDeclaringType());
                var enclosingTypeName = this.ComputeEnclosingTypeName(typeDef);

                // Process method signature.
                MethodSignature<string> signature;
                try
                {
                    if (this.language == OutputLanguage.Rust)
                    {
                        var typeProvider = new RustTypeProvider();
                        signature = methodDef.DecodeSignature(typeProvider, null);
                        typeProvider.ThrowIfUnsupportedLastPrimitiveType();
                    }
                    else
                    {
                        var typeProvider = new C99TypeProvider();
                        signature = methodDef.DecodeSignature(typeProvider, null);
                        typeProvider.ThrowIfUnsupportedLastPrimitiveType();
                    }
                }
                catch (NotSupportedTypeException nste)
                {
                    throw new GeneratorException(this.assemblyPath, $"Method '{managedMethodName}' has non-exportable type '{nste.Type}'");
                }

                var returnType = signature.ReturnType;
                var argumentTypes = signature.ParameterTypes.ToArray();
                var argumentNames = new string[signature.ParameterTypes.Length];

                // Process each parameter.
                foreach (ParameterHandle paramHandle in methodDef.GetParameters())
                {
                    Parameter param = this.mdReader.GetParameter(paramHandle);

                    // Sequence number starts from 1 for arguments.
                    // Number of 0 indicates return value.
                    // Update arg index to be from [0..n-1]
                    // Return index is -1.
                    const int ReturnIndex = -1;
                    var argIndex = param.SequenceNumber - 1;
                    if (argIndex != ReturnIndex)
                    {
                        Debug.Assert(argIndex >= 0);
                        argumentNames[argIndex] = this.mdReader.GetString(param.Name);
                    }

                    // Check custom attributes for additional code.
                    foreach (var attr in param.GetCustomAttributes())
                    {
                        CustomAttribute custAttr = this.mdReader.GetCustomAttribute(attr);
                        if (TryGetLanguageTypeAttributeValue(custAttr, out string typeOverride))
                        {
                            if (argIndex == ReturnIndex)
                            {
                                returnType = typeOverride;
                            }
                            else
                            {
                                Debug.Assert(argIndex >= 0);
                                argumentTypes[argIndex] = typeOverride;
                            }
                        }
                        else if (TryGetLanguageDeclCodeAttributeValue(custAttr, out string declCode))
                        {
                            additionalCodeStatements.Add(declCode);
                        }
                    }
                }

                var xmlDoc = FindXmlDoc(enclosingTypeName.Replace('+', '.') + Type.Delimiter + managedMethodName, argumentTypes);

                // In Rust mode, skip exports that have non-primitive value types
                // without a type override (indicated by the "/* SUPPLY TYPE */" placeholder).
                if (this.language == OutputLanguage.Rust)
                {
                    bool hasUnsuppliedType = returnType.Contains("/* SUPPLY TYPE */")
                        || argumentTypes.Any(t => t.Contains("/* SUPPLY TYPE */"));
                    if (hasUnsuppliedType)
                    {
                        continue;
                    }
                }

                exportedMethods.Add(new ExportedMethod()
                {
                    Type = exportAttrType,
                    EnclosingTypeName = enclosingTypeName,
                    MethodName = managedMethodName,
                    ExportName = exportName,
                    CallingConvention = callConv,
                    Platforms = new PlatformSupport()
                    {
                        Assembly = this.assemblyScope,
                        Module = this.moduleScope,
                        Type = GetTypeOSPlatformScope(methodDef),
                        Method = new Scope()
                        {
                            Support = supported,
                            NoSupport = unsupported,
                        }
                    },
                    ReturnType = returnType,
                    XmlDoc = xmlDoc,
                    ArgumentTypes = ImmutableArray.Create(argumentTypes),
                    ArgumentNames = ImmutableArray.Create(argumentNames),
                });
            }

            if (exportedMethods.Count == 0)
            {
                throw new GeneratorException(this.assemblyPath, "Nothing to export.");
            }

            string assemblyName = this.mdReader.GetString(this.mdReader.GetAssemblyDefinition().Name);
            if (this.language == OutputLanguage.Rust)
            {
                RustEmitter.Emit(outputStream, assemblyName, exportedMethods, additionalCodeStatements);
            }
            else
            {
                C99Emitter.Emit(outputStream, assemblyName, exportedMethods, additionalCodeStatements);
            }
        }

        private static Dictionary<string, string> LoadXmlDocumentation(string xmlDocumentation)
        {
            var actXml = new Dictionary<string, string>();
            if (xmlDocumentation is null)
                return actXml;

            // See https://docs.microsoft.com/dotnet/csharp/language-reference/xmldoc/
            // for xml documenation definition
            using XmlReader xmlReader = XmlReader.Create(xmlDocumentation);
            while (xmlReader.Read())
            {
                if (xmlReader.NodeType == XmlNodeType.Element && xmlReader.Name == "member")
                {
                    string raw_name = xmlReader["name"];
                    actXml[raw_name] = xmlReader.ReadInnerXml();
                }
            }
            return actXml;
        }

        private string FindXmlDoc(string fullMethodName, string[] argumentTypes)
        {
            string xmlDoc = "";
            foreach (var item in loadedXmlDocumentation)
            {
                if (item.Key.StartsWith("M:" + fullMethodName))
                {
                    xmlDoc = item.Value;
                    break;
                }
            }
            if (xmlDoc == "")
                return "";

            var lines = xmlDoc.TrimStart('\n').TrimEnd().Split("\n");
            string prefix = "/// ";
            var result = lines
             .Select(x => prefix + x.Trim())
             .ToList();

            return Environment.NewLine + string.Join(Environment.NewLine, result);
        }

        public void Dispose()
        {
            if (this.isDisposed)
            {
                return;
            }

            this.peReader.Dispose();

            this.isDisposed = true;
        }

        private ExportType GetExportAttributeType(CustomAttribute attribute)
        {
            if (IsAttributeType(this.mdReader, attribute, "DNNE", "ExportAttribute"))
            {
                return ExportType.Export;
            }
            else if (IsAttributeType(this.mdReader, attribute, "System.Runtime.InteropServices", nameof(UnmanagedCallersOnlyAttribute)))
            {
                return ExportType.UnmanagedCallersOnly;
            }
            else
            {
                return ExportType.None;
            }
        }

        private string ComputeEnclosingTypeName(TypeDefinition typeDef)
        {
            var enclosingTypes = new List<string>() { this.mdReader.GetString(typeDef.Name) };
            TypeDefinition parentTypeDef = typeDef;
            while (parentTypeDef.IsNested)
            {
                parentTypeDef = this.mdReader.GetTypeDefinition(parentTypeDef.GetDeclaringType());
                enclosingTypes.Add(this.mdReader.GetString(parentTypeDef.Name));
            }

            enclosingTypes.Reverse();
            string name = string.Join('+', enclosingTypes);
            if (!parentTypeDef.Namespace.IsNil)
            {
                name = $"{this.mdReader.GetString(parentTypeDef.Namespace)}{Type.Delimiter}{name}";
            }

            return name;
        }

        private bool TryGetLanguageTypeAttributeValue(CustomAttribute attribute, out string typeValue)
        {
            string attrName = this.language == OutputLanguage.Rust ? "RustTypeAttribute" : "C99TypeAttribute";
            typeValue = IsAttributeType(this.mdReader, attribute, "DNNE", attrName)
                ? GetFirstFixedArgAsStringValue(this.typeResolver, attribute)
                : null;
            return !string.IsNullOrEmpty(typeValue);
        }

        private bool TryGetLanguageDeclCodeAttributeValue(CustomAttribute attribute, out string declCode)
        {
            string attrName = this.language == OutputLanguage.Rust ? "RustDeclCodeAttribute" : "C99DeclCodeAttribute";
            declCode = IsAttributeType(this.mdReader, attribute, "DNNE", attrName)
                ? GetFirstFixedArgAsStringValue(this.typeResolver, attribute)
                : null;
            return !string.IsNullOrEmpty(declCode);
        }

        private Scope GetTypeOSPlatformScope(MethodDefinition methodDef)
        {
            TypeDefinitionHandle typeDefHandle = methodDef.GetDeclaringType();
            if (this.typePlatformScenarios.TryGetValue(typeDefHandle, out Scope scope))
            {
                return scope;
            }

            TypeDefinition typeDef = this.mdReader.GetTypeDefinition(typeDefHandle);
            var typeScope = this.GetOSPlatformScope(typeDef.GetCustomAttributes());

            // Record and return the scenarios.
            this.typePlatformScenarios.Add(typeDefHandle, typeScope);
            return typeScope;
        }

        private Scope GetOSPlatformScope(CustomAttributeHandleCollection attrs)
        {
            var supported = new List<OSPlatform>();
            var unsupported = new List<OSPlatform>();
            foreach (var customAttrHandle in attrs)
            {
                CustomAttribute customAttr = this.mdReader.GetCustomAttribute(customAttrHandle);
                if (this.TryGetOSPlatformAttributeValue(customAttr, out bool isSupported, out OSPlatform scen))
                {
                    if (isSupported)
                    {
                        supported.Add(scen);
                    }
                    else
                    {
                        unsupported.Add(scen);
                    }
                }
            }

            return new Scope()
            {
                Support = supported,
                NoSupport = unsupported
            };
        }

        private bool TryGetOSPlatformAttributeValue(
            CustomAttribute attribute,
            out bool support,
            out OSPlatform platform)
        {
            platform = default;

            support = IsAttributeType(this.mdReader, attribute, "System.Runtime.Versioning", nameof(SupportedOSPlatformAttribute));
            if (!support)
            {
                // If the unsupported attribute exists the "support" value is properly set.
                bool nosupport = IsAttributeType(this.mdReader, attribute, "System.Runtime.Versioning", nameof(UnsupportedOSPlatformAttribute));
                if (!nosupport)
                {
                    return false;
                }
            }

            string value = GetFirstFixedArgAsStringValue(this.typeResolver, attribute);
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            const string platformPrefix = "DNNE_";
            if (value.Contains(nameof(OSPlatform.Windows), StringComparison.OrdinalIgnoreCase))
            {
                platform = OSPlatform.Create($"{platformPrefix}{OSPlatform.Windows}");
            }
            else if (value.Contains(nameof(OSPlatform.OSX), StringComparison.OrdinalIgnoreCase))
            {
                platform = OSPlatform.Create($"{platformPrefix}{OSPlatform.OSX}");
            }
            else if (value.Contains(nameof(OSPlatform.Linux), StringComparison.OrdinalIgnoreCase))
            {
                platform = OSPlatform.Create($"{platformPrefix}{OSPlatform.Linux}");
            }
            else if (value.Contains(nameof(OSPlatform.FreeBSD), StringComparison.OrdinalIgnoreCase))
            {
                platform = OSPlatform.Create($"{platformPrefix}{OSPlatform.FreeBSD}");
            }
            else
            {
                platform = OSPlatform.Create(value);
            }

            return true;
        }

        private static string GetFirstFixedArgAsStringValue(ICustomAttributeTypeProvider<KnownType> typeResolver, CustomAttribute attribute)
        {
            CustomAttributeValue<KnownType> data = attribute.DecodeValue(typeResolver);
            if (data.FixedArguments.Length == 1)
            {
                return (string)data.FixedArguments[0].Value;
            }

            return null;
        }

        private static bool IsAttributeType(MetadataReader reader, CustomAttribute attribute, string targetNamespace, string targetName)
        {
            StringHandle namespaceMaybe;
            StringHandle nameMaybe;
            switch (attribute.Constructor.Kind)
            {
                case HandleKind.MemberReference:
                    MemberReference refConstructor = reader.GetMemberReference((MemberReferenceHandle)attribute.Constructor);
                    TypeReference refType = reader.GetTypeReference((TypeReferenceHandle)refConstructor.Parent);
                    namespaceMaybe = refType.Namespace;
                    nameMaybe = refType.Name;
                    break;

                case HandleKind.MethodDefinition:
                    MethodDefinition defConstructor = reader.GetMethodDefinition((MethodDefinitionHandle)attribute.Constructor);
                    TypeDefinition defType = reader.GetTypeDefinition(defConstructor.GetDeclaringType());
                    namespaceMaybe = defType.Namespace;
                    nameMaybe = defType.Name;
                    break;

                default:
                    Debug.Assert(false, "Unknown attribute constructor kind");
                    return false;
            }

#if DEBUG
            string attrNamespace = reader.GetString(namespaceMaybe);
            string attrName = reader.GetString(nameMaybe);
#endif
            return reader.StringComparer.Equals(namespaceMaybe, targetNamespace) && reader.StringComparer.Equals(nameMaybe, targetName);
        }

        private enum KnownType
        {
            Unknown,
            I4,
            CallingConvention,
            CallConvCdecl,
            CallConvStdcall,
            CallConvThiscall,
            CallConvFastcall,
            String,
            SystemTypeArray,
            SystemType
        }

        private class TypeResolver : ICustomAttributeTypeProvider<KnownType>
        {
            public KnownType GetPrimitiveType(PrimitiveTypeCode typeCode)
            {
                return typeCode switch
                {
                    PrimitiveTypeCode.Int32 => KnownType.I4,
                    PrimitiveTypeCode.String => KnownType.String,
                    _ => KnownType.Unknown
                };
            }

            public KnownType GetSystemType()
            {
                return KnownType.SystemType;
            }

            public KnownType GetSZArrayType(KnownType elementType)
            {
                if (elementType == KnownType.SystemType)
                {
                    return KnownType.SystemTypeArray;
                }

                throw new BadImageFormatException("Unexpectedly got an array of unsupported type.");
            }

            public KnownType GetTypeFromDefinition(MetadataReader reader, TypeDefinitionHandle handle, byte rawTypeKind)
            {
                return KnownType.Unknown;
            }

            public KnownType GetTypeFromReference(MetadataReader reader, TypeReferenceHandle handle, byte rawTypeKind)
            {
                return KnownType.Unknown;
            }

            public KnownType GetTypeFromSerializedName(string name)
            {
                int typeAssemblySeparator = name.IndexOf(',');
                string typeName = name[..typeAssemblySeparator];
                string assemblyName = name[(typeAssemblySeparator + 1)..];
                string assemblySimpleName = assemblyName;
                int simpleNameEnd = assemblySimpleName.IndexOf(',');
                if (simpleNameEnd != -1)
                {
                    assemblySimpleName = assemblySimpleName[..simpleNameEnd];
                }

                return (typeName, assemblySimpleName.TrimStart()) switch
                {
                    ("System.Runtime.InteropServices.CallingConvention", "System.Runtime.InteropServices") => KnownType.CallingConvention,
                    ("System.Runtime.CompilerServices.CallConvCdecl", "System.Runtime") => KnownType.CallConvCdecl,
                    ("System.Runtime.CompilerServices.CallConvStdcall", "System.Runtime") => KnownType.CallConvStdcall,
                    ("System.Runtime.CompilerServices.CallConvThiscall", "System.Runtime") => KnownType.CallConvThiscall,
                    ("System.Runtime.CompilerServices.CallConvFastcall", "System.Runtime") => KnownType.CallConvFastcall,
                    _ => KnownType.Unknown
                };
            }

            public PrimitiveTypeCode GetUnderlyingEnumType(KnownType type)
            {
                if (type == KnownType.CallingConvention)
                {
                    return PrimitiveTypeCode.Int32;
                }

                throw new BadImageFormatException("Unexpectedly got an enum parameter for an attribute.");
            }

            public bool IsSystemType(KnownType type)
            {
                return type == KnownType.SystemType;
            }
        }
    }

    internal enum ExportType
    {
        None,
        Export,
        UnmanagedCallersOnly,
    }

    internal struct PlatformSupport
    {
        public Scope Assembly { get; init; }
        public Scope Module { get; init; }
        public Scope Type { get; init; }
        public Scope Method { get; init; }
    }

    internal struct Scope
    {
        public IEnumerable<OSPlatform> Support { get; init; }
        public IEnumerable<OSPlatform> NoSupport { get; init; }
    }

    internal class ExportedMethod
    {
        public ExportType Type { get; init; }
        public string EnclosingTypeName { get; init; }
        public string MethodName { get; init; }
        public string ExportName { get; init; }
        public SignatureCallingConvention CallingConvention { get; init; }
        public PlatformSupport Platforms { get; init; }
        public string ReturnType { get; init; }
        public string XmlDoc { get; init; }
        public ImmutableArray<string> ArgumentTypes { get; init; }
        public ImmutableArray<string> ArgumentNames { get; init; }
    }
}
