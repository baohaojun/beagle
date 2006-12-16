//
// FilterHtml.cs
//
// Copyright (C) 2005 Debajyoti Bera <dbera.web@gmail.com>
// Copyright (C) 2004 Novell, Inc.
//

//
// Permission is hereby granted, free of charge, to any person obtaining a
// copy of this software and associated documentation files (the "Software"),
// to deal in the Software without restriction, including without limitation
// the rights to use, copy, modify, merge, publish, distribute, sublicense,
// and/or sell copies of the Software, and to permit persons to whom the
// Software is furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING
// FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER
// DEALINGS IN THE SOFTWARE.
//


using System;
using System.Collections;
using System.IO;
using System.Text;
using SW=System.Web;

using Beagle.Daemon;
using Beagle.Util;

using HtmlAgilityPack;

namespace Beagle.Filters {

	public class FilterHtml : Beagle.Daemon.Filter {
		// When see <b> push "b" in the stack
		// When see </b> pop from the stack
		// For good error checking, we should compare
		// current element with what was popped
		// Currently, we just pop, this might allow
		// unmatched elements to pass through
		private Stack hot_stack;
		private Stack ignore_stack;
		private bool building_text;
		private StringBuilder builder;
		protected Encoding enc;

		// delegate types
		public delegate int AppendTextCallback (string s);
		public delegate void AddPropertyCallback (Beagle.Property p);
		public delegate void AppendSpaceCallback ();
		public delegate void HotCallback ();

		// delegates
		private new AppendTextCallback AppendText;
		private new AddPropertyCallback AddProperty;
		private new AppendSpaceCallback AppendWhiteSpace;
		private new AppendSpaceCallback AppendStructuralBreak;
		private new HotCallback HotUp;
		private new HotCallback HotDown;

		public FilterHtml (bool register_filter)
		{
			if (register_filter) {
				// 1: Add meta keyword fields as meta:key
				SetVersion (1);
				RegisterSupportedTypes ();
				SnippetMode = true;

				AppendText = new AppendTextCallback (base.AppendText);
				AddProperty = new AddPropertyCallback (base.AddProperty);
				AppendWhiteSpace = new AppendSpaceCallback (base.AppendWhiteSpace);
				AppendStructuralBreak = new AppendSpaceCallback (base.AppendStructuralBreak);
				HotUp = new HotCallback (base.HotUp);
				HotDown = new HotCallback (base.HotDown);
			}

			hot_stack = new Stack ();
			ignore_stack = new Stack ();
			building_text = false;
			builder = new StringBuilder ();
		}

		public FilterHtml () : this (true) {}

		// Safeguard against spurious stack pop ups...
		// caused by mismatched tags in bad html files
		// FIXME: If matching elements is not required
		// and if HtmlAgilityPack matches elements itself,
		// then we can just use a counter hot_stack_depth
		// instead of the hot_stack
		private void SafePop (Stack st)
		{
			if (st != null && st.Count != 0)
				st.Pop ();
		}
		
		protected bool NodeIsHot (String nodeName) 
		{
			return nodeName == "b"
				|| nodeName == "u"
				|| nodeName == "em"
				|| nodeName == "strong"
				|| nodeName == "big"
				|| nodeName == "h1"
				|| nodeName == "h2"
				|| nodeName == "h3"
				|| nodeName == "h4"
				|| nodeName == "h5"
				|| nodeName == "h6"
				|| nodeName == "i"
				|| nodeName == "th";
		}

		protected static bool NodeBreaksText (String nodeName) 
		{
			return nodeName == "td"
				|| nodeName == "a"
				|| nodeName == "div"
				|| nodeName == "option";
		}

		protected static bool NodeBreaksStructure (string nodeName)
		{
			return nodeName == "p"
				|| nodeName == "br"
				|| nodeName == "h1"
				|| nodeName == "h2"
				|| nodeName == "h3"
				|| nodeName == "h4"
				|| nodeName == "h5"
				|| nodeName == "h6";
		}
		
		protected static bool NodeIsContentFree (String nodeName) 
		{
			return nodeName == "script"
				|| nodeName == "map"
				|| nodeName == "style";
		}

		protected bool HandleNodeEvent (HtmlNode node)
		{
			switch (node.NodeType) {
				
			case HtmlNodeType.Document:
			case HtmlNodeType.Element:
				if (node.Name == "title") {
					if (node.StartTag) {
						builder.Length = 0;
						building_text = true;
					} else {
						String title = HtmlEntity.DeEntitize (builder.ToString ().Trim ());
						AddProperty (Beagle.Property.New ("dc:title", title));
						builder.Length = 0;
						building_text = false;
					}
				} else if (node.Name == "meta") {
	   				string name = node.GetAttributeValue ("name", String.Empty);
           				string content = node.GetAttributeValue ("content", String.Empty);
					if (name != String.Empty)
						AddProperty (Beagle.Property.New ("meta:" + name, content));
				} else if (! NodeIsContentFree (node.Name)) {
					bool isHot = NodeIsHot (node.Name);
					bool breaksText = NodeBreaksText (node.Name);
					bool breaksStructure = NodeBreaksStructure (node.Name);

					if (breaksText)
						AppendWhiteSpace ();

					if (node.StartTag) {
						if (isHot) {
							if (hot_stack.Count == 0)
								HotUp ();
							hot_stack.Push (node.Name);
						}
						if (node.Name == "img") {
							string attr = node.GetAttributeValue ("alt", String.Empty);
							if (attr != String.Empty) {
								AppendText (HtmlEntity.DeEntitize (attr));
								AppendWhiteSpace ();
							}
						} else if (node.Name == "a") {
							string attr = node.GetAttributeValue ("href", String.Empty);
							if (attr != String.Empty) {
								AppendText (HtmlEntity.DeEntitize (
									    SW.HttpUtility.UrlDecode (attr, enc)));
								AppendWhiteSpace ();
							}
						}
					} else { // (! node.StartTag)
						if (isHot) {
							SafePop (hot_stack);
							if (hot_stack.Count == 0)
								HotDown ();
						}	
						if (breaksStructure)
							AppendStructuralBreak ();
					}

					if (breaksText)
						AppendWhiteSpace ();
				} else {
					// so node is a content-free node
					// ignore contents of such node
					if (node.StartTag)
						ignore_stack.Push (node.Name);
					else
						SafePop (ignore_stack);
				}
				break;
				
			case HtmlNodeType.Text:
				// FIXME Do we need to trim the text ?
				String text = ((HtmlTextNode)node).Text;
				if (ignore_stack.Count != 0)
					break; // still ignoring ...
				if (building_text)
					builder.Append (text);
				else
					AppendText (HtmlEntity.DeEntitize (text));
				//if (hot_stack.Count != 0)
				//Console.WriteLine (" TEXT:" + text + " ignore=" + ignore_stack.Count);
				break;
			}

			if (! AllowMoreWords ())
				return false;
			return true;
		}

		override protected void DoOpen (FileInfo info)
		{
			enc = null;

			foreach (Property prop in IndexableProperties) {
				if (prop.Key != StringFu.UnindexedNamespace + "encoding")
					continue;

				try {
					enc = Encoding.GetEncoding ((string) prop.Value);
				} catch (NotSupportedException) {
					// Encoding passed in isn't supported.  Maybe
					// we'll get lucky detecting it from the
					// document instead.
				}

				break;
			}

			if (enc == null) {
				// we need to tell the parser to detect encoding,
				HtmlDocument temp_doc = new HtmlDocument ();
				try {
					enc = temp_doc.DetectEncoding (Stream);
				} catch (NotSupportedException) {
					enc = Encoding.ASCII;
				}
				//Console.WriteLine ("Detected encoding:" + (enc == null ? "null" : enc.EncodingName));
				temp_doc = null;
				Stream.Seek (0, SeekOrigin.Begin);
			}

			HtmlDocument doc = new HtmlDocument ();
			doc.ReportNode += HandleNodeEvent;
			doc.StreamMode = true;
			// we already determined encoding
			doc.OptionReadEncoding = false;
	
			try {
				if (enc == null)
					doc.Load (Stream);
				else
					doc.Load (Stream, enc);
			} catch (NotSupportedException) {
				enc = Encoding.ASCII;
				doc.Load (Stream, enc);
			} catch (Exception e) {
				Log.Debug (e, "Exception while filtering HTML file " + info.FullName);
			}

			Finished ();
		}

		public void ExtractText (string html_string,
					 AppendTextCallback append_text_cb,
					 AddPropertyCallback add_prop_cb,
					 AppendSpaceCallback append_white_cb,
					 AppendSpaceCallback append_break_cb,
					 HotCallback hot_up_cb,
					 HotCallback hot_down_cb)
		{
			AppendText = append_text_cb;
			AddProperty = add_prop_cb;
			AppendWhiteSpace = append_white_cb;
			AppendStructuralBreak = append_break_cb;
			HotUp = hot_up_cb;
			HotDown = hot_down_cb;

			HtmlDocument doc = new HtmlDocument ();
			doc.ReportNode += HandleNodeEvent;
			doc.StreamMode = true;
	
			try {
				doc.LoadHtml (html_string);
			} catch (Exception e) {
				Log.Debug (e, "Exception while filtering html string [{0}]", html_string);
			}

		}

		virtual protected void RegisterSupportedTypes () 
		{
			AddSupportedFlavor (FilterFlavor.NewFromMimeType ("text/html"));
		}
	}

}
