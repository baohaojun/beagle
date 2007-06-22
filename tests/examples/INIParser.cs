//
// INIParser.cs: An INI file parser example
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
using Beagle.Util.Thunderbird;
using Beagle.Util.Thunderbird.Preferences;

namespace Examples {
	
	public static class Preferences {
		
		public static void Main (string[] args)
		{
			if (args == null || args.Length != 1) {
				Console.WriteLine ("Usage: INIParser.exe <file>");
				Environment.Exit (0);
			}
			
			INIFileParser parser = new INIFileParser (args [0]);
			Console.WriteLine ("File content:");
			Console.WriteLine ("-------------");
			foreach (INISection section in parser)
				Console.WriteLine (section);
		}
	}
}
