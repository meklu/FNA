#region License
/* FNA - XNA4 Reimplementation for Desktop Platforms
 * Copyright 2009-2014 Ethan Lee and the MonoGame Team
 *
 * Released under the Microsoft Public License.
 * See LICENSE for details.
 */
#endregion

#region Using Statements
using System;
using System.IO;

using Microsoft.Xna.Framework.Media;
using Microsoft.Xna.Framework.Utilities;
#endregion

namespace Microsoft.Xna.Framework.Content
{
	internal class SongReader : ContentTypeReader<Song>
	{
		#region Private Supported File Extensions Variable

		static string[] supportedExtensions = new string[] { ".flac", ".ogg" };

		#endregion

		#region Internal Filename Normalizer Method

		internal static string Normalize(string fileName)
		{
			return Normalize(fileName, supportedExtensions);
		}

		#endregion

		#region Protected Read Method

		protected internal override Song Read(ContentReader input, Song existingInstance)
		{
			string path = input.ReadString();
			path = Path.Combine(input.ContentManager.RootDirectory, path);
			path = FileHelpers.NormalizeFilePathSeparators(path);

			/* The path string includes the ".wma" extension. Let's see if this
			 * file exists in a format we actually support...
			 */
			path = Normalize(Path.GetFileNameWithoutExtension(path));
			if (String.IsNullOrEmpty(path))
			{
				throw new ContentLoadException();
			}

			int durationMs = input.ReadInt32();

			return new Song(path, durationMs);
		}

		#endregion
	}
}
