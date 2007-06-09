//
// RangeList.cs: A list specialized in storing integer ranges
//
// Copyright (C) 2007 Pierre Östlund
//

//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.
//

using System;
using System.Collections;
using System.Collections.Generic;

namespace Beagle.Util.Thunderbird.Utilities {
	
	public struct Range {
		public int Start;
		public int End;
		
		public static Range New (int start, int end)
		{
			throw new NotImplementedException ();
		}
		
		// Expected formatting: "Start: {0}, End: {1}"
		public override string ToString ()
		{
			throw new NotImplementedException ();
		}
	}
	
	public class RangeList : ICollection<Range> {
		
		public RangeList ()
		{
			throw new NotImplementedException ();
		}
		
		public void Add (Range range)
		{
			throw new NotImplementedException ();
		}
		
		public bool Remove (Range range)
		{
			throw new NotImplementedException ();
		}
		
		public void Clear ()
		{
			throw new NotImplementedException ();
		}
		
		public void CopyTo (Range[] ranges, int index)
		{
			throw new NotImplementedException ();
		}
		
		public bool Contains (Range range)
		{
			throw new NotImplementedException ();
		}
		
		public bool Contains (int n)
		{
			throw new NotImplementedException ();
		}
		
		public IEnumerator<Range> GetEnumerator ()
		{
			throw new NotImplementedException ();
		}
		
		/* public */ IEnumerator IEnumerable.GetEnumerator ()
		{
			throw new NotImplementedException ();
		}
		
		public int Count {
			get {
				throw new NotImplementedException ();
			}
		}
		
		public bool IsReadOnly {
			get {
				throw new NotImplementedException ();
			}
		}
	}
	
	public class InvalidRangeException : Exception {
	
		public InvalidRangeException (string message) : base (message) { }
	}
}
