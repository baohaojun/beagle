//
// FilterFactory.cs
//
// Copyright (C) 2004 Novell, Inc.
// Copyright (C) 2008 Lukas Lipka <lukaslipka@gmail.com>
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
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Reflection;

using System.Xml;
using System.Xml.Serialization;

using Beagle.Util;

namespace Beagle.Filters {

	public class FilterFactory {

		static private bool Debug = false;

		class FilterCache {
			private int version = 1;
			private SortedDictionary<string, DateTime> mtime_cache = new SortedDictionary<string, DateTime> ();
			private string filter_cache_dir;

			public FilterCache (string filter_cache_dir)
			{
				this.filter_cache_dir = filter_cache_dir;
			}

			internal void RegisterFilter (string filter_file_name, DateTime filter_last_mtime)
			{
				mtime_cache [filter_file_name] = filter_last_mtime;
			}

			// Return true if dirty, else return false
			internal bool UpdateCache ()
			{
				StringBuilder cache_string_sb = new StringBuilder ();
				// Format of filterver.dat:
				// <Version\n><FilterAssembly.Filename:FilterAssemblyFile.LastMTime>\n+
				//
				cache_string_sb.AppendFormat ("{0}\n", version);

				foreach (KeyValuePair<string, DateTime> kvp in mtime_cache)
					cache_string_sb.AppendFormat ("{0}:{1}\n", kvp.Key, kvp.Value);

				if (filter_cache_dir == null)
					filter_cache_dir = PathFinder.StorageDir;

				string filterver_dat = Path.Combine (filter_cache_dir, "filterver.dat");
				string old_cache_text = null;

				try {
					old_cache_text = File.ReadAllText (filterver_dat);
				} catch (FileNotFoundException) {
					Log.Debug ("Cannot read {0}, assuming dirty filterver.dat", filterver_dat);
				}

				string new_cache_text = cache_string_sb.ToString ();

				bool is_dirty = (old_cache_text != new_cache_text);
				Log.Debug ("Verifying filter_cache at {0} ... cache is dirty ? {1}", filterver_dat, is_dirty);

				if (! is_dirty)
					return false;

				// If dirty, write the new version info
				try {
					File.WriteAllText (filterver_dat, new_cache_text);
				} catch (Exception e) {
					Log.Warn ("Unable to update {0}", filterver_dat);
				}

				return true;
			}
		}

		static private string filter_cache_dir = null;
		public static string FilterCacheDir {
			set { filter_cache_dir = value; }
		}

		private static bool cache_dirty = true;
		public static bool DirtyFilterCache {
			get { return cache_dirty; }
		}

		static FilterFactory ()
		{
			FilterCache filter_cache = new FilterCache (filter_cache_dir);
			ReflectionFu.ScanEnvironmentForAssemblies ("BEAGLE_FILTER_PATH", PathFinder.FilterDir,
								   delegate (Assembly a) {
									   int n = ScanAssemblyForFilters (a, filter_cache);
									   Logger.Log.Debug ("Loaded {0} filter{1} from {2}",
											     n, n == 1 ? "" : "s", a.Location);
								   });

			// FIXME: Up external filter version if external-filters.xml is modified

			// Check if cache is dirty and also update the cache on the disk
			cache_dirty = filter_cache.UpdateCache ();
		}

		/////////////////////////////////////////////////////////////////////////
		

		static private ICollection CreateFilters (Uri uri, string extension, string mime_type)
		{
			SortedList matched_filters_by_flavor = FilterFlavor.NewHashtable ();

			foreach (FilterFlavor flavor in FilterFlavor.Flavors) {
				if (flavor.IsMatch (uri, extension, mime_type)) {
					Filter matched_filter = null;

					try {
						matched_filter = (Filter) Activator.CreateInstance ((Type) FilterFlavor.FilterTable [flavor]);

						if (flavor.MimeType != null)
							matched_filter.MimeType = flavor.MimeType;
						if (flavor.Extension != null)
							matched_filter.Extension = flavor.Extension;

					} catch (Exception e) {
						continue;
					}
					matched_filters_by_flavor [flavor] = matched_filter;
				}
			}

			if (Debug) {
				foreach (DictionaryEntry entry in matched_filters_by_flavor) {
					FilterFlavor flav = (FilterFlavor) entry.Key;
					Filter filter = (Filter) entry.Value;
					
					Logger.Log.Debug ("Found matching filter: {0}, Weight: {1}", filter, flav.Weight);
				}
			}

			return matched_filters_by_flavor.Values;
		}

		static public int GetFilterVersion (string filter_name) 
		{
			if (filter_versions_by_name.ContainsKey (filter_name)) {
				return filter_versions_by_name [filter_name];
			} else {
				return -1;
			}
		}

		/////////////////////////////////////////////////////////////////////////

		static public ICollection CreateFiltersFromMimeType (string mime_type)
		{
			return CreateFilters (null, null, mime_type);
		}

		static public ICollection CreateFilterFromExtension (string extension)
		{
			return CreateFilters (null, extension, null);
		}

		static public ICollection CreateFiltersFromPath (string path)
		{
			string guessed_mime_type = XdgMime.GetMimeType (path);
			string extension = Path.GetExtension (path);
			return CreateFilters (UriFu.PathToFileUri (path), extension, guessed_mime_type);
		}

		static public ICollection CreateFiltersFromUri (Uri uri)
		{
			if (uri.IsFile)
				return CreateFiltersFromPath (uri.LocalPath);
			else
				return CreateFilters (uri, null, null);
		}

		static public ICollection CreateFiltersFromIndexable (Indexable indexable)
		{
			string path = indexable.ContentUri.LocalPath;
			string extension = Path.GetExtension (path);
			string mime_type = indexable.MimeType;
			return CreateFilters (UriFu.PathToFileUri (path), extension, mime_type);
		}

		/////////////////////////////////////////////////////////////////////////

		private static Dictionary<string, int> filter_versions_by_name = new Dictionary<string, int> ();

		static private int ScanAssemblyForFilters (Assembly assembly, FilterCache filter_cache)
		{
			int count = 0;

			foreach (Type t in ReflectionFu.GetTypesFromAssemblyAttribute (assembly, typeof (FilterTypesAttribute))) {
				Filter filter = null;

				try {
					filter = (Filter) Activator.CreateInstance (t);
				} catch (Exception ex) {
					Logger.Log.Error (ex, "Caught exception while instantiating {0}", t);
				}

				if (filter == null)
					continue;

				filter_versions_by_name [t.ToString ()] = filter.Version;

				foreach (FilterFlavor flavor in filter.SupportedFlavors)
					FilterFlavor.FilterTable [flavor] = t;

				++count;
			}

			if (count > 0) {
				DateTime last_mtime = File.GetLastWriteTimeUtc (assembly.Location);
				filter_cache.RegisterFilter (assembly.Location, last_mtime);
			}

			return count;
		}
	}
}
