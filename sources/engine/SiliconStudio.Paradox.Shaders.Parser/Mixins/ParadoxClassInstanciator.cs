﻿// Copyright (c) 2014 Silicon Studio Corp. (http://siliconstudio.co.jp)
// This file is distributed under GPL v3. See LICENSE.md for details.
using System;
using System.Collections.Generic;
using System.Linq;

using SiliconStudio.Paradox.Shaders.Parser.Ast;
using SiliconStudio.Paradox.Shaders.Parser.Utility;
using SiliconStudio.Shaders.Ast;
using SiliconStudio.Shaders.Ast.Hlsl;
using SiliconStudio.Shaders.Utility;
using SiliconStudio.Shaders.Visitor;

using StorageQualifier = SiliconStudio.Shaders.Ast.StorageQualifier;

namespace SiliconStudio.Paradox.Shaders.Parser.Mixins
{
    internal class ParadoxClassInstanciator : ShaderVisitor
    {
        private ShaderClassType shaderClassType;

        private LoggerResult logger;

        private Dictionary<string, Variable> variableGenerics;

        private Dictionary<string, Expression> expressionGenerics;
        
        private Dictionary<string, Identifier> identifiersGenerics;

        private Dictionary<string, string> stringGenerics;

        private ParadoxClassInstanciator(ShaderClassType classType, Dictionary<string, Expression> expressions, Dictionary<string, Identifier> identifiers, LoggerResult log)
            : base(false, false)
        {
            shaderClassType = classType;
            expressionGenerics = expressions;
            identifiersGenerics = identifiers;
            logger = log;
            variableGenerics = shaderClassType.ShaderGenerics.ToDictionary(x => x.Name.Text, x => x);
        }

        public static void Instanciate(ShaderClassType classType, Dictionary<string, Expression> expressions, Dictionary<string, Identifier> identifiers, LoggerResult log)
        {
            var instanciator = new ParadoxClassInstanciator(classType, expressions, identifiers, log);
            instanciator.Run();
        }

        private void Run()
        {
            stringGenerics = identifiersGenerics.ToDictionary(x => x.Key, x => x.Value.ToString());

            foreach (var baseClass in shaderClassType.BaseClasses)
                VisitDynamic(baseClass); // look for IdentifierGeneric

            foreach (var member in shaderClassType.Members)
                VisitDynamic(member); // look for IdentifierGeneric and Variable

            foreach (var variable in shaderClassType.ShaderGenerics)
            {
                variable.InitialValue = expressionGenerics[variable.Name.Text];

                if (variable.Type is SemanticType || variable.Type is LinkType)
                    continue;
                
                // TODO: be more precise

                if (!(variable.InitialValue is VariableReferenceExpression || variable.InitialValue is MemberReferenceExpression))
                {
                    variable.Qualifiers |= StorageQualifier.Const;
                    variable.Qualifiers |= SiliconStudio.Shaders.Ast.Hlsl.StorageQualifier.Static;
                }
                shaderClassType.Members.Add(variable);
            }
        }

        [Visit]
        protected void Visit(Variable variable)
        {
            Visit((Node)variable);
            //TODO: check types

            // no call on base
            foreach (var sem in variable.Qualifiers.Values.OfType<Semantic>())
            {
                string replacementSemantic;
                if (stringGenerics.TryGetValue(sem.Name, out replacementSemantic))
                {
                    if (logger != null && !(variableGenerics[sem.Name].Type is SemanticType))
                        logger.Warning(ParadoxMessageCode.WarningUseSemanticType, variable.Span, variableGenerics[sem.Name]);
                    sem.Name = replacementSemantic;
                }
            }

            foreach (var annotation in variable.Attributes.OfType<AttributeDeclaration>().Where(x => x.Name == "Link" && x.Parameters.Count > 0))
            {
                var linkName = (string)annotation.Parameters[0].Value;

                if (String.IsNullOrEmpty(linkName))
                    continue;

                var replacements = new List<Tuple<string, int>>();

                foreach (var generic in variableGenerics.Where(x => x.Value.Type is LinkType))
                {
                    var index = linkName.IndexOf(generic.Key, 0);
                    if (index >= 0)
                        replacements.Add(Tuple.Create(generic.Key, index));
                }

                if (replacements.Count > 0)
                {
                    var finalString = "";
                    var currentIndex = 0;
                    foreach (var replacement in replacements.OrderBy(x => x.Item2))
                    {
                        var replacementIndex = replacement.Item2;
                        var stringToReplace = replacement.Item1;

                        if (replacementIndex - currentIndex > 0)
                            finalString += linkName.Substring(currentIndex, replacementIndex - currentIndex);
                        finalString += stringGenerics[stringToReplace];
                        currentIndex = replacementIndex + stringToReplace.Length;
                    }

                    if (currentIndex < linkName.Length)
                        finalString += linkName.Substring(currentIndex);

                    annotation.Parameters[0] = new Literal(finalString);
                }
            }
        }

        [Visit]
        protected void Visit(IdentifierGeneric identifierGeneric)
        {
            Visit((Node)identifierGeneric);

            for (var i = 0; i < identifierGeneric.Identifiers.Count; ++i)
            {
                Identifier replacement;
                if (identifiersGenerics.TryGetValue(identifierGeneric.Identifiers[i].ToString(), out replacement))
                    identifierGeneric.Identifiers[i] = replacement;
            }
        }
    }
}
