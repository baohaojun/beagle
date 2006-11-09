//
// Indexable.cs
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
using System.Collections;
using System.IO;
using System.Text;
using System.Threading;
using System.Xml;
using System.Xml.Serialization;
using Beagle.Util;

namespace Beagle {

	public enum IndexableType {
		Add,
		Remove,
		PropertyChange
	}

	public enum IndexableFiltering {
		Never,           // Never try to filter this indexable, it contains no content
		AlreadyFiltered, // The readers promise to return nice clean text, so do nothing
		Automatic,       // Try to determine automatically if this needs to be filtered
		Always           // Always try to filter this indexable
	}

	public class Indexable : Versioned, IComparable {

		static private bool Debug = false;

		// This is the type of indexing operation represented by
		// this Indexable object.  We default to Add, for historical
		// reasons.
		private IndexableType type = IndexableType.Add;

		// The URI of the item being indexed.
		private Uri uri = null;

		// The URI of the parent indexable, if any.
		private Uri parent_uri = null;

		// The URI of the contents to index
		private Uri contentUri = null;

		// The URI of the hot contents to index
		private Uri hotContentUri = null;

		// Whether the content should be deleted after indexing
		private bool deleteContent = false;
		
		// File, WebLink, MailMessage, IMLog, etc.
		private String hit_type = null;

		// If applicable, otherwise set to null.
		private String mimeType = null;

		// The source backend that generated this indexable
		private string source = null;
		
		// List of Property objects
		private ArrayList properties = new ArrayList ();

		// Is this being indexed because of crawling or other
		// background activity?
		private bool crawled = true;

		// Is this object inherently contentless?
		private bool no_content = false;

		// If necessary, should we cache this object's content?
		// The cached version is used to generate snippets.
		private bool cache_content = true;

		// A stream of the content to index
		private TextReader textReader;

		// A stream of the hot content to index
		private TextReader hotTextReader;

		// A stream of binary data to filter
		private Stream binary_stream;

		// When should we try to filter this indexable?
		private IndexableFiltering filtering = IndexableFiltering.Automatic;

		// Local state: these are key/value pairs that never get serialized
		// into XML
		Hashtable local_state = new Hashtable ();

		//////////////////////////

		static private XmlSerializer our_serializer;

		static Indexable ()
		{
			our_serializer = new XmlSerializer (typeof (Indexable));
		}

		//////////////////////////

		public Indexable (IndexableType type,
				  Uri           uri)
		{
			this.type = type;
			this.uri = uri;
			this.hit_type = "File"; // FIXME: Why do we default to this?
		}

		public Indexable (Uri uri) : this (IndexableType.Add, uri)
		{ }

		public Indexable ()
		{
			// Only used when reading from xml
		}

		public static Indexable NewFromXml (string xml)
		{
			StringReader reader = new StringReader (xml);
			return (Indexable) our_serializer.Deserialize (reader);
		}

		//////////////////////////

		[XmlAttribute ("Type")]
		public IndexableType Type {
			get { return type; }
			set { type = value; }
		}

		[XmlIgnore]
		public Uri Uri { 
			get { return uri; }
			set { uri = value; }
		}

		[XmlAttribute ("Uri")]
		public string UriString {
			get { return UriFu.UriToEscapedString (uri); }
			set { uri = UriFu.EscapedStringToUri (value); }
		}

		[XmlIgnore]
		public Uri ParentUri { 
			get { return parent_uri; }
			set { parent_uri = value; }
		}

		[XmlAttribute ("ParentUri")]
		public string ParentUriString {
			get {
				if (parent_uri == null)
					return null;

				return UriFu.UriToEscapedString (parent_uri);
			}

			set {
				if (value == null)
					parent_uri = null;
				else
					parent_uri = UriFu.EscapedStringToUri (value);
			}
		}

		[XmlIgnore]
		public Uri ContentUri {
			get { return contentUri != null ? contentUri : Uri; }
			set { contentUri = value; }
		}

		[XmlAttribute ("ContentUri")]
		public string ContentUriString {
			get { return UriFu.UriToEscapedString (ContentUri); }
			set { contentUri = UriFu.EscapedStringToUri (value); } 
		}

		[XmlIgnore]
		private Uri HotContentUri {
			get { return hotContentUri; }
			set { hotContentUri = value; }
		}
		
		[XmlAttribute ("HotContentUri")]
		public string HotContentUriString {
			get { return HotContentUri != null ? UriFu.UriToEscapedString (HotContentUri) : ""; }
			set { hotContentUri = (value != "") ? UriFu.EscapedStringToUri (value) : null; }
		}

		[XmlIgnore]
		public Uri DisplayUri {
			get { return uri.Scheme == GuidFu.UriScheme ? ContentUri : Uri; }
		}

		[XmlAttribute]
		public bool DeleteContent {
			get { return deleteContent; }
			set { deleteContent = value; }
		}

		[XmlAttribute]
		public String HitType {
			get { return  hit_type; }
			set { hit_type = value; }
		}

		[XmlAttribute]
		public String MimeType {
			get { return mimeType; }
			set { mimeType = value; }
		}

		[XmlAttribute]
		public string Source {
			get { return source; }
			set { source = value; }
		}

		[XmlIgnore]
		public bool IsNonTransient {
			get { return ! DeleteContent && ContentUri.IsFile && ParentUri == null; }
		}

		[XmlAttribute]
		public bool Crawled {
			get { return crawled; }
			set { crawled = value; }
		}

		[XmlAttribute]
		public bool NoContent {
			get { return no_content; }
			set { no_content = value; }
		}

		[XmlAttribute]
		public bool CacheContent {
			get { return cache_content; }
			set { cache_content = value; }
		}

		[XmlAttribute]
		public IndexableFiltering Filtering {
			get { return filtering; }
			set { filtering = value; }
		}

		[XmlIgnore]
		public IDictionary LocalState {
			get { return local_state; }
		}

		//////////////////////////

		public void Cleanup ()
		{
			if (DeleteContent) {
				if (contentUri != null) {
					if (Debug)
						Logger.Log.Debug ("Cleaning up {0}", contentUri.LocalPath);

					try {
						File.Delete (contentUri.LocalPath);
					} catch { 
						// It might be gone already, so catch the exception.
					}

					contentUri = null;
				}

				if (hotContentUri != null) {
					if (Debug)
						Logger.Log.Debug ("Cleaning up {0}", hotContentUri.LocalPath);

					try {
						File.Delete (hotContentUri.LocalPath);
					} catch {
						// Ditto
					}

					hotContentUri = null;
				}
			}
		}

		private Stream StreamFromUri (Uri uri)
		{
			Stream stream = null;

			if (uri != null && uri.IsFile && ! no_content) {
				stream = new FileStream (uri.LocalPath,
							 FileMode.Open,
							 FileAccess.Read,
							 FileShare.Read);
			}

			return stream;
		}

		private TextReader ReaderFromUri (Uri uri)
		{
			Stream stream = StreamFromUri (uri);

			if (stream == null)
				return null;

			return new StreamReader (stream);
		}
				

		public TextReader GetTextReader ()
		{
			if (NoContent)
				return null;

			if (textReader == null)
				textReader = ReaderFromUri (ContentUri);

			return textReader;
		}
		
		public void SetTextReader (TextReader reader)
		{ 
			textReader = reader;
		}

		public TextReader GetHotTextReader ()
		{
			if (NoContent)
				return null;

			if (hotTextReader == null)
				hotTextReader = ReaderFromUri (HotContentUri);
			return hotTextReader;
		}

		public void SetHotTextReader (TextReader reader)
		{
			hotTextReader = reader;
		}

		public Stream GetBinaryStream ()
		{
			if (NoContent)
				return null;

			if (binary_stream == null)
				binary_stream = StreamFromUri (ContentUri);

			return binary_stream;
		}

		public void SetBinaryStream (Stream stream)
		{
			binary_stream = stream;
		}

		[XmlArrayItem (ElementName="Property", Type=typeof (Property))]
		public ArrayList Properties {
			get { return properties; }
		}

		public void AddProperty (Property prop) {
			if (prop != null) {
				
				if (type == IndexableType.PropertyChange && ! prop.IsMutable)
					throw new ArgumentException ("Non-mutable properties aren't allowed in this indexable");

				// If this is a mutable property, make sure that
				// we don't already contain another mutable property
				// with the same name.  If we do, replace it.
				if (prop.IsMutable) {
					for (int i = 0; i < properties.Count; ++i) {
						Property other_prop = properties [i] as Property;
						if (other_prop.IsMutable && prop.Key == other_prop.Key) {
							properties [i] = prop;
							return;
						}
					}
				}

				properties.Add (prop);
			}
		}

		public bool HasProperty (string keyword) {
			foreach (Property property in properties)
				if (property.Key == keyword)
					return true;

			return false;
		}

		// This doesn't check if it makes sense to actually
		// merge the two indexables: it just does it.
		public void Merge (Indexable other)
		{
			this.Timestamp = other.Timestamp;

			foreach (Property prop in other.Properties)
				this.AddProperty (prop);

			foreach (DictionaryEntry entry in other.local_state)
				this.local_state [entry.Key] = entry.Value;
		}

		//////////////////////////

		public void SetChildOf (Indexable parent)
		{
			this.ParentUri = parent.Uri;

			if (!this.ValidTimestamp)
				this.Timestamp = parent.Timestamp;

			// FIXME: Set all of the parent's properties on the
			// child so that we get matches against the child
			// that otherwise would match only the parent, at
			// least until we have proper RDF support.
			foreach (Property prop in parent.Properties) {
				Property new_prop = (Property) prop.Clone ();
				new_prop.Key = "parent:" + new_prop.Key;
				this.AddProperty (new_prop);
			}
		}

		//////////////////////////

		public override string ToString () 
		{
			StringWriter writer = new StringWriter ();
			our_serializer.Serialize (writer, this);
			writer.Close ();
			return writer.ToString ();
		}

		//////////////////////////

		const int BUFFER_SIZE = 8192;

		private static char [] GetCharBuffer ()
		{
			LocalDataStoreSlot slot;
			slot = Thread.GetNamedDataSlot ("Char Buffer");

			object obj;
			char [] buffer;
			obj = Thread.GetData (slot);
			if (obj == null) {
				buffer = new char [BUFFER_SIZE];
				Thread.SetData (slot, buffer);
			} else {
				buffer = (char []) obj; 
			}

			return buffer;
		}

		private static byte [] GetByteBuffer ()
		{
			LocalDataStoreSlot slot;
			slot = Thread.GetNamedDataSlot ("Byte Buffer");

			object obj;
			byte [] buffer;
			obj = Thread.GetData (slot);
			if (obj == null) {
				buffer = new byte [BUFFER_SIZE];
				Thread.SetData (slot, buffer);
			} else {
				buffer = (byte []) obj; 
			}

			return buffer;
		}

		//////////////////////////

		private static Uri TextReaderToTempFileUri (TextReader reader)
		{
			if (reader == null)
				return null;

			string filename = Path.GetTempFileName ();
			FileStream fileStream = File.OpenWrite (filename);

			// When we dump the contents of an indexable into a file, we
			// expect to use it again soon.
			FileAdvise.PreLoad (fileStream);

			// Make sure the temporary file is only readable by the owner.
			// FIXME: There is probably a race here.  Could some malicious program
			// do something to the file between creation and the chmod?
			Mono.Unix.Native.Syscall.chmod (filename, (Mono.Unix.Native.FilePermissions) 256);

			BufferedStream bufferedStream = new BufferedStream (fileStream);
			StreamWriter writer = new StreamWriter (bufferedStream);


			char [] buffer;
			buffer = GetCharBuffer ();

			int read;
			do {
				read = reader.Read (buffer, 0, buffer.Length);
				if (read > 0)
					writer.Write (buffer, 0, read);
			} while (read > 0);
			
			writer.Close ();

			return UriFu.PathToFileUri (filename);
		}

		private static Uri BinaryStreamToTempFileUri (Stream stream)
		{
			if (stream == null)
				return null;

			string filename = Path.GetTempFileName ();
			FileStream fileStream = File.OpenWrite (filename);

			// When we dump the contents of an indexable into a file, we
			// expect to use it again soon.
			FileAdvise.PreLoad (fileStream);

			// Make sure the temporary file is only readable by the owner.
			// FIXME: There is probably a race here.  Could some malicious program
			// do something to the file between creation and the chmod?
			Mono.Unix.Native.Syscall.chmod (filename, (Mono.Unix.Native.FilePermissions) 256);

			BufferedStream bufferedStream = new BufferedStream (fileStream);

			byte [] buffer;
			buffer = GetByteBuffer ();

			int read;
			do {
				read = stream.Read (buffer, 0, buffer.Length);
				if (read > 0)
					bufferedStream.Write (buffer, 0, read);
			} while (read > 0);

			bufferedStream.Close ();

			return UriFu.PathToFileUri (filename);
		}

		public void StoreStream () {
			if (textReader != null) {
				ContentUri = TextReaderToTempFileUri (textReader);

				if (Debug)
					Logger.Log.Debug ("Storing text content from {0} in {1}", Uri, ContentUri);

				DeleteContent = true;
			} else if (binary_stream != null) {
				ContentUri = BinaryStreamToTempFileUri (binary_stream);

				if (Debug)
					Logger.Log.Debug ("Storing binary content from {0} in {1}", Uri, ContentUri);

				DeleteContent = true;
			}

			if (hotTextReader != null) {
				HotContentUri = TextReaderToTempFileUri (hotTextReader);

				if (Debug)
					Logger.Log.Debug ("Storing hot content from {0} in {1}", Uri, HotContentUri);

				DeleteContent = true;
			}
		}

		//////////////////////////

		public override int GetHashCode ()
		{
			return (uri != null ? uri.GetHashCode () : 0) ^ type.GetHashCode ();
		}

		public int CompareTo (object obj)
		{
			Indexable other = (Indexable) obj;
			return DateTime.Compare (this.Timestamp, other.Timestamp);
		}
	}
}
