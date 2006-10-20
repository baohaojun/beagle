//
// GoogleDriver.cs
//
// Copyright (C) 2004 Novell, Inc.
//
// Google is a trademark of Google.  But you already knew that.
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
using System.Text;

using Beagle.Util;

namespace Beagle.Daemon {

	[BackendFlavor (Name="Google", Domain=QueryDomain.Global)]
	public class GoogleDriver : BackendBase, IQueryable {

		int maxResults = 5;

		GoogleSearchService gss = new GoogleSearchService ();
		string googleKey;

		public GoogleDriver ()
		{
			googleKey = Environment.GetEnvironmentVariable ("GOOGLE_WEB_API_KEY");
		}

		public override void Start () 
		{
		}

		public override IQueryable Queryable {
			get { return this; }
		}

		Hit FromGoogleResultElement (ResultElement res, int rank)
		{
			Hit hit = new Hit ();

			hit.Uri      = new Uri (res.URL, true);
			hit.Type     = "Google";
			hit.MimeType = "text/html"; // FIXME
			hit.Source   = "Google";

			// FIXME: We don't get scoring information from Google
			// other than the ranks.  This is a hack.
			hit.Score    = 0.2f / (1 + rank);

			hit ["Summary"]        = res.summary;
			hit ["Snippet"]        = res.snippet;
			hit ["Title"]          = res.title;
			hit ["CachedSize"]     = res.cachedSize;
			hit ["HostName"]       = res.hostName;
			hit ["DirectoryTitle"] = res.directoryTitle;

			return hit;
		}

		static bool showNoKeyMessage = true;

		public bool AcceptQuery (Query query)
		{
			if (query.IsEmpty)
				return false;

			if (! query.AllowsDomain (QueryDomain.Global))
				return false;

			// FIXME: This is a meta-FIXME, since this is a bad assumption
			// because the mime-type setting FIXME above.
			if (! query.AllowsMimeType ("text/html"))
				return false;

			// Reject queries if the key isn't set.
			if (googleKey == null || googleKey == "") {
				if (showNoKeyMessage) {
					Logger.Log.Warn ("To query Google, put your Google key into the GOOGLE_WEB_API_KEY environment variable.");
					Logger.Log.Warn ("To get a Google key, go to http://api.google.com/createkey");
					showNoKeyMessage = false;
				}
				return false;
			}

			return true;
		}


		public void DoQuery (Query query,
				     IQueryResult result,
				     IQueryableChangeData changeData)
		{
			StringBuilder sb = new StringBuilder ();
			
			foreach (QueryPart part in query.Parts) {
				if (part is QueryPart_Human) {
				        sb.Append (((QueryPart_Human) part).QueryString);
				} else if (part is QueryPart_Text) {
				        sb.Append (((QueryPart_Text) part).Text);
				} else
					continue; // ignore other query parts
			}

			Logger.Log.Debug ("Querying google for '" + sb.ToString () + "'");
			GoogleSearchResult gsr = gss.doGoogleSearch (googleKey,
								     sb.ToString (),
								     0, maxResults,
								     false, "", false, "", "", "");

			ArrayList hits = new ArrayList ();
			int rank = 0;
			foreach (ResultElement elt in gsr.resultElements) {
				Hit hit = FromGoogleResultElement (elt, rank);
				hits.Add (hit);
				++rank;
			}
			result.Add (hits);
		}

		public string GetSnippet (string[] query_terms, Hit hit)
		{
			return hit ["Snippet"];
		}

		public QueryableStatus GetQueryableStatus ()
		{
			return null;
		}
	}

}
