﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Linq;
using System.Text;
using Mono.Cecil;

namespace nanoFramework.Tools.MetadataProcessor.Core.Extensions
{
    internal static class TypeReferenceExtensions
    {
        internal static readonly List<string> NameSpacesToExclude = new List<string>
        {
            "System.Diagnostics.CodeAnalysis"
        };

        public static bool IsToInclude(this TypeReference value)
        {
            // check list of known attributes to ignore
            bool ignoreFromList = nanoTablesContext.IgnoringAttributes.Contains(value.FullName);

            // is this attribute in the list of namespaces to exclude
            bool ignoreFromCodeAnalysis = NameSpacesToExclude.Any(ns => value.FullName.StartsWith(ns));

            return !(ignoreFromList || ignoreFromCodeAnalysis);
        }

        public static string TypeSignatureAsString(this TypeReference type)
        {
            if (type.MetadataType == MetadataType.IntPtr)
            {
                return "I";
            }

            if (type.MetadataType == MetadataType.UIntPtr)
            {
                return "U";
            }

            nanoCLR_DataType dataType;
            if (nanoSignaturesTable.PrimitiveTypes.TryGetValue(type.FullName, out dataType))
            {
                switch (dataType)
                {
                    case nanoCLR_DataType.DATATYPE_VOID:
                    case nanoCLR_DataType.DATATYPE_BOOLEAN:
                    case nanoCLR_DataType.DATATYPE_CHAR:
                    case nanoCLR_DataType.DATATYPE_I1:
                    case nanoCLR_DataType.DATATYPE_U1:
                    case nanoCLR_DataType.DATATYPE_I2:
                    case nanoCLR_DataType.DATATYPE_U2:
                    case nanoCLR_DataType.DATATYPE_I4:
                    case nanoCLR_DataType.DATATYPE_U4:
                    case nanoCLR_DataType.DATATYPE_I8:
                    case nanoCLR_DataType.DATATYPE_U8:
                    case nanoCLR_DataType.DATATYPE_R4:
                    case nanoCLR_DataType.DATATYPE_BYREF:
                    case nanoCLR_DataType.DATATYPE_OBJECT:
                    case nanoCLR_DataType.DATATYPE_WEAKCLASS:
                        return dataType.ToString().Replace("DATATYPE_", "");

                    case nanoCLR_DataType.DATATYPE_LAST_PRIMITIVE:
                        return "STRING";

                    case nanoCLR_DataType.DATATYPE_LAST_PRIMITIVE_TO_PRESERVE:
                        return "R8";

                    case nanoCLR_DataType.DATATYPE_LAST_PRIMITIVE_TO_MARSHAL:
                        return "TIMESPAN";

                    case nanoCLR_DataType.DATATYPE_REFLECTION:
                        return type.FullName.Replace(".", "");
                }
            }

            if (type.MetadataType == MetadataType.Class)
            {
                StringBuilder classSig = new StringBuilder("CLASS ");
                classSig.Append(type.FullName);
                classSig.Append(" [");
                classSig.Append(type.MetadataToken.ToInt32().ToString("x8"));
                classSig.Append("]");

                return classSig.ToString();
            }

            if (type.MetadataType == MetadataType.ValueType)
            {
                StringBuilder valueTypeSig = new StringBuilder("VALUETYPE ");
                valueTypeSig.Append(type.FullName);
                valueTypeSig.Append(" [");
                valueTypeSig.Append(type.MetadataToken.ToInt32().ToString("x8"));
                valueTypeSig.Append("]");

                return valueTypeSig.ToString();
            }

            if (type.IsArray)
            {
                StringBuilder arraySig = new StringBuilder();
                arraySig.Append(type.GetElementType().TypeSignatureAsString());
                arraySig.Append("[]");

                return arraySig.ToString();
            }

            if (type.IsByReference)
            {
                StringBuilder byrefSig = new StringBuilder("BYREF ");
                byrefSig.Append(type.GetElementType().TypeSignatureAsString());

                return byrefSig.ToString();
            }

            if (type.IsGenericParameter ||
                type.IsGenericInstance)
            {
                return $"!!{type.Name}";
            }

            return "";
        }

        public static string ToNativeTypeAsString(this TypeReference type)
        {
            nanoCLR_DataType dataType;
            if (nanoSignaturesTable.PrimitiveTypes.TryGetValue(type.FullName, out dataType))
            {
                switch (dataType)
                {
                    case nanoCLR_DataType.DATATYPE_VOID:
                        return "void";
                    case nanoCLR_DataType.DATATYPE_BOOLEAN:
                        return "bool";
                    case nanoCLR_DataType.DATATYPE_CHAR:
                        return "char";
                    case nanoCLR_DataType.DATATYPE_I1:
                        return "int8_t";
                    case nanoCLR_DataType.DATATYPE_U1:
                        return "uint8_t";
                    case nanoCLR_DataType.DATATYPE_I2:
                        return "int16_t";
                    case nanoCLR_DataType.DATATYPE_U2:
                        return "uint16_t";
                    case nanoCLR_DataType.DATATYPE_I4:
                        return "signed int";
                    case nanoCLR_DataType.DATATYPE_U4:
                        return "unsigned int";
                    case nanoCLR_DataType.DATATYPE_I8:
                        return "int64_t";
                    case nanoCLR_DataType.DATATYPE_U8:
                        return "uint64_t";
                    case nanoCLR_DataType.DATATYPE_R4:
                        return "float";
                    case nanoCLR_DataType.DATATYPE_BYREF:
                        return "";

                    // system.String
                    case nanoCLR_DataType.DATATYPE_LAST_PRIMITIVE:
                        return "const char*";

                    // System.Double
                    case nanoCLR_DataType.DATATYPE_LAST_PRIMITIVE_TO_PRESERVE:
                        return "double";

                    default:
                        return "UNSUPPORTED";
                }
            }

            if (type.MetadataType == MetadataType.Class)
            {
                return "UNSUPPORTED";
            }

            if (type.MetadataType == MetadataType.ValueType)
            {
                return "UNSUPPORTED";
            }

            if (type.IsArray)
            {
                StringBuilder arraySig = new StringBuilder("CLR_RT_TypedArray_");
                arraySig.Append(type.GetElementType().ToCLRTypeAsString());

                return arraySig.ToString();
            }

            if (type.IsGenericParameter)
            {
                return "UNSUPPORTED";
            }
            return "";
        }

        public static string ToCLRTypeAsString(this TypeReference type)
        {
            nanoCLR_DataType dataType;
            if (nanoSignaturesTable.PrimitiveTypes.TryGetValue(type.FullName, out dataType))
            {
                switch (dataType)
                {
                    case nanoCLR_DataType.DATATYPE_VOID:
                        return "void";
                    case nanoCLR_DataType.DATATYPE_BOOLEAN:
                        return "bool";
                    case nanoCLR_DataType.DATATYPE_CHAR:
                        return "CHAR";
                    case nanoCLR_DataType.DATATYPE_I1:
                        return "INT8";
                    case nanoCLR_DataType.DATATYPE_U1:
                        return "UINT8";
                    case nanoCLR_DataType.DATATYPE_I2:
                        return "INT16";
                    case nanoCLR_DataType.DATATYPE_U2:
                        return "UINT16";
                    case nanoCLR_DataType.DATATYPE_I4:
                        return "INT32";
                    case nanoCLR_DataType.DATATYPE_U4:
                        return "UINT32";
                    case nanoCLR_DataType.DATATYPE_I8:
                        return "INT64";
                    case nanoCLR_DataType.DATATYPE_U8:
                        return "UINT64";
                    case nanoCLR_DataType.DATATYPE_R4:
                        return "float";
                    case nanoCLR_DataType.DATATYPE_BYREF:
                        return "NONE";

                    // system.String
                    case nanoCLR_DataType.DATATYPE_LAST_PRIMITIVE:
                        return "LPCSTR";

                    // System.Double
                    case nanoCLR_DataType.DATATYPE_LAST_PRIMITIVE_TO_PRESERVE:
                        return "double";

                    default:
                        return "UNSUPPORTED";
                }
            }

            if (type.MetadataType == MetadataType.Class)
            {
                return "UNSUPPORTED";
            }

            if (type.MetadataType == MetadataType.ValueType)
            {
                return "UNSUPPORTED";
            }

            if (type.IsArray)
            {
                StringBuilder arraySig = new StringBuilder();
                arraySig.Append(type.GetElementType().ToCLRTypeAsString());
                arraySig.Append("_ARRAY");

                return arraySig.ToString();
            }

            if (type.IsGenericParameter)
            {
                return "UNSUPPORTED";
            }

            return "";
        }

        public static nanoSerializationType ToSerializationType(this TypeReference value)
        {
            nanoCLR_DataType dataType;
            if (nanoSignaturesTable.PrimitiveTypes.TryGetValue(value.FullName, out dataType))
            {
                switch (dataType)
                {
                    case nanoCLR_DataType.DATATYPE_BOOLEAN:
                        return nanoSerializationType.ELEMENT_TYPE_BOOLEAN;
                    case nanoCLR_DataType.DATATYPE_I1:
                        return nanoSerializationType.ELEMENT_TYPE_I1;
                    case nanoCLR_DataType.DATATYPE_U1:
                        return nanoSerializationType.ELEMENT_TYPE_U1;
                    case nanoCLR_DataType.DATATYPE_I2:
                        return nanoSerializationType.ELEMENT_TYPE_I2;
                    case nanoCLR_DataType.DATATYPE_U2:
                        return nanoSerializationType.ELEMENT_TYPE_U2;
                    case nanoCLR_DataType.DATATYPE_I4:
                        return nanoSerializationType.ELEMENT_TYPE_I4;
                    case nanoCLR_DataType.DATATYPE_U4:
                        return nanoSerializationType.ELEMENT_TYPE_U4;
                    case nanoCLR_DataType.DATATYPE_I8:
                        return nanoSerializationType.ELEMENT_TYPE_I8;
                    case nanoCLR_DataType.DATATYPE_U8:
                        return nanoSerializationType.ELEMENT_TYPE_U8;
                    case nanoCLR_DataType.DATATYPE_R4:
                        return nanoSerializationType.ELEMENT_TYPE_R4;
                    case nanoCLR_DataType.DATATYPE_R8:
                        return nanoSerializationType.ELEMENT_TYPE_R8;
                    case nanoCLR_DataType.DATATYPE_CHAR:
                        return nanoSerializationType.ELEMENT_TYPE_CHAR;
                    case nanoCLR_DataType.DATATYPE_STRING:
                        return nanoSerializationType.ELEMENT_TYPE_STRING;
                    default:
                        return 0;
                }
            }

            return 0;
        }

    }
}
