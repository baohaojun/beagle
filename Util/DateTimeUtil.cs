//
// DateTimeUtil.cs
//
// Copyright (C) 2006 Debajyoti Bera <dbera.web@gmail.com>
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
using System.Globalization;

namespace Beagle.Util {

	public class DateTimeUtil {

		private DateTimeUtil () { }

		private static DateTime epoch;

		static DateTimeUtil ()
		{
			epoch = new DateTime (1970, 1, 1, 0, 0, 0);
		}

		public static DateTime UnixToDateTimeUtc (long time_t)
		{
			// Hack to compensate for lousy .Net-1 DateTime
			// DateTime (1970,1,1,0,0,0,0) creates a datetime of 1970/1/1 00:00:00 _Localtime_
			// Adjust timezone difference to make the time correct wrt to its timezone
			return epoch.AddSeconds (time_t).ToLocalTime ();
		}

		public static string ToString (DateTime dt)
		{
			return dt.ToString("yyyy-MM-dd HH:mm:ss zzz", CultureInfo.InvariantCulture);
		}
	}
}
		