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
using System.Collections.Generic;
using System.IO;
using System.Text;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
#endregion

namespace Microsoft.Xna.Framework.Content
{
	public sealed class ContentReader : BinaryReader
	{
		#region Public Properties

		public ContentManager ContentManager
		{
			get
			{
				return contentManager;
			}
		}

		public string AssetName
		{
			get
			{
				return assetName;
			}
		}

		#endregion

		#region Internal Properties

		internal ContentTypeReader[] TypeReaders
		{
			get
			{
				return typeReaders;
			}
		}

		internal GraphicsDevice GraphicsDevice
		{
			get
			{
				return this.graphicsDevice;
			}
		}

		#endregion

		#region Internal Variables

		internal int version;
		internal int sharedResourceCount;

		#endregion

		#region Private Variables

		private ContentManager contentManager;
		private Action<IDisposable> recordDisposableObject;
		private ContentTypeReaderManager typeReaderManager;
		private GraphicsDevice graphicsDevice;
		private string assetName;
		private List<KeyValuePair<int, Action<object>>> sharedResourceFixups;
		private ContentTypeReader[] typeReaders;

		#endregion

		#region Internal Constructor

		internal ContentReader(
			ContentManager manager,
			Stream stream,
			GraphicsDevice graphicsDevice,
			string assetName,
			int version,
			Action<IDisposable> recordDisposableObject
		) : base(stream) {
			this.graphicsDevice = graphicsDevice;
			this.recordDisposableObject = recordDisposableObject;
			this.contentManager = manager;
			this.assetName = assetName;
			this.version = version;
		}

		#endregion

		#region Public Read Methods

		public T ReadExternalReference<T>()
		{
			string externalReference = ReadString();
			if (!String.IsNullOrEmpty(externalReference))
			{
				const char notSeparator = '\\';
				char separator = Path.DirectorySeparatorChar;
				externalReference = externalReference.Replace(notSeparator, separator);
				// Get a uri for the asset path using the file:// schema and no host
				Uri src = new Uri("file:///" + assetName.Replace(notSeparator, separator));
				// Add the relative path to the external reference
				Uri dst = new Uri(src, externalReference);
				/* The uri now contains the path to the external reference within the content manager
				 * Get the local path and skip the first character (the path separator)
				 */
				return contentManager.Load<T>(dst.LocalPath.Substring(1));
			}
			return default(T);
		}

		public Matrix ReadMatrix()
		{
			Matrix result = new Matrix();
			result.M11 = ReadSingle();
			result.M12 = ReadSingle();
			result.M13 = ReadSingle();
			result.M14 = ReadSingle();
			result.M21 = ReadSingle();
			result.M22 = ReadSingle();
			result.M23 = ReadSingle();
			result.M24 = ReadSingle();
			result.M31 = ReadSingle();
			result.M32 = ReadSingle();
			result.M33 = ReadSingle();
			result.M34 = ReadSingle();
			result.M41 = ReadSingle();
			result.M42 = ReadSingle();
			result.M43 = ReadSingle();
			result.M44 = ReadSingle();
			return result;
		}

		public T ReadObject<T>()
		{
			int typeReaderIndex = Read7BitEncodedInt();
			if (typeReaderIndex == 0)
			{
				return default(T);
			}
			T result = (T) typeReaders[typeReaderIndex - 1].Read(this, default(T));
			RecordDisposable(result);
			return result;
		}

		public T ReadObject<T>(ContentTypeReader typeReader)
		{
			T result = (T) typeReader.Read(this, default(T));
			RecordDisposable(result);
			return result;
		}

		public T ReadObject<T>(T existingInstance)
		{
			int typeReaderIndex = Read7BitEncodedInt();
			if (typeReaderIndex == 0)
			{
				return default(T);
			}
			T result = (T) typeReaders[typeReaderIndex - 1].Read(this, existingInstance);
			RecordDisposable(result);
			return result;
		}

		public T ReadObject<T>(ContentTypeReader typeReader, T existingInstance)
		{
			if (!typeReader.TargetType.IsValueType)
			{
				return (T) ReadObject<object>();
			}
			T result = (T) typeReader.Read(this, existingInstance);
			RecordDisposable(result);
			return result;
		}

		public Quaternion ReadQuaternion()
		{
			Quaternion result = new Quaternion();
			result.X = ReadSingle();
			result.Y = ReadSingle();
			result.Z = ReadSingle();
			result.W = ReadSingle();
			return result;
		}

		public T ReadRawObject<T>()
		{
			return (T) ReadRawObject<T>(default(T));
		}

		public T ReadRawObject<T>(ContentTypeReader typeReader)
		{
			return (T) ReadRawObject<T>(typeReader, default(T));
		}

		public T ReadRawObject<T>(T existingInstance)
		{
			Type objectType = typeof(T);
			foreach (ContentTypeReader typeReader in typeReaders)
			{
				if (typeReader.TargetType == objectType)
				{
					return (T) ReadRawObject<T>(typeReader,existingInstance);
				}
			}
			throw new NotSupportedException();
		}

		public T ReadRawObject<T>(ContentTypeReader typeReader, T existingInstance)
		{
			return (T) typeReader.Read(this, existingInstance);
		}

		public void ReadSharedResource<T>(Action<T> fixup)
		{
			int index = Read7BitEncodedInt();
			if (index > 0)
			{
				sharedResourceFixups.Add(
					new KeyValuePair<int, Action<object>> (
						index - 1,
						delegate(object v)
						{
							if (!(v is T))
							{
								throw new ContentLoadException(
									String.Format(
										"Error loading shared resource. Expected type {0}, received type {1}",
										typeof(T).Name, v.GetType().Name
									)
								);
							}
							fixup((T) v);
						}
					)
				);
			}
		}

		public Vector2 ReadVector2()
		{
			Vector2 result = new Vector2();
			result.X = ReadSingle();
			result.Y = ReadSingle();
			return result;
		}

		public Vector3 ReadVector3()
		{
			Vector3 result = new Vector3();
			result.X = ReadSingle();
			result.Y = ReadSingle();
			result.Z = ReadSingle();
			return result;
		}

		public Vector4 ReadVector4()
		{
			Vector4 result = new Vector4();
			result.X = ReadSingle();
			result.Y = ReadSingle();
			result.Z = ReadSingle();
			result.W = ReadSingle();
			return result;
		}

		public Color ReadColor()
		{
			Color result = new Color();
			result.R = ReadByte();
			result.G = ReadByte();
			result.B = ReadByte();
			result.A = ReadByte();
			return result;
		}

		#endregion

		#region Internal Methods

		internal object ReadAsset<T>()
		{
			InitializeTypeReaders();
			// Read primary object
			object result = ReadObject<T>();
			// Read shared resources
			ReadSharedResources();
			return result;
		}

		internal void InitializeTypeReaders()
		{
			typeReaderManager = new ContentTypeReaderManager(this);
			typeReaders = typeReaderManager.LoadAssetReaders();
			foreach (ContentTypeReader r in typeReaders)
			{
				r.Initialize(typeReaderManager);
			}

			sharedResourceCount = Read7BitEncodedInt();
			sharedResourceFixups = new List<KeyValuePair<int, Action<object>>>();
		}

		internal void ReadSharedResources()
		{
			if (sharedResourceCount <= 0)
			{
				return;
			}

			object[] sharedResources = new object[sharedResourceCount];
			for (int i = 0; i < sharedResourceCount; i += 1)
			{
				int index = Read7BitEncodedInt();
				if (index > 0)
				{
					ContentTypeReader contentReader = typeReaders[index - 1];
					sharedResources[i] = ReadObject<object>(contentReader);
				}
				else
				{
					sharedResources[i] = null;
				}
			}
			// Fixup shared resources by calling each registered action
			foreach (KeyValuePair<int, Action<object>> fixup in sharedResourceFixups)
			{
				fixup.Value(sharedResources[fixup.Key]);
			}
		}

		internal new int Read7BitEncodedInt()
		{
			return base.Read7BitEncodedInt();
		}

		internal BoundingSphere ReadBoundingSphere()
		{
			Vector3 position = ReadVector3();
			float radius = ReadSingle();
			return new BoundingSphere(position, radius);
		}

		#endregion

		#region Private Methods

		private void RecordDisposable<T>(T result)
		{
			IDisposable disposable = result as IDisposable;
			if (disposable == null)
			{
				return;
			}
			if (recordDisposableObject != null)
			{
				recordDisposableObject(disposable);
			}
			else
			{
				contentManager.RecordDisposable(disposable);
			}
		}

		#endregion
	}
}
