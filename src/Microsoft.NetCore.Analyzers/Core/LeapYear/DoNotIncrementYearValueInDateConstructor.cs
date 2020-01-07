﻿// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Collections.Immutable;
using System.Globalization;
using Analyzer.Utilities;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Microsoft.NetCore.Analyzers.LeapYear
{
    /// <summary>
    /// CA2260: Do not increment or decrement year parameter in DateTime constructor.
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class DoNotIncrementYearValueInDateConstructor : DiagnosticAnalyzer
    {
        internal const string RuleId = "CA2260";

        private static readonly LocalizableString _localizableTitle = new LocalizableResourceString(nameof(MicrosoftNetCoreAnalyzersResources.DoNotIncrementYearInDateTimeConstructorTitle), MicrosoftNetCoreAnalyzersResources.ResourceManager, typeof(MicrosoftNetCoreAnalyzersResources));
        private static readonly LocalizableString _localizableMessage = new LocalizableResourceString(nameof(MicrosoftNetCoreAnalyzersResources.DoNotIncrementYearInDateTimeConstructorMessage), MicrosoftNetCoreAnalyzersResources.ResourceManager, typeof(MicrosoftNetCoreAnalyzersResources));

        internal static DiagnosticDescriptor Rule = new DiagnosticDescriptor(
            RuleId,
            _localizableTitle,
            _localizableMessage,
            DiagnosticCategory.Usage,
            DiagnosticHelpers.DefaultDiagnosticSeverity,
            isEnabledByDefault: false,
            helpLinkUri: null);

        private static readonly LocalizableString _localizableIdentifierTitle = new LocalizableResourceString(nameof(MicrosoftNetCoreAnalyzersResources.DoNotIncrementYearInDateTimeIdentifierConstructorTitle), MicrosoftNetCoreAnalyzersResources.ResourceManager, typeof(MicrosoftNetCoreAnalyzersResources));
        private static readonly LocalizableString _localizableIdentifierMessage = new LocalizableResourceString(nameof(MicrosoftNetCoreAnalyzersResources.DoNotIncrementYearInDateTimeIdentifierConstructorMessage), MicrosoftNetCoreAnalyzersResources.ResourceManager, typeof(MicrosoftNetCoreAnalyzersResources));

        internal static DiagnosticDescriptor IdentifierRule = new DiagnosticDescriptor(
            RuleId,
            _localizableIdentifierTitle,
            _localizableIdentifierMessage,
            DiagnosticCategory.Usage,
            DiagnosticHelpers.DefaultDiagnosticSeverity,
            isEnabledByDefault: false,
            helpLinkUri: null);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule, IdentifierRule);

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.Analyze);
            context.EnableConcurrentExecution();
            context.RegisterSemanticModelAction(semanticModelAnalysisContext =>
            {
                var semanticModel = new DateKindSemanticModel(semanticModelAnalysisContext.SemanticModel);
                var walker = new DateKindWalker(semanticModel);

                walker.Visit(semanticModelAnalysisContext.SemanticModel.SyntaxTree.GetRoot(semanticModelAnalysisContext.CancellationToken));
                foreach (DateKindContext dateKindContext in walker.Dates)
                {
                    // If there was a reason to ignore any LYA issue, skip over this context.
                    if (!dateKindContext.IgnoreDiagnostic)
                    {
                        // Diagnostic should not be raised if we can determine that
                        // the date won't be a possible leap year.
                        if (!dateKindContext.AreMonthOrDayValuesSafe())
                        {
                            if (YearIncrementIssueExists(dateKindContext))
                            {
                                if (dateKindContext.YearArgumentBinaryExpression != null)
                                {
                                    semanticModelAnalysisContext.ReportDiagnostic(
                                        Diagnostic.Create(
                                            Rule,
                                            dateKindContext.ObjectCreationExpression.GetLocation(),
                                            dateKindContext.YearArgumentBinaryExpression));
                                }

                                if (dateKindContext.YearArgumentIdentifierBinaryExpression != null)
                                {
                                    semanticModelAnalysisContext.ReportDiagnostic(
                                        Diagnostic.Create(
                                            IdentifierRule,
                                            dateKindContext.ObjectCreationExpression.GetLocation(),
                                            dateKindContext.YearArgumentIdentifier,
                                            dateKindContext.YearArgumentIdentifierBinaryExpression));
                                }
                            }
                        }
                    }
                }
            });
        }

        /// <summary>
        /// Examines stored code analysis to see if there is a year increment issue.
        /// </summary>
        /// <returns>True if a year increment pattern was detected.</returns>
        private static bool YearIncrementIssueExists(DateKindContext dateKindContext)
        {
            return IsBinaryExpressionAYearIncrementTriggeringPattern(dateKindContext.YearArgumentBinaryExpression)
                || IsBinaryExpressionAYearIncrementTriggeringPattern(dateKindContext.YearArgumentIdentifierBinaryExpression);
        }

        /// <summary>
        /// Returns true if the binary expression contains an operator for addition(+)
        /// or subtraction(-) and neither of the operands are numeric literals greater
        /// than 1000.
        /// </summary>
        private static bool IsBinaryExpressionAYearIncrementTriggeringPattern(BinaryExpressionSyntax? node)
        {
            return node != null
                && (node.IsKind(SyntaxKind.AddExpression) || node.IsKind(SyntaxKind.SubtractExpression))
                && !IsExpressionLiteralIntValueGreaterThanOrEqual(node.Left, 1000)
                && !IsExpressionLiteralIntValueGreaterThanOrEqual(node.Right, 1000);
        }

        private static bool IsExpressionLiteralIntValueGreaterThanOrEqual(ExpressionSyntax expressionSyntax, int limit)
        {
            return expressionSyntax.IsKind(SyntaxKind.NumericLiteralExpression)
                && expressionSyntax is LiteralExpressionSyntax literal
                && int.Parse(literal.Token.ValueText, new NumberFormatInfo()) >= limit;
        }
    }
}