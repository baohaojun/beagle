/***************************************************************************
    copyright            : (C) 2005 by Brian Nickel
    email                : brian.nickel@gmail.com
    based on             : id3v2frame.cpp from TagLib
 ***************************************************************************/

/***************************************************************************
 *   This library is free software; you can redistribute it and/or modify  *
 *   it  under the terms of the GNU Lesser General Public License version  *
 *   2.1 as published by the Free Software Foundation.                     *
 *                                                                         *
 *   This library is distributed in the hope that it will be useful, but   *
 *   WITHOUT ANY WARRANTY; without even the implied warranty of            *
 *   MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the GNU     *
 *   Lesser General Public License for more details.                       *
 *                                                                         *
 *   You should have received a copy of the GNU Lesser General Public      *
 *   License along with this library; if not, write to the Free Software   *
 *   Foundation, Inc., 59 Temple Place, Suite 330, Boston, MA  02111-1307  *
 *   USA                                                                   *
 ***************************************************************************/

using System.Collections;
using System;

namespace TagLib.Id3v2
{
   public class CommentsFrame : Frame
   {
      //////////////////////////////////////////////////////////////////////////
      // private properties
      //////////////////////////////////////////////////////////////////////////
      StringType text_encoding;
      ByteVector language;
      string description;
      string text;
      
      
      //////////////////////////////////////////////////////////////////////////
      // public methods
      //////////////////////////////////////////////////////////////////////////
      public CommentsFrame (string description, ByteVector language, StringType encoding) : base ("COMM", 4)
      {
         text_encoding = encoding;
         this.language = language;
         this.description = description;
         text = null;
      }
      
      public CommentsFrame (string description, ByteVector language) : this (description, language, TagLib.Id3v2.Tag.DefaultEncoding)
      {}

      public CommentsFrame (string description) : this (description, null)
      {}

      public CommentsFrame (ByteVector data, uint version) : base (data, version)
      {
         text_encoding = StringType.UTF8;
         language = null;
         description = null;
         text = null;
         SetData (data, 0, version);
      }


      public override string ToString ()
      {
         return text;
      }
      
      public override void SetText (string text)
      {
         this.text = text;
      }
      
      public static CommentsFrame Get (Tag tag, string description, ByteVector language, bool create)
      {
         foreach (Frame f in tag.GetFrames ("COMM"))
         {
            CommentsFrame cf = f as CommentsFrame;
            
            if (cf != null && cf.Description == description && (language == null || language == cf.Language))
               return cf;
         }
         
         if (!create)
            return null;
         
         CommentsFrame frame = new CommentsFrame (description, language);
         tag.AddFrame (frame);
         return frame;
      }
      
      public static CommentsFrame GetPreferred (Tag tag, string description, ByteVector language, bool create)
      {
         // This is weird, so bear with me. The best thing we can have is 
         // something straightforward and in our own language. If it has a 
         // description, then it is probably used for something other than
         // an actual comment. If that doesn't work, we'd still rather have 
         // something in our language than something in another. After that
         // all we have left are things in other languages, so we'd rather 
         // have one with actual content, so we try to get one with no 
         // description first.
         int best_value = -1;
         CommentsFrame best_frame = null;
         
         foreach (Frame f in tag.GetFrames ("COMM"))
         {
            CommentsFrame cf = f as CommentsFrame;
            if (cf == null) continue;
            
            bool same_name = cf.Description == description;
            bool same_lang = cf.Language == language;
            
            if (same_name && same_lang) return cf;
            
            int value = same_lang ? 2 : same_name ? 1 : 0;
            
            if (value <= best_value)
               continue;
            
            best_value = value;
            best_frame = cf;
         }
         
         if (best_frame == null && create)
         {
            best_frame = new CommentsFrame (description, language);
            tag.AddFrame (best_frame);
         }
         return best_frame;
      }
      
      //////////////////////////////////////////////////////////////////////////
      // public properties
      //////////////////////////////////////////////////////////////////////////
      public StringType TextEncoding
      {
         get {return text_encoding;}
         set {text_encoding = value;}
      }

      public ByteVector Language
      {
         get {return language != null ? language : "XXX";}
         set {language = value != null ? value.Mid (0, 3) : "XXX";}
      }
      
      public string Description
      {
         get {return description != null ? description : string.Empty;}
         set {description = value;}
      }
      
      public string Text
      {
         get {return text;}
         set {text = value;}
      }
      
      
      //////////////////////////////////////////////////////////////////////////
      // protected methods
      //////////////////////////////////////////////////////////////////////////
      protected override void ParseFields (ByteVector data, uint version)
      {
         if (data.Count < 4)
            throw new CorruptFileException ("Not enough bytes in field.");
         
         text_encoding = (StringType) data [0];
         language = data.Mid (1, 3);

         string [] split = data.ToStrings (text_encoding, 4, 2);
         
         if (split.Length == 1)
         {
            // Bad comment frame. Assume that it lacks a description.
            description = String.Empty;
            text        = split [0];
         }
         else
         {
            description = split [0];
            text        = split [1];
         }
      }

      protected override ByteVector RenderFields (uint version)
      {
         StringType encoding = CorrectEncoding (TextEncoding, version);
         ByteVector v = new ByteVector ();

         v.Add ((byte) encoding);
         v.Add (Language);
         v.Add (ByteVector.FromString (description, encoding));
         v.Add (TextDelimiter (encoding));
         v.Add (ByteVector.FromString (text, encoding));

         return v;
      }

      protected internal CommentsFrame (ByteVector data, int offset, FrameHeader h, uint version) : base (h)
      {
         text_encoding = StringType.UTF8;
         language = null;
         description = null;
         text = null;
         ParseFields (FieldData (data, offset, version), version);
      }
   }
}
