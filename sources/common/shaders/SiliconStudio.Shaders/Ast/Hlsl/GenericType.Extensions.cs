﻿// Copyright (c) 2014 Silicon Studio Corp. (http://siliconstudio.co.jp)
// This file is distributed under GPL v3. See LICENSE.md for details.
using SiliconStudio.Shaders.Visitor;

namespace SiliconStudio.Shaders.Ast.Hlsl
{
    public static class GenericTypeExtensions
    {
         
        public static TypeBase MakeGenericInstance(this GenericType genericType, TypeBase genericTemplateType)
        {
            // TODO cache generic instance that are using predefined hlsl types
            var newType = genericTemplateType.DeepClone();

            var genericParameters = ((IGenerics)genericTemplateType).GenericParameters;
            var genericArguments = ((IGenerics)newType).GenericArguments;
            var genericInstanceParameters = genericType.Parameters;

            var genericParameterTypes = new TypeBase[genericParameters.Count];
            var genericBaseParameterTypes = new TypeBase[genericParameters.Count];

            // Look for parameter instance types
            for (int i = 0; i < genericInstanceParameters.Count; i++)
            {
                var genericInstanceParameter = genericInstanceParameters[i];
                if (genericInstanceParameter is TypeBase)
                {
                    var genericInstanceParameterType = (TypeBase)genericInstanceParameter;
                    genericParameterTypes[i] = genericInstanceParameterType;
                    genericBaseParameterTypes[i] = TypeBase.GetBaseType(genericInstanceParameterType);
                    genericParameters[i] = genericParameterTypes[i];
                    genericArguments.Add(genericInstanceParameterType);
                }
            }

            // Replace all references to template arguments to their respective generic instance types
            SearchVisitor.Run(
                newType,
                node =>
                {
                    var typeInferencer = node as ITypeInferencer;
                    if (typeInferencer != null && typeInferencer.TypeInference.Declaration is GenericDeclaration)
                    {
                        var genericDeclaration = (GenericDeclaration)typeInferencer.TypeInference.Declaration;
                        var i = genericDeclaration.Index;
                        var targeType = genericDeclaration.IsUsingBase ? genericBaseParameterTypes[i] : genericParameterTypes[i];

                        if (node is TypeBase)
                            return targeType.ResolveType();
                    }

                    return node;
                });


            return newType;
        }
    }
}