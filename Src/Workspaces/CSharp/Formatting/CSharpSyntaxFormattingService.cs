﻿// Copyright (c) Microsoft Open Technologies, Inc.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.ComponentModel.Composition;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Formatting;
using Microsoft.CodeAnalysis.Formatting.Rules;
using Microsoft.CodeAnalysis.Host;
using Microsoft.CodeAnalysis.Host.Mef;
using Microsoft.CodeAnalysis.Options;
using Microsoft.CodeAnalysis.Shared.Collections;
using Microsoft.CodeAnalysis.Shared.Utilities;
using Microsoft.CodeAnalysis.Text;
using Roslyn.Utilities;

namespace Microsoft.CodeAnalysis.CSharp.Formatting
{
    [ExportLanguageService(typeof(ISyntaxFormattingService), LanguageNames.CSharp)]
    internal class CSharpSyntaxFormattingService : AbstractSyntaxFormattingService
    {
        private readonly Lazy<IEnumerable<IFormattingRule>> lazyExportedRules;

        [ImportingConstructor]
        public CSharpSyntaxFormattingService([ImportMany] IEnumerable<Lazy<IFormattingRule, OrderableLanguageMetadata>> rules)
        {
            this.lazyExportedRules = new Lazy<IEnumerable<IFormattingRule>>(() =>
                ExtensionOrderer.Order(rules)
                                .Where(x => x.Metadata.Language == LanguageNames.CSharp)
                                .Select(x => x.Value)
                                .Concat(new DefaultOperationProvider())
                                .ToImmutableArray());
        }

        public override IEnumerable<IFormattingRule> GetDefaultFormattingRules()
        {
            var rules = this.lazyExportedRules.Value;

            var spaceFormattingRules = new IFormattingRule[]
                {
                    new WrappingFormattingRule(),
                    new SpacingFormattingRule(),
                    new NewLineUserSettingFormattingRule(),
                    new IndentUserSettingsFormattingRule()
                };

            return spaceFormattingRules.Concat(rules).ToImmutableArray();
        }

        protected override IFormattingResult CreateAggregatedFormattingResult(SyntaxNode node, IList<AbstractFormattingResult> results, SimpleIntervalTree<TextSpan> formattingSpans = null)
        {
            return new AggregatedFormattingResult(node, results, formattingSpans);
        }

        protected override AbstractFormattingResult Format(SyntaxNode node, OptionSet optionSet, IEnumerable<IFormattingRule> formattingRules, SyntaxToken token1, SyntaxToken token2, CancellationToken cancellationToken)
        {
            return new CSharpFormatEngine(node, optionSet, formattingRules, token1, token2).Format(cancellationToken);
        }
    }
}