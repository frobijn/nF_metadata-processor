﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Collections.Generic;
using Mustache;
using nanoFramework.Tools.MetadataProcessor.Core.Extensions;

namespace nanoFramework.Tools.MetadataProcessor.Core
{
    /// <summary>
    /// Dumps details for parsed assemblies and PE files.
    /// </summary>
    public sealed class nanoDumperGenerator
    {
        private readonly nanoTablesContext _tablesContext;
        private readonly string _path;

        public nanoDumperGenerator(
                    nanoTablesContext tablesContext,
                    string path)
        {
            _tablesContext = tablesContext;
            _path = path;
        }

        public void DumpAll()
        {
            var dumpTable = new DumpAllTable();

            DumpAssemblyReferences(dumpTable);
            //DumpModuleReferences(dumpTable);
            DumpTypeReferences(dumpTable);

            DumpTypeDefinitions(dumpTable);
            DumpCustomAttributes(dumpTable);
            DumpUserStrings(dumpTable);


            FormatCompiler compiler = new FormatCompiler();
            Generator generator = compiler.Compile(DumpTemplates.DumpAllTemplate);

            using (var dumpFile = File.CreateText(_path))
            {
                var output = generator.Render(dumpTable);
                dumpFile.Write(output);
            }
        }

        private void DumpCustomAttributes(DumpAllTable dumpTable)
        {
            foreach (var a in _tablesContext.TypeDefinitionTable.Items.Where(td => td.HasCustomAttributes))
            {
                foreach (var ma in a.Methods)
                {
                    if (ma.HasCustomAttributes)
                    {
                        var attribute = new AttributeCustom()
                        {
                            Name = a.Module.Assembly.Name.Name,
                            ReferenceId = ma.MetadataToken.ToInt32().ToString("x8"),
                            TypeToken = ma.CustomAttributes[0].Constructor.MetadataToken.ToInt32().ToString("x8")
                        };

                        if (ma.CustomAttributes[0].HasConstructorArguments)
                        {
                            foreach (var value in ma.CustomAttributes[0].ConstructorArguments)
                            {
                                attribute.FixedArgs.AddRange(BuildFixedArgsAttribute(value));
                            }
                        }

                        dumpTable.Attributes.Add(attribute);
                    }
                }

                foreach (var fa in a.Fields)
                {
                    if (fa.HasCustomAttributes)
                    {
                        var attribute = new AttributeCustom()
                        {
                            Name = a.Module.Assembly.Name.Name,
                            ReferenceId = fa.MetadataToken.ToInt32().ToString("x8"),
                            TypeToken = fa.CustomAttributes[0].Constructor.MetadataToken.ToInt32().ToString("x8")
                        };

                        if (!nanoTablesContext.IgnoringAttributes.Contains(fa.CustomAttributes[0].AttributeType.FullName)
                            && fa.CustomAttributes[0].HasConstructorArguments)
                        {
                            foreach (CustomAttributeArgument value in fa.CustomAttributes[0].ConstructorArguments)
                            {
                                attribute.FixedArgs.AddRange(BuildFixedArgsAttribute(value));
                            }
                        }

                        dumpTable.Attributes.Add(attribute);
                    }
                }
            }
        }

        private List<AttFixedArgs> BuildFixedArgsAttribute(CustomAttributeArgument value)
        {
            if (value.Type.IsArray && value.Type.GetElementType().FullName == "System.Object")
            {
                var attArgs = new List<AttFixedArgs>();

                foreach (var attributeArgument in (CustomAttributeArgument[])value.Value)
                {
                    attArgs.AddRange(BuildFixedArgsAttribute((CustomAttributeArgument)attributeArgument.Value));
                }

                return attArgs;
            }

            var serializationType = value.Type.ToSerializationType();

            var newArg = new AttFixedArgs()
            {
                Options = ((byte)serializationType).ToString("X2"),
                Text = "",
            };

            switch (serializationType)
            {
                case nanoSerializationType.ELEMENT_TYPE_BOOLEAN:
                    newArg.Numeric = ((bool)value.Value) ? 1.ToString("X16") : 0.ToString("X16");
                    break;

                case nanoSerializationType.ELEMENT_TYPE_STRING:
                    newArg.Text = (string)value.Value;
                    break;

                case nanoSerializationType.ELEMENT_TYPE_OBJECT:
                    newArg.Text = (string)value.Value;
                    break;

                case nanoSerializationType.ELEMENT_TYPE_I1:
                    newArg.Numeric = ((sbyte)value.Value).ToString("X16");
                    break;

                case nanoSerializationType.ELEMENT_TYPE_I2:
                    newArg.Numeric = ((short)value.Value).ToString("X16");
                    break;

                case nanoSerializationType.ELEMENT_TYPE_I4:
                    newArg.Numeric = ((int)value.Value).ToString("X16");
                    break;

                case nanoSerializationType.ELEMENT_TYPE_I8:
                    newArg.Numeric = ((long)value.Value).ToString("X16");
                    break;

                case nanoSerializationType.ELEMENT_TYPE_U1:
                    newArg.Numeric = ((byte)value.Value).ToString("X16");
                    break;

                case nanoSerializationType.ELEMENT_TYPE_U2:
                    newArg.Numeric = ((ushort)value.Value).ToString("X16");
                    break;

                case nanoSerializationType.ELEMENT_TYPE_U4:
                    newArg.Numeric = ((uint)value.Value).ToString("X16");
                    break;

                case nanoSerializationType.ELEMENT_TYPE_U8:
                    newArg.Numeric = ((ulong)value.Value).ToString("X16");
                    break;

                default:
                    newArg.Text = value.Value.ToString();
                    break;
            }

            return new List<AttFixedArgs>() { newArg };
        }

        private void DumpUserStrings(DumpAllTable dumpTable)
        {
            // start at 1, because 0 is the empty string entry
            int tokenId = 1;

            foreach (var s in _tablesContext.StringTable.GetItems().OrderBy(i => i.Value).Where(i => i.Value > _tablesContext.StringTable.LastPreAllocatedId))
            {
                // don't output the empty string
                if (s.Value == 0)
                {
                    continue;
                }

                // fake the metadata token from the ID
                var stringMetadataToken = new MetadataToken(TokenType.String, tokenId++);

                dumpTable.UserStrings.Add(
                    new UserString()
                    {
                        ReferenceId = stringMetadataToken.ToInt32().ToString("x8"),
                        Content = s.Key
                    });
            }
        }

        private void DumpTypeDefinitions(DumpAllTable dumpTable)
        {
            foreach (TypeDefinition t in _tablesContext.TypeDefinitionTable.Items.OrderBy(tr => tr.MetadataToken.ToInt32()))
            {
                // fill type definition
                var typeDef = new TypeDef()
                {
                    ReferenceId = t.MetadataToken.ToInt32().ToString("x8"),
                };

                if (t.IsNested)
                {
                    typeDef.Name = t.Name;
                }
                else
                {
                    typeDef.Name = t.FullName;
                }

                uint typeFlags = (uint)nanoTypeDefinitionTable.GetFlags(
                    t,
                    _tablesContext.MethodDefinitionTable);

                typeDef.Flags = typeFlags.ToString("x8");

                if (t.BaseType != null)
                {
                    typeDef.ExtendsType = t.BaseType.MetadataToken.ToInt32().ToString("x8");
                }
                else
                {
                    var token = new MetadataToken(TokenType.TypeRef, 0);
                    typeDef.ExtendsType = token.ToInt32().ToString("x8");
                }

                if (t.DeclaringType != null)
                {
                    typeDef.EnclosedType = t.DeclaringType.MetadataToken.ToInt32().ToString("x8");
                }
                else
                {
                    var token = new MetadataToken(TokenType.TypeDef, 0);
                    typeDef.EnclosedType = token.ToInt32().ToString("x8");
                }

                // list generic parameters
                foreach (GenericParameter gp in t.GenericParameters)
                {
                    var genericParam = new GenericParam()
                    {
                        Position = gp.Position.ToString(),
                        GenericParamToken = gp.MetadataToken.ToInt32().ToString("x8"),
                        Name = gp.FullName,
                        Owner = gp.Owner.MetadataToken.ToInt32().ToString("x8"),
                        Signature = gp.DeclaringType.Name
                    };

                    typeDef.GenericParameters.Add(genericParam);
                }

                // list type fields
                foreach (FieldDefinition f in t.Fields)
                {
                    uint att = (uint)f.Attributes;

                    var fieldDef = new FieldDef()
                    {
                        ReferenceId = f.MetadataToken.ToInt32().ToString("x8"),
                        Name = f.Name,
                        Flags = att.ToString("x8"),
                        Attributes = att.ToString("x8"),
                        Signature = f.FieldType.TypeSignatureAsString()
                    };

                    typeDef.FieldDefinitions.Add(fieldDef);
                }

                // list type methods
                foreach (MethodDefinition m in t.Methods)
                {
                    var methodDef = new MethodDef()
                    {
                        ReferenceId = m.MetadataToken.ToInt32().ToString("x8"),
                        Name = m.FullName(),
                        RVA = m.RVA.ToString("x8"),
                        Implementation = "00000000",
                        Signature = PrintSignatureForMethod(m)
                    };

                    uint methodFlags = nanoMethodDefinitionTable.GetFlags(m);
                    methodDef.Flags = methodFlags.ToString("x8");

                    if (m.HasBody)
                    {
                        // locals
                        if (m.Body.HasVariables)
                        {
                            methodDef.Locals = PrintSignatureForLocalVar(m.Body.Variables);
                        }

                        // exceptions
                        foreach (Mono.Cecil.Cil.ExceptionHandler eh in m.Body.ExceptionHandlers)
                        {
                            var h = new ExceptionHandler();

                            h.Handler = $"{((int)eh.HandlerType).ToString("x2")} " +
                                $"{eh.TryStart?.Offset.ToString("x8")}->{eh.TryEnd?.Offset.ToString("x8")} " +
                                $"{eh.HandlerStart?.Offset.ToString("x8")}->{eh.HandlerEnd?.Offset.ToString("x8")} ";

                            if (eh.CatchType != null)
                            {
                                h.Handler += $"{eh.CatchType.MetadataToken.ToInt32().ToString("x8")}";
                            }
                            else
                            {
                                h.Handler += "00000000";
                            }

                            methodDef.ExceptionHandlers.Add(h);
                        }

                        methodDef.ILCodeInstructionsCount = m.Body.Instructions.Count.ToString();

                        // IL code
                        foreach (Instruction instruction in m.Body.Instructions)
                        {
                            ILCode ilCode = new ILCode();

                            ilCode.IL += instruction.OpCode.Name.PadRight(12);

                            if (instruction.Operand != null)
                            {
                                if (instruction.OpCode.OperandType == OperandType.InlineTok ||
                                    instruction.OpCode.OperandType == OperandType.InlineSig)
                                {
                                    ilCode.IL += $"[{((IMetadataTokenProvider)instruction.Operand).MetadataToken.ToInt32():x8}]";
                                }
                                else if (instruction.OpCode.OperandType == OperandType.InlineField)
                                {
                                    // output the field type name
                                    ilCode.IL += $"{((FieldReference)instruction.Operand).FieldType.FullName}";

                                    // output the token
                                    ilCode.IL += $" [{((IMetadataTokenProvider)instruction.Operand).MetadataToken.ToInt32():x8}]";
                                }
                                else if (instruction.OpCode.OperandType == Mono.Cecil.Cil.OperandType.InlineMethod)
                                {
                                    // output the method name
                                    ilCode.IL += $"{((MethodReference)instruction.Operand).FullName}";

                                    // output the token
                                    ilCode.IL += $" [{((IMetadataTokenProvider)instruction.Operand).MetadataToken.ToInt32():x8}]";
                                }
                                else if (instruction.OpCode.OperandType == OperandType.InlineType)
                                {
                                    // Mono.Cecil.ArrayType
                                    if (instruction.Operand is ArrayType arrayType)
                                    {
                                        // output the type name
                                        ilCode.IL += $"{arrayType.ElementType.FullName}[]";
                                    }
                                    else
                                    {
                                        // output the type name
                                        ilCode.IL += $"{((TypeReference)instruction.Operand).FullName}";
                                    }

                                    // output the token
                                    ilCode.IL += $" [{((IMetadataTokenProvider)instruction.Operand).MetadataToken.ToInt32():x8}]";
                                }
                                else if (instruction.OpCode.OperandType == OperandType.InlineString)
                                {
                                    // strings need a different processing
                                    // get string ID from table
                                    ushort stringReferenceId = _tablesContext.StringTable.GetOrCreateStringId((string)instruction.Operand, true);

                                    // fake the metadata token from the ID
                                    MetadataToken stringMetadataToken = new MetadataToken(TokenType.String, stringReferenceId);

                                    // output the string
                                    ilCode.IL += $"\"{(string)instruction.Operand}\"";

                                    // ouput the metadata token
                                    ilCode.IL += $" [{stringMetadataToken.ToInt32():x8}]";
                                }

                            }

                            methodDef.ILCode.Add(ilCode);
                        }
                    }

                    typeDef.MethodDefinitions.Add(methodDef);
                }

                // list interface implementations
                foreach (InterfaceImplementation i in t.Interfaces)
                {
                    typeDef.InterfaceDefinitions.Add(
                        new InterfaceDef()
                        {
                            ReferenceId = i.MetadataToken.ToInt32().ToString("x8"),
                            Interface = i.InterfaceType.MetadataToken.ToInt32().ToString("x8")
                        });
                }

                dumpTable.TypeDefinitions.Add(typeDef);
            }
        }

        private void DumpTypeReferences(DumpAllTable dumpTable)
        {
            foreach (var t in _tablesContext.TypeReferencesTable.Items.OrderBy(tr => tr.MetadataToken.ToInt32()))
            {
                ushort refId;

                var typeRef = new TypeRef()
                {
                    Name = t.FullName,
                    // need to add 1 to match the index on the old MDP
                    Scope = new MetadataToken(TokenType.AssemblyRef, _tablesContext.TypeReferencesTable.GetScope(t) + 1).ToInt32().ToString("x8")
                };

                if (_tablesContext.TypeReferencesTable.TryGetTypeReferenceId(t, out refId))
                {
                    typeRef.ReferenceId = t.MetadataToken.ToInt32().ToString("x8");
                }

                // list member refs               
                foreach (var m in _tablesContext.MethodReferencesTable.Items.Where(mr => mr.DeclaringType == t))
                {
                    var memberRef = new MemberRef()
                    {
                        Name = m.Name
                    };

                    if (_tablesContext.MethodReferencesTable.TryGetMethodReferenceId(m, out refId))
                    {
                        memberRef.ReferenceId = m.MetadataToken.ToInt32().ToString("x8");
                        memberRef.Signature = PrintSignatureForMethod(m);
                    }

                    typeRef.MemberReferences.Add(memberRef);
                }

                dumpTable.TypeReferences.Add(typeRef);
            }
        }

        private void DumpModuleReferences(DumpAllTable dumpTable)
        {
            throw new NotImplementedException();
        }

        private void DumpAssemblyReferences(DumpAllTable dumpTable)
        {
            foreach (var a in _tablesContext.AssemblyReferenceTable.Items)
            {
                dumpTable.AssemblyReferences.Add(new AssemblyRef()
                {
                    Name = a.Name,
                    // need to add 1 to match the index on the old MDP
                    ReferenceId = new MetadataToken(TokenType.AssemblyRef, _tablesContext.AssemblyReferenceTable.GetReferenceId(a) + 1).ToInt32().ToString("x8"),
                    Flags = "00000000"
                });
            }
        }

        private string PrintSignatureForMethod(MethodReference method)
        {
            var sig = new StringBuilder(method.ReturnType.TypeSignatureAsString());

            sig.Append('(');

            foreach (ParameterDefinition param in method.Parameters)
            {
                sig.Append(param.ParameterType.TypeSignatureAsString());
                sig.Append(", ");
            }

            // remove trailing", "
            if (method.Parameters.Count > 0)
            {
                sig.Remove(sig.Length - 2, 2);
            }
            else
            {
                sig.Append(' ');
            }

            sig.Append(')');

            return sig.ToString();
        }

        private string PrintSignatureForLocalVar(Collection<VariableDefinition> variables)
        {
            const string localIdentation = "                ";

            StringBuilder sig = new StringBuilder();

            sig.Append('(');

            for (int localIndex = 0; localIndex < variables.Count; localIndex++)
            {
                sig.Append($"{(localIndex > 0 ? localIdentation : "")} [{localIndex}] ");
                sig.Append(variables[localIndex].VariableType.TypeSignatureAsString());

                if (localIndex < variables.Count - 1)
                {
                    sig.AppendLine(", ");
                }
            }

            sig.Append(" )");

            return sig.ToString();
        }
    }
}
