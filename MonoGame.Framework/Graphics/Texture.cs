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
using System.Diagnostics;

using OpenTK.Graphics.OpenGL;
#endregion

namespace Microsoft.Xna.Framework.Graphics
{
	public abstract class Texture : GraphicsResource
	{
		#region Public Properties

		public SurfaceFormat Format
		{
			get;
			protected set;
		}

		public int LevelCount
		{
			get;
			protected set;
		}

		#endregion

		#region Internal OpenGL Variables

		internal OpenGLDevice.OpenGLTexture texture;

		#endregion

		#region Protected OpenGL Variables

		[CLSCompliant(false)]
		protected PixelInternalFormat glInternalFormat;

		[CLSCompliant(false)]
		protected PixelFormat glFormat;

		[CLSCompliant(false)]
		protected PixelType glType;

		#endregion

		#region Protected Dispose Method

		protected override void Dispose(bool disposing)
		{
			if (!IsDisposed)
			{
				GraphicsDevice.AddDisposeAction(() =>
				{
					OpenGLDevice.Instance.DeleteTexture(texture);
				});
			}
			base.Dispose(disposing);
		}

		#endregion

		#region Internal Surface Pitch Calculator

		internal int GetPitch(int width)
		{
			Debug.Assert(width > 0, "The width is negative!");

			if (	Format == SurfaceFormat.Dxt1 ||
				Format == SurfaceFormat.Dxt3 ||
				Format == SurfaceFormat.Dxt5	)
			{
				return ((width + 3) / 4) * GetFormatSize();
			}
			return width * GetFormatSize();
		}

		#endregion

		#region Internal Context Reset Method

		internal protected override void GraphicsDeviceResetting()
		{
			// FIXME: Do we even want to bother with DeviceResetting for GL? -flibit
		}

		#endregion

		#region Protected XNA->GL SurfaceFormat Conversion Method

		protected void GetGLSurfaceFormat()
		{
			switch (Format)
			{
				case SurfaceFormat.Color:
					glInternalFormat = PixelInternalFormat.Rgba;
					glFormat = PixelFormat.Rgba;
					glType = PixelType.UnsignedByte;
					break;
				case SurfaceFormat.Bgr565:
					glInternalFormat = PixelInternalFormat.Rgb;
					glFormat = PixelFormat.Rgb;
					glType = PixelType.UnsignedShort565;
					break;
				case SurfaceFormat.Bgra4444:
					glInternalFormat = PixelInternalFormat.Rgba4;
					glFormat = PixelFormat.Rgba;
					glType = PixelType.UnsignedShort4444;
					break;
				case SurfaceFormat.Bgra5551:
					glInternalFormat = PixelInternalFormat.Rgba;
					glFormat = PixelFormat.Rgba;
					glType = PixelType.UnsignedShort5551;
					break;
				case SurfaceFormat.Alpha8:
					glInternalFormat = PixelInternalFormat.Luminance;
					glFormat = PixelFormat.Luminance;
					glType = PixelType.UnsignedByte;
					break;
				case SurfaceFormat.Dxt1:
					glInternalFormat = PixelInternalFormat.CompressedRgbaS3tcDxt1Ext;
					glFormat = (PixelFormat)All.CompressedTextureFormats;
					break;
				case SurfaceFormat.Dxt3:
					glInternalFormat = PixelInternalFormat.CompressedRgbaS3tcDxt3Ext;
					glFormat = (PixelFormat)All.CompressedTextureFormats;
					break;
				case SurfaceFormat.Dxt5:
					glInternalFormat = PixelInternalFormat.CompressedRgbaS3tcDxt5Ext;
					glFormat = (PixelFormat)All.CompressedTextureFormats;
					break;
				case SurfaceFormat.Single:
					glInternalFormat = PixelInternalFormat.R32f;
					glFormat = PixelFormat.Red;
					glType = PixelType.Float;
					break;
				case SurfaceFormat.HalfVector2:
					glInternalFormat = PixelInternalFormat.Rg16f;
					glFormat = PixelFormat.Rg;
					glType = PixelType.HalfFloat;
					break;
				case SurfaceFormat.HdrBlendable:
				case SurfaceFormat.HalfVector4:
					glInternalFormat = PixelInternalFormat.Rgba16f;
					glFormat = PixelFormat.Rgba;
					glType = PixelType.HalfFloat;
					break;
				case SurfaceFormat.HalfSingle:
					glInternalFormat = PixelInternalFormat.R16f;
					glFormat = PixelFormat.Red;
					glType = PixelType.HalfFloat;
					break;
				case SurfaceFormat.Vector2:
					glInternalFormat = PixelInternalFormat.Rg32f;
					glFormat = PixelFormat.Rg;
					glType = PixelType.Float;
					break;
				case SurfaceFormat.Vector4:
					glInternalFormat = PixelInternalFormat.Rgba32f;
					glFormat = PixelFormat.Rgba;
					glType = PixelType.Float;
					break;
				case SurfaceFormat.NormalizedByte2:
					glInternalFormat = PixelInternalFormat.Rg8i;
					glFormat = PixelFormat.Rg;
					glType = PixelType.Byte;
					break;
				case SurfaceFormat.NormalizedByte4:
					glInternalFormat = PixelInternalFormat.Rgba8i;
					glFormat = PixelFormat.Rgba;
					glType = PixelType.Byte;
					break;
				case SurfaceFormat.Rg32:
					glInternalFormat = PixelInternalFormat.Rg16ui;
					glFormat = PixelFormat.Rg;
					glType = PixelType.UnsignedShort;
					break;
				case SurfaceFormat.Rgba64:
					glInternalFormat = PixelInternalFormat.Rgba16ui;
					glFormat = PixelFormat.Rgba;
					glType = PixelType.UnsignedShort;
					break;
				case SurfaceFormat.Rgba1010102:
					glInternalFormat = PixelInternalFormat.Rgb10A2;
					glFormat = PixelFormat.Rgba;
					glType = PixelType.UnsignedInt1010102;
					break;
				default:
					throw new NotSupportedException();
			}
		}

		#endregion

		#region Protected SurfaceFormat Size Method

		protected int GetFormatSize()
		{
			switch (Format)
			{
				case SurfaceFormat.Dxt1:
					return 8;
				case SurfaceFormat.Dxt3:
				case SurfaceFormat.Dxt5:
					return 16;
				case SurfaceFormat.Alpha8:
					return 1;
				case SurfaceFormat.Bgr565:
				case SurfaceFormat.Bgra4444:
				case SurfaceFormat.Bgra5551:
				case SurfaceFormat.HalfSingle:
				case SurfaceFormat.NormalizedByte2:
					return 2;
				case SurfaceFormat.Color:
				case SurfaceFormat.Single:
				case SurfaceFormat.Rg32:
				case SurfaceFormat.HalfVector2:
				case SurfaceFormat.NormalizedByte4:
				case SurfaceFormat.Rgba1010102:
					return 4;
				case SurfaceFormat.HalfVector4:
				case SurfaceFormat.Rgba64:
				case SurfaceFormat.Vector2:
					return 8;
				case SurfaceFormat.Vector4:
					return 16;
				default:
					throw new ArgumentException("Should be a value defined in SurfaceFormat", "Format");
			}
		}

		#endregion

		#region Static Mipmap Level Calculator

		internal static int CalculateMipLevels(
			int width,
			int height = 0,
			int depth = 0
		) {
			int levels = 1;
			for (
				int size = Math.Max(Math.Max(width, height), depth);
				size > 1;
				levels += 1
			) {
				size /= 2;
			}
			return levels;
		}

		#endregion
	}
}
