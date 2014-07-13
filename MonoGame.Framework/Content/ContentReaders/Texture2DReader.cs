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

using Microsoft.Xna.Framework.Graphics;
#endregion

namespace Microsoft.Xna.Framework.Content
{
	internal class Texture2DReader : ContentTypeReader<Texture2D>
	{
		#region Private Supported File Extensions Variable

		static string[] supportedExtensions = new string[] {
			".jpg", ".bmp", ".jpeg", ".png", ".gif", ".pict", ".tga"
		};

		#endregion

		#region Internal Constructor

		internal Texture2DReader()
		{
		}

		#endregion

		#region Internal Filename Normalizer Method

		internal static string Normalize(string fileName)
		{
			return Normalize(fileName, supportedExtensions);
		}

		#endregion

		#region Protected Read Method

		protected internal override Texture2D Read(
			ContentReader reader,
			Texture2D existingInstance
		) {
			Texture2D texture = null;

			SurfaceFormat surfaceFormat;
			if (reader.version < 5) {
				SurfaceFormat_Legacy legacyFormat =
					(SurfaceFormat_Legacy) reader.ReadInt32();
				switch(legacyFormat) {
				case SurfaceFormat_Legacy.Dxt1:
					surfaceFormat = SurfaceFormat.Dxt1;
					break;
				case SurfaceFormat_Legacy.Dxt3:
					surfaceFormat = SurfaceFormat.Dxt3;
					break;
				case SurfaceFormat_Legacy.Dxt5:
					surfaceFormat = SurfaceFormat.Dxt5;
					break;
				case SurfaceFormat_Legacy.Color:
					surfaceFormat = SurfaceFormat.Color;
					break;
				default:
					throw new NotSupportedException(
						"Unsupported legacy surface format."
					);
				}
			}
			else
			{
				surfaceFormat = (SurfaceFormat) reader.ReadInt32();
			}
			int width = reader.ReadInt32();
			int height = reader.ReadInt32();
			int levelCount = reader.ReadInt32();
			int levelCountOutput = levelCount;
			SurfaceFormat convertedFormat = surfaceFormat;
			switch (surfaceFormat)
			{
			case SurfaceFormat.Dxt1:
				if (!Game.Instance.GraphicsDevice.GLDevice.SupportsDxt1)
				{
					convertedFormat = SurfaceFormat.Color;
				}
				break;
			case SurfaceFormat.Dxt3:
			case SurfaceFormat.Dxt5:
				if (!Game.Instance.GraphicsDevice.GLDevice.SupportsS3tc)
				{
					convertedFormat = SurfaceFormat.Color;
				}
				break;
			case SurfaceFormat.NormalizedByte4:
				convertedFormat = SurfaceFormat.Color;
				break;
			}
			if (existingInstance == null)
			{
				texture = new Texture2D(
					reader.GraphicsDevice,
					width,
					height,
					levelCountOutput > 1,
					convertedFormat
				);
			}
			else
			{
				texture = existingInstance;
			}

			for (int level = 0; level < levelCount; level += 1)
			{
				int levelDataSizeInBytes = (reader.ReadInt32 ());
				byte[] levelData = null; // Don't assign this quite yet...
				int levelWidth = width >> level;
				int levelHeight = height >> level;
				if (level >= levelCountOutput)
				{
					continue;
				}
				// Convert the image data if required
				switch (surfaceFormat)
				{
				case SurfaceFormat.Dxt1:
					if (!Game.Instance.GraphicsDevice.GLDevice.SupportsDxt1)
					{
						levelData = reader.ReadBytes(levelDataSizeInBytes);
						levelData = DxtUtil.DecompressDxt1(
							levelData,
							levelWidth,
							levelHeight
						);
					}
					break;
				case SurfaceFormat.Dxt3:
					if (!Game.Instance.GraphicsDevice.GLDevice.SupportsS3tc)
					{
						levelData = reader.ReadBytes(levelDataSizeInBytes);
						levelData = DxtUtil.DecompressDxt3(
							levelData,
							levelWidth,
							levelHeight
						);
					}
					break;
				case SurfaceFormat.Dxt5:
					if (!Game.Instance.GraphicsDevice.GLDevice.SupportsS3tc)
					{
						levelData = reader.ReadBytes(levelDataSizeInBytes);
						levelData = DxtUtil.DecompressDxt5(
							levelData,
							levelWidth,
							levelHeight
						);
					}
					break;
				case SurfaceFormat.Bgr565:
					{
						levelData = reader.ReadBytes(levelDataSizeInBytes);
					}
					break;
				case SurfaceFormat.Bgra5551:
					{
						levelData = reader.ReadBytes(levelDataSizeInBytes);
						// Shift the channels to suit OPENGL
						int offset = 0;
						for (int y = 0; y < levelHeight; y += 1)
						{
							for (int x = 0; x < levelWidth; x += 1)
							{
								ushort pixel = BitConverter.ToUInt16(
									levelData,
									offset
								);
								pixel = (ushort) (
									((pixel & 0x7FFF) << 1) |
									((pixel & 0x8000) >> 15)
								);
								levelData[offset] =
									(byte) (pixel);
								levelData[offset + 1] =
									(byte) (pixel >> 8);
								offset += 2;
							}
						}
					}
					break;
				case SurfaceFormat.Bgra4444:
					{
						levelData = reader.ReadBytes(levelDataSizeInBytes);
						// Shift the channels to suit OPENGL
						int offset = 0;
						for (int y = 0; y < levelHeight; y += 1)
						{
							for (int x = 0; x < levelWidth; x += 1)
							{
								ushort pixel = BitConverter.ToUInt16(
									levelData,
									offset
								);
								pixel = (ushort) (
									((pixel & 0x0FFF) << 4) |
									((pixel & 0xF000) >> 12)
								);
								levelData[offset] =
									(byte) (pixel);
								levelData[offset + 1] =
									(byte) (pixel >> 8);
								offset += 2;
							}
						}
					}
					break;
				case SurfaceFormat.NormalizedByte4:
					{
						levelData = reader.ReadBytes(levelDataSizeInBytes);
						int bytesPerPixel = 4; // According to Texture.GetFormatSize()
						int pitch = levelWidth * bytesPerPixel;
						for (int y = 0; y < levelHeight; y += 1)
						{
							for (int x = 0; x < levelWidth; x += 1)
							{
								int color = BitConverter.ToInt32(
									levelData,
									y * pitch + x * bytesPerPixel
								);
								// R:=W
								levelData[y * pitch + x * 4] =
									(byte) (((color >> 16) & 0xff));
								// G:=V
								levelData[y * pitch + x * 4 + 1] =
									(byte) (((color >> 8) & 0xff));
								// B:=U
								levelData[y * pitch + x * 4 + 2] =
									(byte) (((color) & 0xff));
								// A:=Q
								levelData[y * pitch + x * 4 + 3] =
									(byte) (((color >> 24) & 0xff));
							}
						}
					}
					break;
				}

				if (	levelData == null &&
					reader.BaseStream.GetType() != typeof(System.IO.MemoryStream)	)
				{
					/* If the ContentReader is not backed by a
					 * MemoryStream, we have to read the data in.
					 */
					levelData = reader.ReadBytes(levelDataSizeInBytes);
				}
				if (levelData != null)
				{
					/* If we had to convert the data, or get the data from a
					 * non-MemoryStream, we set the data with our levelData
					 * reference.
					 */
					texture.SetData(level, null, levelData, 0, levelData.Length);
				}
				else
				{
					/* Ideally, we didn't have to perform any conversion or
					 * unnecessary reading. Just throw the buffer directly
					 * into SetData, skipping a redundant byte[] copy.
					 */
					texture.SetData<byte>(
						level,
						null,
						(((System.IO.MemoryStream) (reader.BaseStream)).GetBuffer()),
						(int) reader.BaseStream.Position,
						levelDataSizeInBytes
					);
					reader.BaseStream.Seek(
						levelDataSizeInBytes,
						System.IO.SeekOrigin.Current
					);
				}

			}

			return texture;
		}

		#endregion
	}
}
