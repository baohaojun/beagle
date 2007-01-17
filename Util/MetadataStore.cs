using SemWeb;
using SemWeb.Util;
using Mono.Posix;

namespace Beagle.Util {
	public class MetadataStore : MemoryStore
	{
		public static NamespaceManager Namespaces;
		private static MetadataStore descriptions;

		static MetadataStore ()
		{
			Namespaces = new NamespaceManager ();
			
			Namespaces.AddNamespace ("http://ns.adobe.com/photoshop/1.0/", "photoshop");
			Namespaces.AddNamespace ("http://iptc.org/std/Iptc4xmpCore/1.0/xmlns/", "Iptc4xmpCore");
			Namespaces.AddNamespace ("http://purl.org/dc/elements/1.1/", "dc");
			Namespaces.AddNamespace ("http://ns.adobe.com/xap/1.0/", "xmp");
			Namespaces.AddNamespace ("http://ns.adobe.com/xmp/Identifier/qual/1.0", "xmpidq");
			Namespaces.AddNamespace ("http://ns.adobe.com/xap/1.0/rights/", "xmpRights");
			Namespaces.AddNamespace ("http://ns.adobe.com/xap/1.0/bj/", "xmpBJ");
			Namespaces.AddNamespace ("http://ns.adobe.com/xap/1.0/mm/", "xmpMM");
			Namespaces.AddNamespace ("http://ns.adobe.com/exif/1.0/", "exif");
			Namespaces.AddNamespace ("http://ns.adobe.com/tiff/1.0/", "tiff");
			Namespaces.AddNamespace ("http://www.w3.org/1999/02/22-rdf-syntax-ns#", "rdf");
			Namespaces.AddNamespace ("http://www.w3.org/2000/01/rdf-schema#", "rdfs");
		}

		public static MetadataStore Descriptions {
			get {
				if (descriptions == null) {
					descriptions = new MetadataStore ();
					System.IO.Stream stream = System.Reflection.Assembly.GetCallingAssembly ().GetManifestResourceStream ("dces.rdf");
					if (stream != null) {
						descriptions.Import (new RdfXmlReader (stream));
					} else {
						System.Console.WriteLine ("Can't find resource");
					}
				}
				
				return descriptions;
			}
		}

		public void Dump ()
		{
			foreach (SemWeb.Statement stmt in this) {
				System.Console.WriteLine(stmt);
			}

			/*
			XPathSemWebNavigator navi = new XPathSemWebNavigator (this, Namespaces);
			navi.MoveToRoot ();
			navi.MoveToFirstChild ();
			navi.MoveToFirstChild ();
			DumpNode (navi, 0);
			*/

			/* Use the statement writer to filter the messages */
			/*
			foreach (string nspace in Namespaces.GetNamespaces ()) {
				this.Select (new StatementWriter (nspace));
			}
			*/
		}

		public static void AddLiteral (StatementSink sink, string predicate, string type, Literal value)
		{
			Entity empty = new Entity (null);
			Statement top = new Statement ("", (Entity)MetadataStore.Namespaces.Resolve (predicate), empty);
			Statement desc = new Statement (empty, 
							(Entity)MetadataStore.Namespaces.Resolve ("rdf:type"), 
							(Entity)MetadataStore.Namespaces.Resolve (type));
			sink.Add (desc);
			Statement literal = new Statement (empty,
							   (Entity)MetadataStore.Namespaces.Resolve ("rdf:li"),
							   value);
			sink.Add (literal);
			sink.Add (top);
		}

		public static void AddLiteral (StatementSink sink, string predicate, string value)
		{
			Statement stmt = new Statement ((Entity)"", 
							(Entity)MetadataStore.Namespaces.Resolve (predicate), 
							new Literal (value));
			sink.Add (stmt);
		}

		public static void Add (StatementSink sink, string predicate, string type, string [] values)
		{
			Add (sink, new Entity (""), predicate, type, values);
		}

		public static void Add (StatementSink sink, Entity subject, string predicate, string type, string [] values)
		{
			if (values == null) {
				System.Console.WriteLine ("{0} has no values; skipping", predicate);
				return;
			}

			Entity empty = new Entity (null);
			Statement top = new Statement (subject, (Entity)MetadataStore.Namespaces.Resolve (predicate), empty);
			Statement desc = new Statement (empty, 
							(Entity)MetadataStore.Namespaces.Resolve ("rdf:type"), 
							(Entity)MetadataStore.Namespaces.Resolve (type));
			sink.Add (desc);
			foreach (string value in values) {
				Statement literal = new Statement (empty,
								   (Entity)MetadataStore.Namespaces.Resolve ("rdf:li"),
								   new Literal (value, null, null));
				sink.Add (literal);
			}
			sink.Add (top);
		}

		private class StatementWriter : StatementSink 
		{
			string name;
			public StatementWriter (string name)
			{
				this.name = name;
			}

			public bool Add (Statement stmt)
			{
				string predicate = stmt.Predicate.ToString ();

				if (predicate.StartsWith (name))
					System.Console.WriteLine ("----------- {0}", stmt);

				return true;
			}
		}

		private class SelectFirst : StatementSink
		{
			public Statement Statement;

			public bool Add (Statement stmt)
			{
				this.Statement = stmt;
				return false;
			}
		}			

		public void DumpNode (XPathSemWebNavigator navi, int depth)
		{
			do { 
				System.Console.WriteLine ("node [{0}] {1} {2}", depth, navi.Name, navi.Value);
			} while (navi.MoveToNext ());
		}
	       
	}	       
}
