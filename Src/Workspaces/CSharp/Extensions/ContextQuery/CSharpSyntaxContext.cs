﻿// Copyright (c) Microsoft Open Technologies, Inc.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Threading;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.CSharp.Utilities;
using Microsoft.CodeAnalysis.Shared.Extensions;
using Microsoft.CodeAnalysis.Shared.Extensions.ContextQuery;

namespace Microsoft.CodeAnalysis.CSharp.Extensions.ContextQuery
{
    internal sealed class CSharpSyntaxContext : AbstractSyntaxContext
    {
        public readonly TypeDeclarationSyntax ContainingTypeDeclaration;
        public readonly BaseTypeDeclarationSyntax ContainingTypeOrEnumDeclaration;

        public readonly bool IsInNonUserCode;

        public readonly bool IsPreProcessorKeywordContext;
        public readonly bool IsPreProcessorExpressionContext;

        public readonly bool IsGlobalStatementContext;

        public readonly bool IsNonAttributeExpressionContext;
        public readonly bool IsConstantExpressionContext;

        public readonly bool IsLabelContext;
        public readonly bool IsTypeArgumentOfConstraintContext;

        public readonly bool IsNamespaceDeclarationNameContext;
        public readonly bool IsIsOrAsContext;
        public readonly bool IsObjectCreationTypeContext;
        public readonly bool IsDefiniteCastTypeContext;
        public readonly bool IsGenericTypeArgumentContext;
        public readonly bool IsEnumBaseListContext;
        public readonly bool IsIsOrAsTypeContext;
        public readonly bool IsLocalVariableDeclarationContext;
        public readonly bool IsFixedVariableDeclarationContext;
        public readonly bool IsParameterTypeContext;
        public readonly bool IsPossibleLambdaOrAnonymousMethodParameterTypeContext;
        public readonly bool IsImplicitOrExplicitOperatorTypeContext;
        public readonly bool IsPrimaryFunctionExpressionContext;
        public readonly bool IsDelegateReturnTypeContext;
        public readonly bool IsTypeOfExpressionContext;
        public readonly ISet<SyntaxKind> PrecedingModifiers;
        public readonly bool IsInstanceContext;
        public readonly bool IsCrefContext;
        public readonly bool IsCatchFilterContext;
        public readonly bool IsDestructorTypeContext;

        private CSharpSyntaxContext(
            Workspace workspace,
            SemanticModel semanticModel,
            int position,
            SyntaxToken leftToken,
            SyntaxToken targetToken,
            TypeDeclarationSyntax containingTypeDeclaration,
            BaseTypeDeclarationSyntax containingTypeOrEnumDeclaration,
            bool isInNonUserCode,
            bool isPreProcessorDirectiveContext,
            bool isPreProcessorKeywordContext,
            bool isPreProcessorExpressionContext,
            bool isTypeContext,
            bool isNamespaceContext,
            bool isStatementContext,
            bool isGlobalStatementContext,
            bool isAnyExpressionContext,
            bool isNonAttributeExpressionContext,
            bool isConstantExpressionContext,
            bool isAttributeNameContext,
            bool isEnumTypeMemberAccessContext,
            bool isInQuery,
            bool isInImportsDirective,
            bool isLabelContext,
            bool isTypeArgumentOfConstraintContext,
            bool isNamespaceDeclarationNameContext,
            bool isRightOfDotOrArrowOrColonColon,
            bool isIsOrAsContext,
            bool isObjectCreationTypeContext,
            bool isDefiniteCastTypeContext,
            bool isGenericTypeArgumentContext,
            bool isEnumBaseListContext,
            bool isIsOrAsTypeContext,
            bool isLocalVariableDeclarationContext,
            bool isFixedVariableDeclarationContext,
            bool isParameterTypeContext,
            bool isPossibleLambdaOrAnonymousMethodParameterTypeContext,
            bool isImplicitOrExplicitOperatorTypeContext,
            bool isPrimaryFunctionExpressionContext,
            bool isDelegateReturnTypeContext,
            bool isTypeOfExpressionContext,
            ISet<SyntaxKind> precedingModifiers,
            bool isInstanceContext,
            bool isCrefContext,
            bool isCatchFilterContext,
            bool isDestructorTypeContext)
            : base(workspace, semanticModel, position, leftToken, targetToken,
                   isTypeContext, isNamespaceContext,
                   isPreProcessorDirectiveContext,
                   isRightOfDotOrArrowOrColonColon, isStatementContext, isAnyExpressionContext,
                   isAttributeNameContext, isEnumTypeMemberAccessContext,
                   isInQuery, isInImportsDirective)
        {
            this.ContainingTypeDeclaration = containingTypeDeclaration;
            this.ContainingTypeOrEnumDeclaration = containingTypeOrEnumDeclaration;
            this.IsInNonUserCode = isInNonUserCode;
            this.IsPreProcessorKeywordContext = isPreProcessorKeywordContext;
            this.IsPreProcessorExpressionContext = isPreProcessorExpressionContext;
            this.IsGlobalStatementContext = isGlobalStatementContext;
            this.IsNonAttributeExpressionContext = isNonAttributeExpressionContext;
            this.IsConstantExpressionContext = isConstantExpressionContext;
            this.IsLabelContext = isLabelContext;
            this.IsTypeArgumentOfConstraintContext = isTypeArgumentOfConstraintContext;
            this.IsNamespaceDeclarationNameContext = isNamespaceDeclarationNameContext;
            this.IsIsOrAsContext = isIsOrAsContext;
            this.IsObjectCreationTypeContext = isObjectCreationTypeContext;
            this.IsDefiniteCastTypeContext = isDefiniteCastTypeContext;
            this.IsGenericTypeArgumentContext = isGenericTypeArgumentContext;
            this.IsEnumBaseListContext = isEnumBaseListContext;
            this.IsIsOrAsTypeContext = isIsOrAsTypeContext;
            this.IsLocalVariableDeclarationContext = isLocalVariableDeclarationContext;
            this.IsFixedVariableDeclarationContext = isFixedVariableDeclarationContext;
            this.IsParameterTypeContext = isParameterTypeContext;
            this.IsPossibleLambdaOrAnonymousMethodParameterTypeContext = isPossibleLambdaOrAnonymousMethodParameterTypeContext;
            this.IsImplicitOrExplicitOperatorTypeContext = isImplicitOrExplicitOperatorTypeContext;
            this.IsPrimaryFunctionExpressionContext = isPrimaryFunctionExpressionContext;
            this.IsDelegateReturnTypeContext = isDelegateReturnTypeContext;
            this.IsTypeOfExpressionContext = isTypeOfExpressionContext;
            this.PrecedingModifiers = precedingModifiers;
            this.IsInstanceContext = isInstanceContext;
            this.IsCrefContext = isCrefContext;
            this.IsCatchFilterContext = isCatchFilterContext;
            this.IsDestructorTypeContext = isDestructorTypeContext;
        }

        public static CSharpSyntaxContext CreateContext(Workspace workspace, SemanticModel semanticModel, int position, CancellationToken cancellationToken)
        {
            var syntaxTree = semanticModel.SyntaxTree;

            var isInNonUserCode = syntaxTree.IsInNonUserCode(position, cancellationToken);

            var preProcessorTokenOnLeftOfPosition = syntaxTree.FindTokenOnLeftOfPosition(position, cancellationToken, includeDirectives: true);
            var isPreProcessorDirectiveContext = syntaxTree.IsPreProcessorDirectiveContext(position, preProcessorTokenOnLeftOfPosition, cancellationToken);

            var leftToken = isPreProcessorDirectiveContext
                ? preProcessorTokenOnLeftOfPosition
                : syntaxTree.FindTokenOnLeftOfPosition(position, cancellationToken);

            var targetToken = leftToken.GetPreviousTokenIfTouchingWord(position);

            var isPreProcessorKeywordContext = isPreProcessorDirectiveContext
                ? syntaxTree.IsPreProcessorKeywordContext(position, leftToken, cancellationToken)
                : false;

            var isPreProcessorExpressionContext = isPreProcessorDirectiveContext
                ? targetToken.IsPreProcessorExpressionContext()
                : false;

            var isStatementContext = !isPreProcessorDirectiveContext
                ? targetToken.IsBeginningOfStatementContext()
                : false;

            var isGlobalStatementContext = !isPreProcessorDirectiveContext
                ? syntaxTree.IsGlobalStatementContext(position, cancellationToken)
                : false;

            var isAnyExpressionContext = !isPreProcessorDirectiveContext
                ? syntaxTree.IsExpressionContext(position, leftToken, attributes: true, cancellationToken: cancellationToken, semanticModelOpt: semanticModel)
                : false;

            var isNonAttributeExpressionContext = !isPreProcessorDirectiveContext
                ? syntaxTree.IsExpressionContext(position, leftToken, attributes: false, cancellationToken: cancellationToken, semanticModelOpt: semanticModel)
                : false;

            var isConstantExpressionContext = !isPreProcessorDirectiveContext
                ? syntaxTree.IsConstantExpressionContext(position, leftToken, cancellationToken)
                : false;

            var containingTypeDeclaration = syntaxTree.GetContainingTypeDeclaration(position, cancellationToken);
            var containingTypeOrEnumDeclaration = syntaxTree.GetContainingTypeOrEnumDeclaration(position, cancellationToken);

            var isDestructorTypeContext = targetToken.MatchesKind(SyntaxKind.TildeToken) &&
                                            targetToken.Parent.MatchesKind(SyntaxKind.DestructorDeclaration) &&
                                            targetToken.Parent.Parent.MatchesKind(SyntaxKind.ClassDeclaration, SyntaxKind.StructDeclaration);

            return new CSharpSyntaxContext(
                workspace,
                semanticModel,
                position,
                leftToken,
                targetToken,
                containingTypeDeclaration,
                containingTypeOrEnumDeclaration,
                isInNonUserCode,
                isPreProcessorDirectiveContext,
                isPreProcessorKeywordContext,
                isPreProcessorExpressionContext,
                syntaxTree.IsTypeContext(position, cancellationToken, semanticModelOpt: semanticModel),
                syntaxTree.IsNamespaceContext(position, cancellationToken, semanticModelOpt: semanticModel),
                isStatementContext,
                isGlobalStatementContext,
                isAnyExpressionContext,
                isNonAttributeExpressionContext,
                isConstantExpressionContext,
                syntaxTree.IsAttributeNameContext(position, cancellationToken),
                syntaxTree.IsEnumTypeMemberAccessContext(semanticModel, position, cancellationToken),
                leftToken.GetAncestor<QueryExpressionSyntax>() != null,
                IsLeftSideOfUsingAliasDirective(leftToken, cancellationToken),
                syntaxTree.IsLabelContext(position, cancellationToken),
                syntaxTree.IsTypeArgumentOfConstraintClause(position, cancellationToken),
                syntaxTree.IsNamespaceDeclarationNameContext(position, cancellationToken),
                syntaxTree.IsRightOfDotOrArrowOrColonColon(position, cancellationToken),
                syntaxTree.IsIsOrAsContext(position, leftToken, cancellationToken),
                syntaxTree.IsObjectCreationTypeContext(position, leftToken, cancellationToken),
                syntaxTree.IsDefiniteCastTypeContext(position, leftToken, cancellationToken),
                syntaxTree.IsGenericTypeArgumentContext(position, leftToken, cancellationToken),
                syntaxTree.IsEnumBaseListContext(position, leftToken, cancellationToken),
                syntaxTree.IsIsOrAsTypeContext(position, leftToken, cancellationToken),
                syntaxTree.IsLocalVariableDeclarationContext(position, leftToken, cancellationToken),
                syntaxTree.IsFixedVariableDeclarationContext(position, leftToken, cancellationToken),
                syntaxTree.IsParameterTypeContext(position, leftToken, cancellationToken),
                syntaxTree.IsPossibleLambdaOrAnonymousMethodParameterTypeContext(position, leftToken, cancellationToken),
                syntaxTree.IsImplicitOrExplicitOperatorTypeContext(position, leftToken, cancellationToken),
                syntaxTree.IsPrimaryFunctionExpressionContext(position, leftToken, cancellationToken),
                syntaxTree.IsDelegateReturnTypeContext(position, leftToken, cancellationToken),
                syntaxTree.IsTypeOfExpressionContext(position, leftToken, cancellationToken),
                syntaxTree.GetPrecedingModifiers(position, leftToken, cancellationToken),
                syntaxTree.IsInstanceContext(position, leftToken, cancellationToken),
                syntaxTree.IsCrefContext(position, cancellationToken) && !leftToken.MatchesKind(SyntaxKind.DotToken),
                syntaxTree.IsCatchFilterContext(position, leftToken),
                isDestructorTypeContext);
        }

        public static CSharpSyntaxContext CreateContext_Test(SemanticModel semanticModel, int position, CancellationToken cancellationToken)
        {
            return CreateContext(/*workspace*/null, semanticModel, position, cancellationToken);
        }

        public bool IsTypeAttributeContext(CancellationToken cancellationToken)
        {
            // cases:
            //    [ |
            //    class C { [ |
            var token = this.TargetToken;

            // Note that we pass the token.SpanStart to IsTypeDeclarationContext below. This is a bit subtle,
            // but we want to be sure that the attribute itself (i.e. the open square bracket, '[') is in a
            // type declaration context.
            if (token.CSharpKind() == SyntaxKind.OpenBracketToken &&
                token.Parent.CSharpKind() == SyntaxKind.AttributeList &&
                this.SyntaxTree.IsTypeDeclarationContext(
                    token.SpanStart, contextOpt: null, validModifiers: null, validTypeDeclarations: SyntaxKindSet.ClassStructTypeDeclarations, canBePartial: false, cancellationToken: cancellationToken))
            {
                return true;
            }

            return false;
        }

        public bool IsTypeDeclarationContext(
            ISet<SyntaxKind> validModifiers = null,
            ISet<SyntaxKind> validTypeDeclarations = null,
            bool canBePartial = false,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            return this.SyntaxTree.IsTypeDeclarationContext(this.Position, this, validModifiers, validTypeDeclarations, canBePartial, cancellationToken);
        }

        public bool IsMemberAttributeContext(ISet<SyntaxKind> validTypeDeclarations, CancellationToken cancellationToken)
        {
            // cases:
            //   class C { [ |
            var token = this.TargetToken;

            if (token.CSharpKind() == SyntaxKind.OpenBracketToken &&
                token.Parent.CSharpKind() == SyntaxKind.AttributeList &&
                this.SyntaxTree.IsMemberDeclarationContext(
                    token.SpanStart, contextOpt: null, validModifiers: null, validTypeDeclarations: validTypeDeclarations, canBePartial: false, cancellationToken: cancellationToken))
            {
                return true;
            }

            return false;
        }

        public bool IsMemberDeclarationContext(
            ISet<SyntaxKind> validModifiers = null,
            ISet<SyntaxKind> validTypeDeclarations = null,
            bool canBePartial = false,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            return this.SyntaxTree.IsMemberDeclarationContext(this.Position, this, validModifiers, validTypeDeclarations, canBePartial, cancellationToken);
        }

        private static bool IsLeftSideOfUsingAliasDirective(SyntaxToken leftToken, CancellationToken cancellationToken)
        {
            var usingDirective = leftToken.GetAncestor<UsingDirectiveSyntax>();

            if (usingDirective != null)
            {
                // No = token: 
                if (usingDirective.Alias == null || usingDirective.Alias.EqualsToken.IsMissing)
                {
                    return true;
                }

                return leftToken.SpanStart < usingDirective.Alias.EqualsToken.SpanStart;
            }

            return false;
        }
    }
}