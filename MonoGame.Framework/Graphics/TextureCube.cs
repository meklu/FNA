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
using System.Runtime.InteropServices;

using OpenTK.Graphics.OpenGL;
#endregion

namespace Microsoft.Xna.Framework.Graphics
{
	public class TextureCube : Texture
	{
		#region Public Properties

		/// <summary>
		/// Gets the width and height of the cube map face in pixels.
		/// </summary>
		/// <value>The width and height of a cube map face in pixels.</value>
		public int Size
		{
			get;
			private set;
		}

		#endregion

		#region Public Constructor

		public TextureCube(
			GraphicsDevice graphicsDevice,
			int size,
			bool mipMap,
			SurfaceFormat format
		) {
			if (graphicsDevice == null)
			{
				throw new ArgumentNullException("graphicsDevice");
			}

			GraphicsDevice = graphicsDevice;
			Size = size;
			LevelCount = mipMap ? CalculateMipLevels(size) : 1;
			Format = format;
			GetGLSurfaceFormat();

			Threading.ForceToMainThread(() =>
			{
				texture = GraphicsDevice.GLDevice.CreateTexture(
					typeof(TextureCube),
					Format,
					mipMap
				);

				if (glFormat == (PixelFormat) All.CompressedTextureFormats)
				{
					for (int i = 0; i < 6; i += 1)
					{
						for (int l = 0; l < LevelCount; l += 1)
						{
							int levelSize = Math.Max(size >> l, 1);
							GL.CompressedTexImage2D(
								GetGLCubeFace((CubeMapFace) i),
								l,
								glInternalFormat,
								levelSize,
								levelSize,
								0,
								((levelSize + 3) / 4) * ((levelSize + 3) / 4) * GetFormatSize(),
								IntPtr.Zero
							);
						}
					}
				}
				else
				{
					for (int i = 0; i < 6; i += 1)
					{
						for (int l = 0; l < LevelCount; l += 1)
						{
							GL.TexImage2D(
								GetGLCubeFace((CubeMapFace) i),
								l,
								glInternalFormat,
								size,
								size,
								0,
								glFormat,
								glType,
								IntPtr.Zero
							);
						}
					}
				}
			});
		}

		#endregion

		#region Public SetData Methods

		public void SetData<T>(
			CubeMapFace cubeMapFace,
			T[] data
		) where T : struct {
			SetData(
				cubeMapFace,
				0,
				null,
				data,
				0,
				data.Length
			);
		}

		public void SetData<T>(
			CubeMapFace cubeMapFace,
			T[] data,
			int startIndex,
			int elementCount
		) where T : struct {
			SetData(
				cubeMapFace,
				0,
				null,
				data,
				startIndex,
				elementCount
			);
		}

		public void SetData<T>(
			CubeMapFace cubeMapFace,
			int level,
			Rectangle? rect,
			T[] data,
			int startIndex,
			int elementCount
		) where T : struct {
			if (data == null)
			{
				throw new ArgumentNullException("data");
			}

			int xOffset, yOffset, width, height;
			if (rect.HasValue)
			{
				xOffset = rect.Value.X;
				yOffset = rect.Value.Y;
				width = rect.Value.Width;
				height = rect.Value.Height;
			}
			else
			{
				xOffset = 0;
				yOffset = 0;
				width = Math.Max(1, Size >> level);
				height = Math.Max(1, Size >> level);
			}

			Threading.ForceToMainThread(() =>
			{
				GCHandle dataHandle = GCHandle.Alloc(data, GCHandleType.Pinned);
				int elementSizeInBytes = Marshal.SizeOf(typeof(T));
				int startByte = startIndex * elementSizeInBytes;
				IntPtr dataPtr = (IntPtr) (dataHandle.AddrOfPinnedObject().ToInt64() + startByte);

				try
				{
					GraphicsDevice.GLDevice.BindTexture(texture);
					if (glFormat == (PixelFormat) All.CompressedTextureFormats)
					{
						int dataLength;
						if (elementCount > 0)
						{
							dataLength = elementCount * elementSizeInBytes;
						}
						else
						{
							dataLength = data.Length - startByte;
						}

						/* Note that we're using glInternalFormat, not glFormat.
						 * In this case, they should actually be the same thing,
						 * but we use glFormat somewhat differently for
						 * compressed textures.
						 * -flibit
						 */
						GL.CompressedTexSubImage2D(
							GetGLCubeFace(cubeMapFace),
							level,
							xOffset,
							yOffset,
							width,
							height,
							(PixelFormat) glInternalFormat,
							dataLength,
							dataPtr
						);
					}
					else
					{
						GL.TexSubImage2D(
							GetGLCubeFace(cubeMapFace),
							level,
							xOffset,
							yOffset,
							width,
							height,
							glFormat,
							glType,
							dataPtr
						);
					}
				}
				finally
				{
					dataHandle.Free();
				}
			});
		}

		#endregion

		#region Public GetData Method

		public void GetData<T>(
			CubeMapFace cubeMapFace,
			T[] data
		) where T : struct {
			GetData(
				cubeMapFace,
				0,
				null,
				data,
				0,
				data.Length
			);
		}

		public void GetData<T>(
			CubeMapFace cubeMapFace,
			T[] data,
			int startIndex,
			int elementCount
		) where T : struct {
			GetData(
				cubeMapFace,
				0,
				null,
				data,
				startIndex,
				elementCount
			);
		}

		public void GetData<T>(
			CubeMapFace cubeMapFace,
			int level,
			Rectangle? rect,
			T[] data,
			int startIndex,
			int elementCount
		) where T : struct {
			if (data == null || data.Length == 0)
			{
				throw new ArgumentException("data cannot be null");
			}
			if (data.Length < startIndex + elementCount)
			{
				throw new ArgumentException(
					"The data passed has a length of " + data.Length.ToString() +
					" but " + elementCount.ToString() + " pixels have been requested."
				);
			}

			GraphicsDevice.GLDevice.BindTexture(texture);

			if (glFormat == (PixelFormat) All.CompressedTextureFormats)
			{
				throw new NotImplementedException("GetData, CompressedTexture");
			}
			else if (rect == null)
			{
				// Just throw the whole texture into the user array.
				GL.GetTexImage(
					GetGLCubeFace(cubeMapFace),
					0,
					glFormat,
					glType,
					data
				);
			}
			else
			{
				// Get the whole texture...
				T[] texData = new T[Size * Size];
				GL.GetTexImage(
					GetGLCubeFace(cubeMapFace),
					0,
					glFormat,
					glType,
					texData
				);

				// Now, blit the rect region into the user array.
				Rectangle region = rect.Value;
				int curPixel = -1;
				for (int row = region.Y; row < region.Y + region.Height; row += 1)
				{
					for (int col = region.X; col < region.X + region.Width; col += 1)
					{
						curPixel += 1;
						if (curPixel < startIndex)
						{
							// If we're not at the start yet, just keep going...
							continue;
						}
						if (curPixel > elementCount)
						{
							// If we're past the end, we're done!
							return;
						}
						data[curPixel - startIndex] = texData[(row * Size) + col];
					}
				}
			}
		}

		#endregion

		#region XNA->GL CubeMapFace Conversion Method

		private static TextureTarget GetGLCubeFace(CubeMapFace face)
		{
			switch (face)
			{
				case CubeMapFace.PositiveX: return TextureTarget.TextureCubeMapPositiveX;
				case CubeMapFace.NegativeX: return TextureTarget.TextureCubeMapNegativeX;
				case CubeMapFace.PositiveY: return TextureTarget.TextureCubeMapPositiveY;
				case CubeMapFace.NegativeY: return TextureTarget.TextureCubeMapNegativeY;
				case CubeMapFace.PositiveZ: return TextureTarget.TextureCubeMapPositiveZ;
				case CubeMapFace.NegativeZ: return TextureTarget.TextureCubeMapNegativeZ;
			}
			throw new ArgumentException("Should be a value defined in CubeMapFace", "face");
		}

		#endregion
	}
}
