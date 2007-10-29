//
// FileNameFilter.cs
//
// Copyright (C) 2004, 2005 Novell, Inc.
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
using System.Text.RegularExpressions;
using System.IO;

using Beagle.Util;

namespace Beagle.Daemon.FileSystemQueryable {

	public class FileNameFilter {

		private FileSystemQueryable queryable;

		private static bool Debug = false;
		
		// User defined paths to exclude
		private ArrayList exclude_paths = new ArrayList ();
		
		// User defined exclude patterns
		private ArrayList exclude_patterns = new ArrayList ();
		private Dictionary<string, ExcludeItem> exclude_patterns_table = new Dictionary<string, ExcludeItem> ();

		// Our default exclude patterns
		private ArrayList exclude_patterns_default = new ArrayList ();

		/////////////////////////////////////////////////////////////

		// Setup our default exclude patterns.

		private void AddDefaultPatternToIgnore (IEnumerable patterns)
		{
			foreach (string pattern in patterns)
				exclude_patterns_default.Add (new ExcludeItem (ExcludeType.Pattern, pattern));
		}
		
		/////////////////////////////////////////////////////////////

		private void AddExclude (string value, bool is_pattern)
		{
			if (String.IsNullOrEmpty (value))
				return;

			if (Debug)
				Logger.Log.Debug ("FileNameFilter: Adding ExcludeItem (value={0}, type={1})", value, (is_pattern ? "Pattern" : "Path"));

			if (! is_pattern) {
				exclude_paths.Add (value);
				queryable.RemoveDirectory (value);
			} else {
				exclude_patterns.Add (value);
				exclude_patterns_table.Add (value, new ExcludeItem (ExcludeType.Pattern, value));
			}
		}

		private void RemoveExclude (string value, bool is_pattern)
		{
			if (Debug)
				Logger.Log.Debug ("FileNameFilter: Removing ExcludeItem (value={0}, type={1})", value, (is_pattern ? "Pattern" : "Path"));

			if (! is_pattern) {
				exclude_paths.Remove (value);
			} else {
				exclude_patterns.Remove (value);
				exclude_patterns_table.Remove (value);
			}
		}
		
		/////////////////////////////////////////////////////////////

		public FileNameFilter (FileSystemQueryable queryable)
		{
			this.queryable = queryable;

			LoadConfiguration ();
		}

		/////////////////////////////////////////////////////////////

		// Load data from configuration. Intersect deltas to the currently active excludes and
		// implement any changes upon notification.

		private void LoadConfiguration () 
		{
			Config config = Conf.Get (Conf.Names.FilesQueryableConfig);

			List<string[]> values = config.GetListOptionValues (Conf.Names.ExcludeSubdirectory);
			if (values != null) {
				foreach (string[] exclude in values) {
					// Excluded subdirectories can use environment variables
					// like $HOME/tmp
					string expanded_exclude = StringFu.ExpandEnvVariables (exclude [0]);
					if (expanded_exclude != null) {
						expanded_exclude = Path.GetFullPath (expanded_exclude);
						if (Directory.Exists (expanded_exclude))
							AddExclude (expanded_exclude, false);
					}
				}
			}

			values = config.GetListOptionValues (Conf.Names.ExcludePattern);
			if (values != null)
				foreach (string[] exclude in values)
					// RemoveQuotes from beginning and end
					AddExclude (exclude [0], true);

			Conf.WatchForUpdates ();
			Conf.Subscribe (Conf.Names.FilesQueryableConfig, OnConfigurationChanged);
		}

		private void OnConfigurationChanged (Config config)
		{
			if (config == null || config.Name != Conf.Names.FilesQueryableConfig)
				return;

			ArrayList exclude_paths_removed = new ArrayList ();
			bool clear_fs_state = false;

			List<string[]> values = config.GetListOptionValues (Conf.Names.ExcludeSubdirectory);
			if (values != null) {
				ArrayList subdirs = new ArrayList (values.Count);
				foreach (string[] value in subdirs)
					subdirs.Add (value [0]);

				IList excludes_wanted = subdirs;
				IList excludes_to_add, excludes_to_remove;

				ArrayFu.IntersectListChanges (excludes_wanted, 
							      exclude_paths, 
						      	      out excludes_to_add, 
						      	      out excludes_to_remove);

				// Process any excludes we think we should remove
				foreach (string path in excludes_to_remove) {
					exclude_paths_removed.Add (path);
					RemoveExclude (path, false);
				}

				// Process any excludes we found to be new
				foreach (string path in excludes_to_add)
					AddExclude (path, true);
			}

			values = config.GetListOptionValues (Conf.Names.ExcludePattern);
			if (values != null) {
				ArrayList patterns = new ArrayList (values.Count);
				foreach (string[] value in patterns)
					patterns.Add (value [0]);

				IList excludes_wanted = patterns;
				IList excludes_to_add, excludes_to_remove;

				ArrayFu.IntersectListChanges (excludes_wanted, 
							      exclude_patterns,
						      	      out excludes_to_add, 
						      	      out excludes_to_remove);

				// Process any excludes we think we should remove
				foreach (string pattern in excludes_to_remove) {
					clear_fs_state = true;
					RemoveExclude (pattern, true);
				}

				// Process any excludes we found to be new
				foreach (string pattern in excludes_to_add)
					AddExclude (pattern, true);
			}

			// If an exclude pattern is removed, we need to recrawl everything
			// so that we can index those files which were previously ignored.
			if (clear_fs_state)
				queryable.RecrawlEverything ();

			// Make sure we re-crawl the paths we used to ignored but
			// no longer do.
			foreach (string path in exclude_paths_removed) 
				queryable.Recrawl (path);
		}

		/////////////////////////////////////////////////////////////

		// Try to match any of our current excludes to determine if 
		// we should ignore a file/directory or not.

		public bool Ignore (DirectoryModel parent, string name, bool is_directory) 
		{
			if (Debug)
				Logger.Log.Debug ("*** Ignore Check (parent={0}, name={1}, is_directory={2})", (parent != null) ? parent.FullName : null, name, is_directory);

			// If parent is null, we have a root. But it might not be
			// active anymore so we need to check if it's still in the list.
			if (parent == null && queryable.Roots.Contains (name)) {
				if (Debug)
					Logger.Log.Debug ("*** Ignore Check Passed");
				return false;
			}
			
			string path;
			if (parent != null)
				path = Path.Combine (parent.FullName, name);
			else
				path = name;
			
			// Exclude paths
			foreach (string exclude in exclude_paths)
				if (path.StartsWith (exclude))
					return true;
			
			// Exclude patterns
			foreach (string pattern in exclude_patterns_table.Keys)
				if (exclude_patterns_table [pattern].IsMatch (name))
					return true;
			
			// Default exclude patterns
			//foreach (ExcludeItem exclude in exclude_patterns_default)
			//	if (exclude.IsMatch (name))
			//		return true;
			
			if (parent == null) {
				if (Debug)
					Logger.Log.Debug ("*** Parent is null (name={0}, is_directory={1}", name, is_directory);
				return false;
			}

			// This is kind of a hack, but if parent.Parent is null, we need to pass
			// the full path of the directory as second argument to Ignore to allow
			// us to do the root check.
			return Ignore (parent.Parent, (parent.Parent == null) ? parent.FullName : parent.Name, true);
		}
	}		
}
