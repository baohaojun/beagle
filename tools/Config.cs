//
// Config.cs
//
// Copyright (C) 2005 Novell, Inc.
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
using System.IO;
using System.Diagnostics;
using System.Collections;
using System.Collections.Specialized;
using System.Reflection;

using Beagle;
using Beagle.Daemon;
using Beagle.Util;

//using Gtk;
using GLib;

// Assembly information
[assembly: AssemblyTitle ("beagle-config")]
[assembly: AssemblyDescription ("Command-line interface to the Beagle config file")]

public static class ConfigTool {

	private static void PrintUsageAndExit ()
	{
		VersionFu.PrintHeader ();

		string usage =
			"Usage: beagle-config [OPTIONS]\n" +
			"   or: beagle-config <SECTION>\n" +
			"   or: beagle-config <SECTION> <SECTIONOPTION> [PARAMS]\n\n" +
			"Options:\n" +
			"  --beagled-reload-config\tAsk the beagle daemon to reload\n" +
			"                         \tthe configuration file.\n" +
			"  --list-sections\t\tList all available configuration sections.\n" +
			"  --help\t\t\tPrint this usage message.\n" +
			"  --version\t\t\tPrint version information.\n\n" +
			"If a section is specified with no options, then a list of the available commands for that section is shown.\n";

		Console.WriteLine (usage);

		System.Environment.Exit (0);
	}

	private static void ListSectionsAndExit ()
	{
		Console.WriteLine ("Available configuration sections: ");
		foreach (string key in Conf.Sections.Keys)
			Console.WriteLine (" - {0}", key);

		System.Environment.Exit (0);
	}
	
	private static void ListSectionOptionsAndExit (string sectionname, Hashtable options)
	{
		Console.WriteLine ("Available options for section '{0}':", sectionname);
		foreach (string key in options.Keys) {
			Console.Write (" - {0}", key);
			if (options [key] != null)
				Console.Write (" ({0})", options [key]);

			Console.WriteLine ();
		}
		if (sectionname == "daemon")
			Console.WriteLine (" - ListBackends (List enabled and disabled backends)");
		
		System.Environment.Exit (0);
	}

	private static void ReloadConfigAndExit ()
	{
		try {
			ReloadConfigRequest request = new ReloadConfigRequest ();
			request.Send ();
			Console.WriteLine ("ReloadConfig request was sent successfully.");
			System.Environment.Exit (0);
		} catch (Exception e) {
			Console.Error.WriteLine ("ERROR: Could not send ReloadConfig request: {0}", e.Message);
			System.Environment.Exit (-1);
		}
	}
		
	public static void Main (string [] args)
	{
		if (args.Length == 0)
			PrintUsageAndExit ();

		int i = 0;
		while (i < args.Length) {
			switch (args [i]) {
			case "--list-sections":
				Conf.Load ();
				ListSectionsAndExit ();
				return;

			case "--reload":
			case "--beagled-reload-config":
				ReloadConfigAndExit ();
				return;

			case "--help":
			case "--usage":
				PrintUsageAndExit ();
				return;

			case "--version":
				VersionFu.PrintVersion ();
				Environment.Exit (0);
				break;

			default:
				break;
			}
			++i;
		}

		Conf.Load ();

		string sectionname = args [0];

		if (! Conf.Sections.ContainsKey (sectionname)) {
			Console.Error.WriteLine ("ERROR: Invalid section name '{0}'", sectionname);
			Environment.Exit (-1);
		}

		Conf.Section section = (Conf.Section) Conf.Sections [sectionname];
		Hashtable options = Conf.GetOptions (section);

		// No option specified?
		if (args.Length == 1)
			ListSectionOptionsAndExit (sectionname, options);
		
		string optionname = args [1];
		if (! options.ContainsKey (optionname)) {
			if (sectionname == "daemon" && optionname == "ListBackends") {
				ListBackends ();
				Environment.Exit (0);
			} else {
				Console.Error.WriteLine ("ERROR: Invalid option name '{0}'", optionname);
				Environment.Exit (-2);
			}
		}

		//
		// Invoke the method the user has chosen
		//

		// Pack the remaining command line params into an array used for
		// params of the method.
		string [] optionparams = new string [args.Length - 2];
		int j, k;
		for (j = 0, k = 2; k < args.Length; j++, k++)
			optionparams [j] = args [k];

		// Invoke the method
		string output = null;
		bool result = false;

		try {
			result = Conf.InvokeOption (section, optionname, optionparams, out output);
		} catch (Exception e) {
			Console.Error.WriteLine("ERROR: Command execution failed - caught exception.");
			Console.Error.WriteLine(e.Message);
			Environment.Exit (-3);
		}

		// Read the result and show the output
		if (result == true)
			Console.WriteLine (output);
		else {
			Console.Error.WriteLine ("ERROR: Command execution failed.");
			Console.Error.WriteLine (output);
			Environment.Exit (-4);
		}

		Conf.Save ();
		Environment.Exit (0);

	}

	private static void ListBackends ()
	{
		ArrayList backends = new ArrayList ();

		ArrayList assemblies = ReflectionFu.ScanEnvironmentForAssemblies ("BEAGLE_BACKEND_PATH", PathFinder.BackendDir);

		// Add BeagleDaemonLib if it hasn't already been added.
		bool found_daemon_lib = false;
		foreach (Assembly assembly in assemblies) {
			if (assembly.GetName ().Name == "BeagleDaemonLib") {
				found_daemon_lib = true;
				break;
			}
		}

		if (!found_daemon_lib) {
			try {
				assemblies.Add (Assembly.LoadFrom (Path.Combine (PathFinder.PkgLibDir, "BeagleDaemonLib.dll")));
			} catch (FileNotFoundException) {
				Console.WriteLine ("WARNING: Could not find backend list.");
				Environment.Exit (1);
			}
		}

		foreach (Assembly assembly in assemblies) {
			foreach (Type type in ReflectionFu.GetTypesFromAssemblyAttribute (assembly, typeof (IQueryableTypesAttribute))) {
				foreach (Beagle.Daemon.QueryableFlavor flavor in ReflectionFu.ScanTypeForAttribute (type, typeof (Beagle.Daemon.QueryableFlavor)))
					backends.Add (flavor.Name);
			}
		}
		
		if ( Directory.Exists (PathFinder.SystemIndexesDir)) {
			foreach (DirectoryInfo index_dir in new DirectoryInfo (PathFinder.SystemIndexesDir).GetDirectories ())
				backends.Add (index_dir.Name);
		}

		bool found_any = false;

		Console.WriteLine ("Allowed backends:");
		foreach (string name in backends) {
			if (Conf.Daemon.DeniedBackends.Contains (name))
				continue;
			Console.WriteLine (" - {0}", name);
			found_any = true;
		}

		if (! found_any)
			Console.WriteLine (" (none)");
		
		Console.WriteLine ();

		found_any = false;

		Console.WriteLine ("Denied backends:");
		foreach (string name in Conf.Daemon.DeniedBackends) {
			Console.WriteLine (" - {0}", name);
			found_any = true;
		}

		if (! found_any)
			Console.WriteLine (" (none)");
	}

}
