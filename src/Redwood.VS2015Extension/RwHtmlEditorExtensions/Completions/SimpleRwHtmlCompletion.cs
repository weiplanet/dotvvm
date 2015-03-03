﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Media;
using Microsoft.VisualStudio.Language.Intellisense;

namespace Redwood.VS2015Extension.RwHtmlEditorExtensions.Completions
{
    public class SimpleRwHtmlCompletion : Completion
    {
        private static ImageSource _glyph = null; //GlyphService.GetGlyph(StandardGlyphGroup.GlyphGroupVariable, StandardGlyphItem.GlyphItemPublic);

        public SimpleRwHtmlCompletion(string displayText)
            : this(displayText, displayText)
        {
            
        }

        public SimpleRwHtmlCompletion(string displayText, string completionText)
            : base(displayText, completionText, string.Empty, _glyph, displayText)
        {

        }
    }
}