// Copyright 2026 Aaron R Robinson
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
using System.Collections.Immutable;
using System.Reflection.Metadata;
using System.Text;

namespace DNNE
{
    internal class UnusedGenericContext { }

    internal class NotSupportedTypeException : Exception
    {
        public string Type { get; private set; }
        public NotSupportedTypeException(string type) { this.Type = type; }
    }

    internal abstract class TypeProviderBase : ISignatureTypeProvider<string, UnusedGenericContext>
    {
        private PrimitiveTypeCode? lastUnsupportedPrimitiveType;

        public string GetArrayType(string elementType, ArrayShape shape)
        {
            throw new NotSupportedTypeException(elementType);
        }

        public string GetByReferenceType(string elementType)
        {
            throw new NotSupportedTypeException(elementType);
        }

        public string GetFunctionPointerType(MethodSignature<string> signature)
        {
            string args = this.GetPrimitiveType(PrimitiveTypeCode.Void);
            if (signature.ParameterTypes.Length != 0)
            {
                var argsBuffer = new StringBuilder();
                var delim = "";
                foreach (var type in signature.ParameterTypes)
                {
                    argsBuffer.Append(delim);
                    argsBuffer.Append(type);
                    delim = ", ";
                }

                args = argsBuffer.ToString();
            }

            string callConv = MapCallConv(signature.Header.CallingConvention);
            string typeComment = FormatFunctionPointerComment(signature.ReturnType, callConv, args);
            return typeComment + this.GetPrimitiveType(PrimitiveTypeCode.IntPtr);
        }

        public string GetGenericInstantiation(string genericType, ImmutableArray<string> typeArguments)
        {
            throw new NotSupportedTypeException($"Generic - {genericType}");
        }

        public string GetGenericMethodParameter(UnusedGenericContext genericContext, int index)
        {
            throw new NotSupportedTypeException($"Generic - {index}");
        }

        public string GetGenericTypeParameter(UnusedGenericContext genericContext, int index)
        {
            throw new NotSupportedTypeException($"Generic - {index}");
        }

        public string GetModifiedType(string modifier, string unmodifiedType, bool isRequired)
        {
            throw new NotSupportedTypeException($"{modifier} {unmodifiedType}");
        }

        public string GetPinnedType(string elementType)
        {
            throw new NotSupportedTypeException($"Pinned - {elementType}");
        }

        public string GetPointerType(string elementType)
        {
            this.lastUnsupportedPrimitiveType = null;
            return FormatPointerType(elementType);
        }

        public string GetPrimitiveType(PrimitiveTypeCode typeCode)
        {
            ThrowIfUnsupportedLastPrimitiveType();

            if (typeCode == PrimitiveTypeCode.Char)
            {
                this.lastUnsupportedPrimitiveType = typeCode;
                return GetCharTypeName();
            }

            return MapPrimitiveType(typeCode);
        }

        public void ThrowIfUnsupportedLastPrimitiveType()
        {
            if (this.lastUnsupportedPrimitiveType.HasValue)
            {
                throw new NotSupportedTypeException(this.lastUnsupportedPrimitiveType.Value.ToString());
            }
        }

        public string GetSZArrayType(string elementType)
        {
            throw new NotSupportedTypeException($"Array - {elementType}");
        }

        public string GetTypeFromDefinition(MetadataReader reader, TypeDefinitionHandle handle, byte rawTypeKind)
        {
            return SupportNonPrimitiveTypes(rawTypeKind);
        }

        public string GetTypeFromReference(MetadataReader reader, TypeReferenceHandle handle, byte rawTypeKind)
        {
            return SupportNonPrimitiveTypes(rawTypeKind);
        }

        public string GetTypeFromSpecification(MetadataReader reader, UnusedGenericContext genericContext, TypeSpecificationHandle handle, byte rawTypeKind)
        {
            return SupportNonPrimitiveTypes(rawTypeKind);
        }

        protected abstract string GetCharTypeName();
        protected abstract string MapPrimitiveType(PrimitiveTypeCode typeCode);
        protected abstract string FormatPointerType(string elementType);
        protected abstract string FormatFunctionPointerComment(string returnType, string callConv, string args);

        internal abstract string MapCallConv(SignatureCallingConvention callConv);

        private static string SupportNonPrimitiveTypes(byte rawTypeKind)
        {
            // See https://docs.microsoft.com/dotnet/framework/unmanaged-api/metadata/corelementtype-enumeration
            const byte ELEMENT_TYPE_VALUETYPE = 0x11;
            if (rawTypeKind == ELEMENT_TYPE_VALUETYPE)
            {
                return "/* SUPPLY TYPE */";
            }

            throw new NotSupportedTypeException("Non-primitive");
        }
    }

    internal class C99TypeProvider : TypeProviderBase
    {
        protected override string GetCharTypeName() => "DNNE_WCHAR";

        protected override string FormatPointerType(string elementType) => elementType + "*";

        protected override string FormatFunctionPointerComment(string returnType, string callConv, string args)
        {
            return $"/* {returnType}({callConv} *)({args}) */ ";
        }

        internal override string MapCallConv(SignatureCallingConvention callConv)
        {
            return callConv switch
            {
                SignatureCallingConvention.CDecl => "DNNE_CALLTYPE_CDECL",
                SignatureCallingConvention.StdCall => "DNNE_CALLTYPE_STDCALL",
                SignatureCallingConvention.ThisCall => "DNNE_CALLTYPE_THISCALL",
                SignatureCallingConvention.FastCall => "DNNE_CALLTYPE_FASTCALL",
                SignatureCallingConvention.Unmanaged => "DNNE_CALLTYPE",
                _ => throw new NotSupportedException($"Unknown CallingConvention: {callConv}"),
            };
        }

        protected override string MapPrimitiveType(PrimitiveTypeCode typeCode)
        {
            return typeCode switch
            {
                PrimitiveTypeCode.SByte => "int8_t",
                PrimitiveTypeCode.Byte => "uint8_t",
                PrimitiveTypeCode.Int16 => "int16_t",
                PrimitiveTypeCode.UInt16 => "uint16_t",
                PrimitiveTypeCode.Int32 => "int32_t",
                PrimitiveTypeCode.UInt32 => "uint32_t",
                PrimitiveTypeCode.Int64 => "int64_t",
                PrimitiveTypeCode.UInt64 => "uint64_t",
                PrimitiveTypeCode.IntPtr => "intptr_t",
                PrimitiveTypeCode.UIntPtr => "uintptr_t",
                PrimitiveTypeCode.Single => "float",
                PrimitiveTypeCode.Double => "double",
                PrimitiveTypeCode.Void => "void",
                _ => throw new NotSupportedTypeException(typeCode.ToString())
            };
        }
    }

    internal class RustTypeProvider : TypeProviderBase
    {
        protected override string GetCharTypeName() => "u16";

        protected override string FormatPointerType(string elementType) => "*mut " + elementType;

        protected override string FormatFunctionPointerComment(string returnType, string callConv, string args)
        {
            var retType = returnType == "c_void" ? "()" : returnType;
            return $"/* unsafe {callConv} fn({args}) -> {retType} */ ";
        }

        internal override string MapCallConv(SignatureCallingConvention callConv)
        {
            return callConv switch
            {
                SignatureCallingConvention.CDecl => @"extern ""C""",
                SignatureCallingConvention.StdCall => @"extern ""stdcall""",
                SignatureCallingConvention.ThisCall => @"extern ""thiscall""",
                SignatureCallingConvention.FastCall => @"extern ""fastcall""",
                SignatureCallingConvention.Unmanaged => @"extern ""C""",
                _ => throw new NotSupportedException($"Unknown CallingConvention: {callConv}"),
            };
        }

        protected override string MapPrimitiveType(PrimitiveTypeCode typeCode)
        {
            return typeCode switch
            {
                PrimitiveTypeCode.SByte => "i8",
                PrimitiveTypeCode.Byte => "u8",
                PrimitiveTypeCode.Int16 => "i16",
                PrimitiveTypeCode.UInt16 => "u16",
                PrimitiveTypeCode.Int32 => "i32",
                PrimitiveTypeCode.UInt32 => "u32",
                PrimitiveTypeCode.Int64 => "i64",
                PrimitiveTypeCode.UInt64 => "u64",
                PrimitiveTypeCode.IntPtr => "isize",
                PrimitiveTypeCode.UIntPtr => "usize",
                PrimitiveTypeCode.Single => "f32",
                PrimitiveTypeCode.Double => "f64",
                PrimitiveTypeCode.Void => "c_void",
                _ => throw new NotSupportedTypeException(typeCode.ToString())
            };
        }
    }
}
