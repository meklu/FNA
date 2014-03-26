#region License
/* FNA - XNA4 Reimplementation for Desktop Platforms
 * Copyright 2009-2014 Ethan Lee and the MonoGame Team
 *
 * Released under the Microsoft Public License.
 * See LICENSE for details.
 */

/*
MIT License
Copyright (c) 2006 The Mono.Xna Team

All rights reserved.

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
*/
#endregion

#region Using Statements
using System;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Collections.Generic;
#endregion

namespace Microsoft.Xna.Framework.Content
{
	public sealed class ContentTypeReaderManager
	{
		#region Private Variables

		private ContentReader _reader;
		private ContentTypeReader[] contentReaders;
		private static string assemblyName;

		// Trick to prevent the linker removing the code, but not actually execute the code
		private static bool falseflag = false;
		/* Static map of type names to creation functions. Required as iOS requires all
		 * types at compile time
		 */
		private static Dictionary<string, Func<ContentTypeReader>> typeCreators =
			new Dictionary<string, Func<ContentTypeReader>>();

		#endregion

		#region Private Static Constructor

		static ContentTypeReaderManager()
		{
			assemblyName = Assembly.GetExecutingAssembly().FullName;
		}

		#endregion

		#region Public Constructors

		public ContentTypeReaderManager(ContentReader reader)
		{
			_reader = reader;
		}

		#endregion

		#region Public Methods

		public ContentTypeReader GetTypeReader(Type targetType)
		{
			foreach (ContentTypeReader r in contentReaders)
			{
				if (targetType == r.TargetType)
				{
					return r;
				}
			}
			return null;
		}

		#endregion

		#region Internal Death Defying Method

		internal ContentTypeReader[] LoadAssetReaders()
		{
#pragma warning disable 0219, 0649
			/* Trick to prevent the linker removing the code, but not actually execute the code
			 * FIXME: Do we really need this in FNA?
			 */
			if (falseflag)
			{
				/* Dummy variables required for it to work on iDevices ** DO NOT DELETE **
				 * This forces the classes not to be optimized out when deploying to iDevices
				 */
				ByteReader hByteReader = new ByteReader();
				SByteReader hSByteReader = new SByteReader();
				DateTimeReader hDateTimeReader = new DateTimeReader();
				DecimalReader hDecimalReader = new DecimalReader();
				BoundingSphereReader hBoundingSphereReader = new BoundingSphereReader();
				BoundingFrustumReader hBoundingFrustumReader = new BoundingFrustumReader();
				RayReader hRayReader = new RayReader();
				ListReader<char> hCharListReader = new ListReader<Char>();
				ListReader<Rectangle> hRectangleListReader = new ListReader<Rectangle>();
				ArrayReader<Rectangle> hRectangleArrayReader = new ArrayReader<Rectangle>();
				ListReader<Vector3> hVector3ListReader = new ListReader<Vector3>();
				ListReader<StringReader> hStringListReader = new ListReader<StringReader>();
				ListReader<int> hIntListReader = new ListReader<Int32>();
				SpriteFontReader hSpriteFontReader = new SpriteFontReader();
				Texture2DReader hTexture2DReader = new Texture2DReader();
				CharReader hCharReader = new CharReader();
				RectangleReader hRectangleReader = new RectangleReader();
				StringReader hStringReader = new StringReader();
				Vector2Reader hVector2Reader = new Vector2Reader();
				Vector3Reader hVector3Reader = new Vector3Reader();
				Vector4Reader hVector4Reader = new Vector4Reader();
				CurveReader hCurveReader = new CurveReader();
				IndexBufferReader hIndexBufferReader = new IndexBufferReader();
				BoundingBoxReader hBoundingBoxReader = new BoundingBoxReader();
				MatrixReader hMatrixReader = new MatrixReader();
				BasicEffectReader hBasicEffectReader = new BasicEffectReader();
				VertexBufferReader hVertexBufferReader = new VertexBufferReader();
				AlphaTestEffectReader hAlphaTestEffectReader = new AlphaTestEffectReader();
				EnumReader<Microsoft.Xna.Framework.Graphics.SpriteEffects> hEnumSpriteEffectsReader = new EnumReader<Graphics.SpriteEffects>();
				ArrayReader<float> hArrayFloatReader = new ArrayReader<float>();
				ArrayReader<Vector2> hArrayVector2Reader = new ArrayReader<Vector2>();
				ListReader<Vector2> hListVector2Reader = new ListReader<Vector2>();
				ArrayReader<Matrix> hArrayMatrixReader = new ArrayReader<Matrix>();
				EnumReader<Microsoft.Xna.Framework.Graphics.Blend> hEnumBlendReader = new EnumReader<Graphics.Blend>();
				NullableReader<Rectangle> hNullableRectReader = new NullableReader<Rectangle>();
				EffectMaterialReader hEffectMaterialReader = new EffectMaterialReader();
				ExternalReferenceReader hExternalReferenceReader = new ExternalReferenceReader();
				SoundEffectReader hSoundEffectReader = new SoundEffectReader();
				SongReader hSongReader = new SongReader();
			}
#pragma warning restore 0219, 0649
			int numberOfReaders;
			// The first content byte i read tells me the number of content readers in this XNB file
			numberOfReaders = _reader.Read7BitEncodedInt();
			contentReaders = new ContentTypeReader[numberOfReaders];
			/* For each reader in the file, we read out the length of the string which contains the type of the reader,
			 * then we read out the string. Finally we instantiate an instance of that reader using reflection
			 */
			for (int i = 0; i < numberOfReaders; i += 1)
			{
				/* This string tells us what reader we need to decode the following data
				 * string readerTypeString = reader.ReadString();
				 */
				string originalReaderTypeString = _reader.ReadString();
				Func<ContentTypeReader> readerFunc;
				if (typeCreators.TryGetValue(originalReaderTypeString, out readerFunc))
				{
					contentReaders[i] = readerFunc();
				}
				else
				{
					// Need to resolve namespace differences
					string readerTypeString = originalReaderTypeString;
					readerTypeString = PrepareType(readerTypeString);
					Type l_readerType = Type.GetType(readerTypeString);
					if (l_readerType != null)
					{
						try
						{
							contentReaders[i] = l_readerType.GetDefaultConstructor().Invoke(null) as ContentTypeReader;
						}
						catch (TargetInvocationException ex)
						{
							/* If you are getting here, the Mono runtime is most likely not able to JIT the type.
							 * In particular, MonoTouch needs help instantiating types that are only defined in strings in Xnb files.
							 */
							throw new InvalidOperationException(
								"Failed to get default constructor for ContentTypeReader.\n" +
								"To work around, add a creation function to\n" +
								"ContentTypeReaderManager.AddTypeCreator() with\n" +
								"the following failed type string:\n" +
								originalReaderTypeString,
								ex
							);
						}
					}
					else
					{
						throw new ContentLoadException(
							"Could not find ContentTypeReader Type.\n" +
							"Please ensure the name of the Assembly that contains\n" +
							"the Type matches the assembly in the full type name:\n" +
							originalReaderTypeString + " (" + readerTypeString + ")"
						);
					}
				}
				/* I think the next 4 bytes refer to the "Version" of the type reader,
				 * although it always seems to be zero
				 */
				_reader.ReadInt32();
			}
			return contentReaders;
		}

		#endregion

		#region Public Static Methods

		/// <summary>
		/// Adds the type creator.
		/// </summary>
		/// <param name='typeString'>
		/// Type string.
		/// </param>
		/// <param name='createFunction'>
		/// Create function.
		/// </param>
		public static void AddTypeCreator(
			string typeString,
			Func<ContentTypeReader> createFunction
		) {
			if (!typeCreators.ContainsKey(typeString))
			{
				typeCreators.Add(typeString, createFunction);
			}
		}

		public static void ClearTypeCreators()
		{
			typeCreators.Clear();
		}

		/// <summary>
		/// Removes Version, Culture and PublicKeyToken from a type string.
		/// </summary>
		/// <remarks>
		/// Supports multiple generic types (e.g. Dictionary&lt;TKey,TValue&gt;)
		/// and nested generic types (e.g. List&lt;List&lt;int&gt;&gt;).
		/// </remarks>
		/// <param name="type">
		/// A <see cref="System.String"/>
		/// </param>
		/// <returns>
		/// A <see cref="System.String"/>
		/// </returns>
		public static string PrepareType(string type)
		{
			// Needed to support nested types
			int count = type.Split(
				new[] {"[["},
				StringSplitOptions.None
			).Length - 1;
			string preparedType = type;
			for (int i = 0; i < count; i += 1)
			{
				preparedType = Regex.Replace(
					preparedType,
					@"\[(.+?), Version=.+?\]",
					"[$1]"
				);
			}
			// Handle non generic types
			if (preparedType.Contains("PublicKeyToken"))
			{
				preparedType = Regex.Replace(
					preparedType,
					@"(.+?), Version=.+?$",
					"$1"
				);
			}
			// TODO: For WinRT this is most likely broken!
			preparedType = preparedType.Replace(
				", Microsoft.Xna.Framework.Graphics",
				string.Format(
					", {0}",
					assemblyName
				)
			);
			preparedType = preparedType.Replace(
				", Microsoft.Xna.Framework",
				string.Format(
					", {0}",
					assemblyName
				)
			);
			return preparedType;
		}

		#endregion
	}
}
