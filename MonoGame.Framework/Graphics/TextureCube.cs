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
				texture = new OpenGLDevice.OpenGLTexture(
					TextureTarget.TextureCubeMap,
					Format,
					LevelCount > 1
				);
				texture.WrapS.Set(TextureAddressMode.Clamp);
				texture.WrapT.Set(TextureAddressMode.Clamp);

				OpenGLDevice.Instance.BindTexture(texture);
				for (int i = 0; i < 6; i += 1)
				{
					if (glFormat == (PixelFormat) All.CompressedTextureFormats)
					{
						throw new NotImplementedException();
					}
					else
					{
						GL.TexImage2D(
							GetGLCubeFace((CubeMapFace) i),
							0,
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
				texture.Flush(true);
			});
		}

		#endregion

		#region Public SetData Methods

		public void SetData<T>(
			CubeMapFace face,
			T[] data
		) where T : struct {
			SetData(
				face,
				0,
				null,
				data,
				0,
				data.Length
			);
		}

		public void SetData<T>(
			CubeMapFace face,
			T[] data,
			int startIndex,
			int elementCount
		) where T : struct {
			SetData(
				face,
				0,
				null,
				data,
				startIndex,
				elementCount
			);
		}

		public void SetData<T>(
			CubeMapFace face,
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

				try
				{
					OpenGLDevice.Instance.BindTexture(texture);
					if (glFormat == (PixelFormat) All.CompressedTextureFormats)
					{
						throw new NotImplementedException();
					}
					else
					{
						GL.TexSubImage2D(
							GetGLCubeFace(face),
							level,
							xOffset,
							yOffset,
							width,
							height,
							glFormat,
							glType,
							(IntPtr) (dataHandle.AddrOfPinnedObject().ToInt64() + startIndex * Marshal.SizeOf(typeof(T)))
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

		/// <summary>
		/// Gets a copy of cube texture data specifying a cubemap face.
		/// </summary>
		/// <typeparam name="T">Generic type for data array element.</typeparam>
		/// <param name="cubeMapFace">The cube map face.</param>
		/// <param name="data">The data.</param>
		public void GetData<T>(
			CubeMapFace cubeMapFace,
			T[] data
		) where T : struct {
			// 4 bytes per pixel
			if (data.Length < Size * Size * 4)
			{
				throw new ArgumentException("data");
			}

			TextureTarget target = GetGLCubeFace(cubeMapFace);
			OpenGLDevice.Instance.BindTexture(texture);
			GL.GetTexImage<T>(
				target,
				0,
				PixelFormat.Bgra,
				PixelType.UnsignedByte,
				data
			);
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
