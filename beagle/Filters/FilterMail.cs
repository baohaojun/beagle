
//
// FilterMail.cs
//
// Copyright (C) 2004-2005 Novell, Inc.
//
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
using MimeKit;

using Beagle;
using Beagle.Daemon;
using Beagle.Util;

namespace Beagle.Filters {

	public class FilterMail : Beagle.Daemon.Filter, IDisposable {

		private static bool snippet_attachment = false;

		private MimeMessage message;
		private PartHandler handler;

		public FilterMail ()
		{
			// 1: Make email addresses non-keyword, add sanitized version
			//    for eaching for parts of an email address.
			// 2: No need to separately add sanitized version of emails.
			//    BeagleAnalyzer uses a tokenfilter taking care of this.
			// 3: Add standard file properties to attachments.
			// 4: Store snippets
			// 5: Store mailing list id as tokenized
			// 6: Store snippets for attachments - this is controlled separately
			//    since attachments can often be huge and take up a lot of space.
			SetVersion (6);

			SnippetMode = true;
			SetFileType ("mail");
		}

		protected override void RegisterSupportedTypes ()
		{
			AddSupportedFlavor (FilterFlavor.NewFromMimeType ("message/rfc822"));

			// Add the list of user requested maildir directories
			// This is useful if beagle (xdgmime) does not correctly detect the mimetypes
			// of several maildir files as message/rfc822

			List<string[]> values = Conf.Daemon.GetListOptionValues (Conf.Names.Maildirs);
			if (values == null)
				return;

			foreach (string[] maildir in values) {
				FilterFlavor flavor =
					new FilterFlavor (new Uri (maildir [0] + "/*").ToString (),
							  null, // No meaning of extension for maildir files
							  null,
							  1 /* Should be more than priority of text filter */);
				AddSupportedFlavor (flavor);
			}

			snippet_attachment = Conf.Daemon.GetOption(Conf.Names.EnableEmailAttachmentSnippet, false);
		}

		protected override void DoOpen (FileInfo info)
		{
			FileStream stream = new FileStream (info.FullName, FileMode.Open, FileAccess.Read);

			this.message = MimeMessage.Load (stream);
			stream.Dispose ();

			if (this.message == null)
				Error ();
		}

		private bool HasAttachments ()
		{
			int i = 0;
			foreach (var part in this.message.Attachments) {
				i++;
			}
			return i > 0;
		}

		protected override void DoPullProperties ()
		{
			string subject = this.message.Subject;
			AddProperty (Property.New ("dc:title", subject));

			AddProperty (Property.NewDate ("fixme:date", message.Date.DateTime));

			InternetAddressList addrs;
			addrs = this.message.To;
			foreach (InternetAddress ia in addrs) {
				AddProperty (Property.NewUnsearched ("fixme:to", ia.ToString (false)));
				if (ia is MailboxAddress) {
					MailboxAddress ma = ia as MailboxAddress;
					AddProperty (Property.New ("fixme:to_address", ma.Address));
				}
				AddProperty (Property.New ("fixme:to_name", ia.Name));
				AddEmailLink (ia);
			}

			addrs = this.message.Cc;
			foreach (InternetAddress ia in addrs) {
				AddProperty (Property.NewUnsearched ("fixme:cc", ia.ToString (false)));
				if (ia is MailboxAddress) {
					var mailbox = ia as MailboxAddress;
					AddProperty (Property.New ("fixme:cc_address", mailbox.Address));
				}

				AddProperty (Property.New ("fixme:cc_name", ia.Name));
				AddEmailLink (ia);
			}

			addrs = this.message.From;
			foreach (InternetAddress ia in addrs) {
				AddProperty (Property.NewUnsearched ("fixme:from", ia.ToString (false)));
				if (ia is MailboxAddress) {
					var mailbox = ia as MailboxAddress;
					AddProperty (Property.New ("fixme:from_address", mailbox.Address));
				}

				AddProperty (Property.New ("fixme:from_name", ia.Name));
				AddEmailLink (ia);
			}

			if (HasAttachments ())
				AddProperty (Property.NewFlag ("fixme:hasAttachments"));

			// Store the message ID and references are unsearched
			// properties.  They will be used to generate
			// conversations in the frontend.
			string msgid = this.message.Headers ["Message-Id"];
		        if (msgid != null)
				AddProperty (Property.NewUnsearched ("fixme:msgid", msgid));

			foreach (var refs in this.message.References)
				AddProperty (Property.NewUnsearched ("fixme:reference", refs));

			string list_id = this.message.Headers ["List-Id"];
			if (list_id != null)
				AddProperty (Property.New ("fixme:mlist", list_id));
		}

		private void AddEmailLink (InternetAddress ia)
		{
#if ENABLE_RDF_ADAPTER
			if (String.IsNullOrEmpty (ia.Name))
				AddLink (String.Concat ("mailto://", ia.Addr));
			else
				AddLink (String.Concat ("mailto://", ia.Addr, "/", Uri.EscapeDataString (ia.Name)));
#endif
		}

		protected override void DoPullSetup ()
		{
			this.handler = new PartHandler (Indexable,
							delegate (string s)
							{
								AddLink (s);
							});

			int n_parts = 0;
			foreach (MimePart mime_part in this.message.BodyParts) {
				n_parts++;
			}

			foreach (MimePart mime_part in this.message.BodyParts) {
				this.handler.OnEachPart (mime_part, n_parts);
			}

			AddIndexables (this.handler.ChildIndexables);
		}

		protected override void DoPull ()
		{
			if (handler.Reader == null) {
				Finished ();
				return;
			}

			if (handler.HtmlPart) {
				DoPullingReaderPull ();
				return;
			}

			string l = null;
			try {
				l = handler.Reader.ReadLine ();
			} catch (IOException e) {
			}

			if (l == null)
				Finished ();
			else if (l.Length > 0) {
				AppendText (l);
				AppendStructuralBreak ();
			}
		}

		char[] pulling_reader_buf = null;
		const int BUFSIZE = 1024;
		private void DoPullingReaderPull ()
		{
			if (pulling_reader_buf == null)
				pulling_reader_buf = new char [BUFSIZE];

			int count = handler.Reader.Read (pulling_reader_buf, 0, BUFSIZE);
			if (count == 0) {
				Finished ();
				pulling_reader_buf = null;
				return;
			}

			AppendChars (pulling_reader_buf, 0, count);
		}

		protected override void DoClose ()
		{
			Dispose ();
		}
		
		public void Dispose ()
		{
			if (this.handler != null && this.handler.Reader != null)
				this.handler.Reader.Close ();

			this.handler = null;

			if (this.message != null) {
				this.message = null;
			}
		}

		private class PartHandler {
			private Indexable indexable;
			private int count = 0; // parts handled so far
			private ArrayList child_indexables = new ArrayList ();
			private TextReader reader;
			private FilterHtml.AddLinkCallback link_handler;

			private bool html_part = false;
			internal bool HtmlPart {
				get { return html_part; }
			}

			// Blacklist a handful of common MIME types that are
			// either pointless on their own or ones that we don't
			// have filters for.
			static private string[] blacklisted_mime_types = new string[] {
				"application/pgp-signature",
				"application/x-pkcs7-signature",
				"application/ms-tnef",
				"text/x-vcalendar",
				"text/x-vcard"
			};

			public PartHandler (Indexable parent_indexable, FilterHtml.AddLinkCallback link_handler)
			{
				this.indexable = parent_indexable;
				this.link_handler = link_handler;
			}

			private bool IsMimeTypeHandled (string mime_type)
			{
				foreach (FilterFlavor flavor in FilterFlavor.Flavors) {
					if (flavor.IsMatch (null, null, mime_type.ToLower ()))
						return true;
				}

				return false;
			}

			public void OnEachPart (MimePart part, int n_parts)
			{
				if (part != null) {
					string mime_type = part.ContentType.MimeType.ToString().ToLower();
					System.IO.Stream stream = null;
					stream = part.ContentObject.Open();

					// If this is the only part and it's plain text, we
					// want to just attach it to our filter instead of
					// creating a child indexable for it.
					bool no_child_needed = false;

					if (n_parts == 1) {
						if (mime_type == "text/plain") {
							no_child_needed = true;

							this.reader = new StreamReader (stream);
						} else if (mime_type == "text/html") {
							no_child_needed = true;
							html_part = true;
							string enc = part.ContentType.Parameters ["charset"];
							// DataWrapper.Stream is a very limited stream
							// and does not allow Seek or Tell
							// HtmlFilter requires Stream.Position=0.
							// Play safe and create a memorystream
							// for HTML parsing.

							MemoryStream mem_stream = new MemoryStream();

							part.ContentObject.WriteTo(mem_stream);
							stream.Close();

							mem_stream.Seek (0, SeekOrigin.Begin);

							try {
								this.reader = FilterHtml.GetHtmlReader (mem_stream, enc, link_handler);
							} catch (Exception e) {
								Log.Debug (e, "Exception while filtering HTML email {0}", this.indexable.Uri);
								this.reader = null;
								mem_stream.Close ();
								html_part = false;
							}
						}
					}

					if (!no_child_needed) {
						// Check the mime type against the blacklist and don't index any
						// parts that are contained within.  That way the user doesn't
						// get flooded with pointless signatures and vcard and ical
						// attachments along with (real) attachments.

						if (Array.IndexOf (blacklisted_mime_types, mime_type) == -1) {
							string sub_uri = "#" + this.count;
							Indexable child;
							child = new Indexable (UriFu.AddFragment (this.indexable.Uri, sub_uri, true));

							child.DisplayUri = new Uri (this.indexable.DisplayUri.ToString () + "#" + this.count);

							// This is a special case.
							// Even for mails found on disk, MailMessage hitype is set
							child.HitType = "MailMessage";
							child.MimeType = mime_type;

							// If this is the richest part we found for multipart emails, add its content to textcache
							if (snippet_attachment || n_parts == 1)
								child.CacheContent = true;
							else
								child.CacheContent = false;

							string filename = part.FileName;

							if (! String.IsNullOrEmpty (filename)) {
								child.AddProperty (Property.NewKeyword ("fixme:attachment_title", filename));

								foreach (Property prop in Property.StandardFileProperties (filename, false))
									child.AddProperty (prop);
							}

							// Store length of attachment

							if (part.ContentType.MediaType.ToLower () == "text")
								child.SetTextReader (new StreamReader (stream));
							else
								child.SetBinaryStream (stream);

							child.SetChildOf (this.indexable);
							child.StoreStream ();
							child.CloseStreams ();
							this.child_indexables.Add (child);
						} else {
							Log.Debug ("Skipping attachment {0}#{1} with blacklisted mime type {2}",
								   this.indexable.Uri, this.count, mime_type);
						}
					}

					this.count++;
				}

			}

			public ICollection ChildIndexables {
				get { return this.child_indexables; }
			}

			public TextReader Reader {
				get { return this.reader; }
			}
		}

	}

}
