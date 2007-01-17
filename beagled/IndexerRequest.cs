//
// IndexerRequest.cs
//
// Copyright (C) 2005-2006 Novell, Inc.
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
using System.Xml.Serialization;

using Beagle.Util;

namespace Beagle.Daemon {

	public class IndexerRequest {

		public bool OptimizeIndex = false;

		// These two collections are mutually exclusive.  It's an ugly
		// hack, but it's necessary to make XML serialization work
		// easily, and assumes that an IndexerRequest is never changed
		// after it has been serialized.
		private Hashtable indexables_by_uri = null;
		private ArrayList indexables = null;

		public void Clear ()
		{
			OptimizeIndex = false;
			indexables_by_uri = null;
			indexables = null;
		}
	       
		public void Add (Indexable indexable)
		{
			if (indexables != null)
				throw new Exception ("Attempt to add to a serialized IndexerRequest");

			if (indexable == null)
				return;

			if (indexables_by_uri == null)
				indexables_by_uri = UriFu.NewHashtable ();

			Indexable prior;
			prior = indexables_by_uri [indexable.Uri] as Indexable;

			if (prior != null) {
				
				switch (indexable.Type) {

				case IndexableType.Add:
				case IndexableType.Remove:
					// Do nothing, and just clobber the prior indexable.
					break;

				case IndexableType.PropertyChange:
					// Merge with the prior indexable.
					prior.Merge (indexable);
					indexable = prior;
					break;
				}
			}

			indexables_by_uri [indexable.Uri] = indexable;
		}

		public Indexable GetByUri (Uri uri)
		{
			if (indexables_by_uri != null)
				return indexables_by_uri [uri] as Indexable;

			// Fall back to using the list.  This happens when we
			// have been serialized.  Which is sort of lame.
			foreach (Indexable indexable in indexables) {
				if (UriFu.Equals (uri, indexable.Uri))
					return indexable;
			}
			return null;
		}

		[XmlIgnore]
		public ICollection Indexables {
			get { 
				if (indexables != null)
					return indexables;
				else if (indexables_by_uri != null)
					return indexables_by_uri.Values;
				else
					return new Indexable [0];
			}
		}

		[XmlArray (ElementName="Indexables")]
		[XmlArrayItem (ElementName="Indexable", Type=typeof (Indexable))]
		public ArrayList IndexablesForSerialization {
			get { 
				if (indexables == null) {
					indexables = new ArrayList (Indexables);
					indexables.Sort (); // sort into ascending timestamp order
				}
				return indexables;
			}
		}

		[XmlIgnore]
		public int Count {
			get { return Indexables.Count; }
		}
		
		[XmlIgnore]
		public bool IsEmpty {
			get { return Count == 0 && ! OptimizeIndex; }
		}

		public void Cleanup ()
		{
			foreach (Indexable i in Indexables)
				i.Cleanup ();
		}
	}
}
