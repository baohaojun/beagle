//
// FilterText.cs
//
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
using System.IO;

using Beagle.Daemon;

namespace Beagle.Filters {

	public class FilterText : Beagle.Daemon.Filter {

		public FilterText ()
		{
			AddSupportedFlavor (FilterFlavor.NewFromMimeType ("text/plain"));
			AddSupportedFlavor (FilterFlavor.NewFromMimeType ("text/x-log"));
			AddSupportedFlavor (FilterFlavor.NewFromMimeType ("text/x-readme"));
			AddSupportedFlavor (FilterFlavor.NewFromMimeType ("text/x-install"));
			AddSupportedFlavor (FilterFlavor.NewFromMimeType ("text/x-credits"));
			AddSupportedFlavor (FilterFlavor.NewFromMimeType ("text/x-authors"));
			AddSupportedFlavor (FilterFlavor.NewFromMimeType ("text/x-copying"));

			SnippetMode = true;
			OriginalIsText = true;
		}

		const long LENGTH_CUTOFF = 5 * 1024 * 1024; // 5 Mb

		override protected void DoOpen (FileInfo file)
		{
			// Extremely large files of type text/plain are usually log files,
			// data files, or other bits of not-particularly-human-readable junk
			// that will tend to clog up our indexes.
			if (file.Length > LENGTH_CUTOFF) {
				Beagle.Util.Logger.Log.Debug ("{0} is too large to filter!", file.FullName);
				Error ();
			}
		}

		override protected void DoPull ()
		{
			string str = TextReader.ReadLine ();
			if (str == null) {
				Finished ();
			} else if (str.Length > 0) {
				AppendText (str);
				AppendStructuralBreak ();
			}
		}
	}
}
