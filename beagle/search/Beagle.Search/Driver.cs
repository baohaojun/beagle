//
// Driver.cs
//
// Copyright (C) 2008 Lukas Lipka <lukaslipka@gmail.com>
//

using System;

using NDesk.DBus;
using org.freedesktop.DBus;
using Mono.Unix;

using Beagle;
using Beagle.Util;

namespace Beagle.Search {

	public class Driver {

		private const string BUS_NAME = "org.gnome.Beagle";
		private const string PATH_NAME = "/org/gnome/Beagle/Search";

		private static bool icon_enabled = false;
		private static bool docs_enabled = false;

		public static void PrintUsageAndExit ()
		{
			VersionFu.PrintHeader ();

			string usage =
				"Usage: beagle-search [OPTIONS] [<query string>]\n\n" +
				"Options:\n" +
				"  --icon\t\t\tAdd an icon to the notification area rather than opening a search window.\n" +
				"  --search-docs\t\t\tAlso search the system-wide documentation index.\n" +
				"  --help\t\t\tPrint this usage message.\n" +
				"  --version\t\t\tPrint version information.\n";

			Console.WriteLine (usage);

			System.Environment.Exit (0);
		}

		private static string ParseArgs (String[] args)
		{
			int i = 0;
			string query = String.Empty;

			while (i < args.Length) {
				switch (args [i]) {
				case "--help":
				case "--usage":
					PrintUsageAndExit ();
					return null;

				case "--version":
					VersionFu.PrintVersion ();
					Environment.Exit (0);
					break;

				case "--icon":
					icon_enabled = true;
					break;

				case "--search-docs":
					docs_enabled = true;
					break;

				// Ignore session management
				case "--sm-config-prefix":
				case "--sm-client-id":
				case "--screen":
					// These all take an argument, so
					// increment i
					i++;
					break;

				default:
					if (args [i].Length < 2 || args [i].Substring (0, 2) != "--") {
						if (query.Length != 0)
							query += " ";
						query += args [i];
					}
					break;
				}

				i++;
			}

			return query;
		}

		public static void Main (string[] args)
		{
			BusG.Init ();

			string query = ParseArgs (args);

			// If there is already an instance of beagle-search running
			// request our search proxy object and open up a query in
			// that instance.

			if (Bus.Session.RequestName (BUS_NAME) != RequestNameReply.PrimaryOwner) {
				if (icon_enabled == true) {
					Console.WriteLine ("There is already an instance of beagle-search running.");
					Console.WriteLine ("Cannot run in --icon mode! Exiting...");
					Environment.Exit (1);
				}

				ISearch s = Bus.Session.GetObject<ISearch> (BUS_NAME, new ObjectPath (PATH_NAME));
				s.Query (query);

				return;
			}

			SystemInformation.SetProcessName ("beagle-search");
			Catalog.Init ("beagle", ExternalStringsHack.LocaleDir);
			
			Gnome.Program program = new Gnome.Program ("search", "0.0", Gnome.Modules.UI, args);
			Gtk.IconTheme.Default.AppendSearchPath (System.IO.Path.Combine (ExternalStringsHack.PkgDataDir, "icons"));

			// FIXME: Passing these icon and docs enabled properties
			// sucks. We really need to do something about them.
			Search search = new Search (icon_enabled, docs_enabled);
			
			if (!String.IsNullOrEmpty (query) || !icon_enabled)
				search.Query (query);

			Bus.Session.Register (BUS_NAME, new ObjectPath (PATH_NAME), search);

			program.Run ();
		}
	}
}
