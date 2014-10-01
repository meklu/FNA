#region License
/* FNA - XNA4 Reimplementation for Desktop Platforms
 * Copyright 2009-2014 Ethan Lee and the MonoGame Team
 *
 * Released under the Microsoft Public License.
 * See LICENSE for details.
 */
#endregion

#region DISABLE_FAUXBACKBUFFER Option
// #define DISABLE_FAUXBACKBUFFER
/* If you want to debug GL without the extra FBO in your way, you can use this.
 * Additionally, if you always use the desktop resolution in fullscreen mode,
 * you can use this to optimize your game and even lower the GL requirements.
 * -flibit
 */
#endregion

#region THREADED_GL Option
// #define THREADED_GL
/* Ah, so I see you've run into some issues with threaded GL...
 *
 * This class is designed to handle rendering coming from multiple threads, but
 * if you're too wreckless with how many threads are calling the GL, this will
 * hang.
 *
 * With THREADED_GL we instead allow you to run threaded rendering using
 * multiple GL contexts. This is more flexible, but much more dangerous.
 *
 * Also note that this affects Threading.cs and SDL2/SDL2_GamePlatform.cs!
 * Check THREADED_GL there too.
 *
 * Basically, if you have to enable this, you should feel very bad.
 * -flibit
 */
#endregion

#region Using Statements
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

using SDL2;
using OpenTK.Graphics.OpenGL;
#endregion

namespace Microsoft.Xna.Framework.Graphics
{
	internal class OpenGLDevice
	{
		#region OpenGL Texture Container Class

		public class OpenGLTexture
		{
			public int Handle
			{
				get;
				private set;
			}

			public TextureTarget Target
			{
				get;
				private set;
			}

			public SurfaceFormat Format
			{
				get;
				private set;
			}

			public bool HasMipmaps
			{
				get;
				private set;
			}

			public TextureAddressMode WrapS;
			public TextureAddressMode WrapT;
			public TextureAddressMode WrapR;
			public TextureFilter Filter;
			public float Anistropy;
			public int MaxMipmapLevel;
			public float LODBias;

			public OpenGLTexture(TextureTarget target, SurfaceFormat format, bool hasMipmaps)
			{
				Handle = GL.GenTexture();
				Target = target;
				Format = format;
				HasMipmaps = hasMipmaps;

				WrapS = TextureAddressMode.Wrap;
				WrapT = TextureAddressMode.Wrap;
				WrapR = TextureAddressMode.Wrap;
				Filter = TextureFilter.Linear;
				Anistropy = 4.0f;
				MaxMipmapLevel = 0;
				LODBias = 0.0f;
			}

			public void Dispose()
			{
				GL.DeleteTexture(Handle);
				Handle = 0;
			}

			// We can't set a SamplerState Texture to null, so use this.
			private OpenGLTexture()
			{
				Handle = 0;
				Target = TextureTarget.Texture2D; // FIXME: Assumption! -flibit
			}
			public static readonly OpenGLTexture NullTexture = new OpenGLTexture();
		}

		#endregion

		#region OpenGL Vertex Buffer Container Class

		public class OpenGLVertexBuffer
		{
			public int Handle
			{
				get;
				private set;
			}

			public BufferUsageHint Dynamic
			{
				get;
				private set;
			}

			public OpenGLVertexBuffer(
				GraphicsDevice graphicsDevice,
				bool dynamic,
				int vertexCount,
				int vertexStride
			) {
				Handle = GL.GenBuffer();
				Dynamic = dynamic ? BufferUsageHint.StreamDraw : BufferUsageHint.StaticDraw;

				graphicsDevice.GLDevice.BindVertexBuffer(this);
				GL.BufferData(
					BufferTarget.ArrayBuffer,
					new IntPtr(vertexStride * vertexCount),
					IntPtr.Zero,
					Dynamic
				);
			}

			private OpenGLVertexBuffer()
			{
				Handle = 0;
			}
			public static readonly OpenGLVertexBuffer NullBuffer = new OpenGLVertexBuffer();
		}

		#endregion

		#region OpenGL Index Buffer Container Class

		public class OpenGLIndexBuffer
		{
			public int Handle
			{
				get;
				private set;
			}

			public BufferUsageHint Dynamic
			{
				get;
				private set;
			}

			public IntPtr BufferSize
			{
				get;
				private set;
			}

			public OpenGLIndexBuffer(
				GraphicsDevice graphicsDevice,
				bool dynamic,
				int indexCount,
				IndexElementSize elementSize
			) {
				Handle = GL.GenBuffer();
				Dynamic = dynamic ? BufferUsageHint.StreamDraw : BufferUsageHint.StaticDraw;
				BufferSize = (IntPtr) (indexCount * (elementSize == IndexElementSize.SixteenBits ? 2 : 4));

				graphicsDevice.GLDevice.BindIndexBuffer(this);
				GL.BufferData(
					BufferTarget.ElementArrayBuffer,
					BufferSize,
					IntPtr.Zero,
					Dynamic
				);
			}

			private OpenGLIndexBuffer()
			{
				Handle = 0;
			}
			public static readonly OpenGLIndexBuffer NullBuffer = new OpenGLIndexBuffer();
		}

		#endregion

		#region OpenGL Vertex Attribute State Container Class

		public class OpenGLVertexAttribute
		{
			// Checked in FlushVertexAttributes
			public int Divisor;

			// Checked in VertexAttribPointer
			public int CurrentBuffer;
			public int CurrentSize;
			public VertexAttribPointerType CurrentType;
			public bool CurrentNormalized;
			public int CurrentStride;
			public IntPtr CurrentPointer;

			public OpenGLVertexAttribute()
			{
				Divisor = 0;
				CurrentBuffer = 0;
				CurrentSize = 4;
				CurrentType = VertexAttribPointerType.Float;
				CurrentNormalized = false;
				CurrentStride = 0;
				CurrentPointer = IntPtr.Zero;
			}
		}

		#endregion

		#region Alpha Blending State Variables

		internal bool alphaBlendEnable = false;
		private Color blendColor = Color.Transparent;
		private BlendFunction blendOp = BlendFunction.Add;
		private BlendFunction blendOpAlpha = BlendFunction.Add;
		private Blend srcBlend = Blend.One;
		private Blend dstBlend = Blend.Zero;
		private Blend srcBlendAlpha = Blend.One;
		private Blend dstBlendAlpha = Blend.Zero;
		private ColorWriteChannels colorWriteEnable = ColorWriteChannels.All;

		#endregion

		#region Depth State Variables

		internal bool zEnable = false;
		private bool zWriteEnable = false;
		private CompareFunction depthFunc = CompareFunction.Less;

		#endregion

		#region Stencil State Variables

		private bool stencilEnable = false;
		private int stencilWriteMask = -1; // AKA 0xFFFFFFFF, ugh -flibit
		private bool separateStencilEnable = false;
		private int stencilRef = 0;
		private int stencilMask = -1; // AKA 0xFFFFFFFF, ugh -flibit
		private CompareFunction stencilFunc = CompareFunction.Always;
		private StencilOperation stencilFail = StencilOperation.Keep;
		private StencilOperation stencilZFail = StencilOperation.Keep;
		private StencilOperation stencilPass = StencilOperation.Keep;
		private CompareFunction ccwStencilFunc = CompareFunction.Always;
		private StencilOperation ccwStencilFail = StencilOperation.Keep;
		private StencilOperation ccwStencilZFail = StencilOperation.Keep;
		private StencilOperation ccwStencilPass = StencilOperation.Keep;

		#endregion

		#region Rasterizer State Variables

		internal bool scissorTestEnable = false;
		internal CullMode cullFrontFace = CullMode.None;
		private FillMode fillMode = FillMode.Solid;
		private float depthBias = 0.0f;
		private float slopeScaleDepthBias = 0.0f;

		#endregion

		#region Viewport State Variables

		/* These two aren't actually empty rects by default in OpenGL,
		 * but we don't _really_ know the starting window size, so
		 * force apply this when the GraphicsDevice is initialized.
		 * -flibit
		 */
		private Rectangle scissorRectangle =  new Rectangle(
			0,
			0,
			0,
			0
		);
		private Rectangle viewport = new Rectangle(
			0,
			0,
			0,
			0
		);
		private float depthRangeMin = 0.0f;
		private float depthRangeMax = 1.0f;

		#endregion

		#region Texture Collection Variables

		// FIXME: This doesn't need to be public. Blame VideoPlayer. -flibit
		public OpenGLTexture[] Textures
		{
			get;
			private set;
		}

		#endregion

		#region Vertex Attribute State Variables

		public OpenGLVertexAttribute[] Attributes
		{
			get;
			private set;
		}

		public bool[] AttributeEnabled
		{
			get;
			private set;
		}

		private bool[] previousAttributeEnabled;
		private int[] previousAttributeDivisor;

		#endregion

		#region Buffer Binding Cache Variables

		private int currentVertexBuffer = 0;
		private int currentIndexBuffer = 0;

		#endregion

		#region Render Target Cache Variables

		private int targetFramebuffer = 0;
		private int[] currentAttachments;
		private TextureTarget[] currentAttachmentFaces;
		private int currentDrawBuffers;
		private DrawBuffersEnum[] drawBuffersArray;
		private uint currentRenderbuffer;
		private DepthFormat currentDepthStencilFormat;

		#endregion

		#region Clear Cache Variables

		private Vector4 currentClearColor = new Vector4(0, 0, 0, 0);
		private float currentClearDepth = 1.0f;
		private int currentClearStencil = 0;

		#endregion

		#region Private OpenGL Context Variable

		private IntPtr glContext;

		#endregion

		#region Faux-Backbuffer Variable

		public FauxBackbuffer Backbuffer
		{
			get;
			private set;
		}

		#endregion

		#region OpenGL Extensions List, Device Capabilities Variables

		public string Extensions
		{
			get;
			private set;
		}

		public bool SupportsDxt1
		{
			get;
			private set;
		}

		public bool SupportsS3tc
		{
			get;
			private set;
		}

		public bool SupportsHardwareInstancing
		{
			get;
			private set;
		}

		public int MaxTextureSlots
		{
			get;
			private set;
		}

		public int MaxVertexAttributes
		{
			get;
			private set;
		}

		#endregion

		#region Public Constructor

		public OpenGLDevice(
			PresentationParameters presentationParameters
		) {
			// Create OpenGL context
			glContext = SDL.SDL_GL_CreateContext(
				presentationParameters.DeviceWindowHandle
			);
			OpenTK.Graphics.GraphicsContext.CurrentContext = glContext;

#if THREADED_GL
			// Create a background context
			SDL.SDL_GL_SetAttribute(SDL.SDL_GLattr.SDL_GL_SHARE_WITH_CURRENT_CONTEXT, 1);
			Threading.WindowInfo = presentationParameters.DeviceWindowHandle;
			Threading.BackgroundContext = new Threading.GL_ContextHandle()
			{
				context = SDL.SDL_GL_CreateContext(
					presentationParameters.DeviceWindowHandle
				)
			};

			// Make the foreground context current.
			SDL.SDL_GL_MakeCurrent(presentationParameters.DeviceWindowHandle, glContext);
#endif

			// Load OpenGL entry points
			GL.LoadAll();

			// Initialize ARB_debug_output callback
			DebugOutput.Initialize();

			// Print GL information
			System.Console.WriteLine("OpenGL Device: " + GL.GetString(StringName.Renderer));
			System.Console.WriteLine("OpenGL Driver: " + GL.GetString(StringName.Version));
			System.Console.WriteLine("OpenGL Vendor: " + GL.GetString(StringName.Vendor));
			
			// Load the extension list, initialize extension-dependent components
			Extensions = GL.GetString(StringName.Extensions);
			Framebuffer.Initialize();
			SupportsS3tc = (
				Extensions.Contains("GL_EXT_texture_compression_s3tc") ||
				Extensions.Contains("GL_OES_texture_compression_S3TC") ||
				Extensions.Contains("GL_EXT_texture_compression_dxt3") ||
				Extensions.Contains("GL_EXT_texture_compression_dxt5")
			);
			SupportsDxt1 = (
				SupportsS3tc ||
				Extensions.Contains("GL_EXT_texture_compression_dxt1")
			);
			SupportsHardwareInstancing = (
				Extensions.Contains("GL_ARB_draw_instanced") &&
				Extensions.Contains("GL_ARB_instanced_arrays")
			);

			/* So apparently OSX Lion likes to lie about hardware instancing support.
			 * This is incredibly stupid, but it works!
			 * -flibit
			 */
			if (SupportsHardwareInstancing) // TODO: Let's just load our own entry points.
			{
				SupportsHardwareInstancing = SDL2.SDL.SDL_GL_GetProcAddress("glVertexAttribDivisorARB") != IntPtr.Zero;
			}

			// Initialize the faux-backbuffer
			Backbuffer = new FauxBackbuffer(
				GraphicsDeviceManager.DefaultBackBufferWidth,
				GraphicsDeviceManager.DefaultBackBufferHeight,
				DepthFormat.Depth16
			);

			// Initialize texture collection array
			int numSamplers;
			GL.GetInteger(GetPName.MaxTextureImageUnits, out numSamplers);
			Textures = new OpenGLTexture[numSamplers];
			for (int i = 0; i < numSamplers; i += 1)
			{
				Textures[i] = OpenGLTexture.NullTexture;
			}
			MaxTextureSlots = numSamplers;

			// Initialize vertex attribute state array
			int numAttributes;
			GL.GetInteger(GetPName.MaxVertexAttribs, out numAttributes);
			Attributes = new OpenGLVertexAttribute[numAttributes];
			AttributeEnabled = new bool[numAttributes];
			previousAttributeEnabled = new bool[numAttributes];
			previousAttributeDivisor = new int[numAttributes];
			for (int i = 0; i < numAttributes; i += 1)
			{
				Attributes[i] = new OpenGLVertexAttribute();
				AttributeEnabled[i] = false;
				previousAttributeEnabled[i] = false;
				previousAttributeDivisor[i] = 0;
			}
			MaxVertexAttributes = numAttributes;

			// Initialize render target FBO and state arrays
			int numAttachments;
			GL.GetInteger(GetPName.MaxDrawBuffers, out numAttachments);
			currentAttachments = new int[numAttachments];
			currentAttachmentFaces = new TextureTarget[numAttachments];
			drawBuffersArray = new DrawBuffersEnum[numAttachments];
			for (int i = 0; i < numAttachments; i += 1)
			{
				currentAttachments[i] = 0;
				currentAttachmentFaces[i] = TextureTarget.Texture2D;
				drawBuffersArray[i] = DrawBuffersEnum.ColorAttachment0 + i;
			}
			currentDrawBuffers = 0;
			currentRenderbuffer = 0;
			currentDepthStencilFormat = DepthFormat.None;
			targetFramebuffer = Framebuffer.GenFramebuffer();
		}

		#endregion

		#region Dispose Method

		public void Dispose()
		{
			Framebuffer.DeleteFramebuffer(targetFramebuffer);
			targetFramebuffer = 0;
			Backbuffer.Dispose();
			Backbuffer = null;
			Framebuffer.Clear();

#if THREADED_GL
			SDL.SDL_GL_DeleteContext(Threading.BackgroundContext.context);
#endif
			SDL.SDL_GL_DeleteContext(glContext);
		}

		#endregion

		#region Window SwapBuffers Method

		public void SwapBuffers(IntPtr overrideWindowHandle)
		{
#if !DISABLE_FAUXBACKBUFFER
			int windowWidth, windowHeight;
			SDL.SDL_GetWindowSize(
				overrideWindowHandle,
				out windowWidth,
				out windowHeight
			);

			if (scissorTestEnable)
			{
				GL.Disable(EnableCap.ScissorTest);
			}

			Framebuffer.BindReadFramebuffer(Backbuffer.Handle);
			Framebuffer.BindDrawFramebuffer(0);

			Framebuffer.BlitFramebuffer(
				Backbuffer.Width,
				Backbuffer.Height,
				windowWidth,
				windowHeight
			);

			Framebuffer.BindFramebuffer(0);

			if (scissorTestEnable)
			{
				GL.Enable(EnableCap.ScissorTest);
			}
#endif

			SDL.SDL_GL_SwapWindow(
				overrideWindowHandle
			);
			Framebuffer.BindFramebuffer(Backbuffer.Handle);
		}

		#endregion

		#region State Management Methods

		public void SetViewport(Viewport vp, bool renderTargetBound)
		{
			// Flip viewport when target is not bound
			if (!renderTargetBound)
			{
				vp.Y = Backbuffer.Height - vp.Y - vp.Height;
			}

			if (vp.Bounds != viewport)
			{
				viewport = vp.Bounds;
				GL.Viewport(
					viewport.X,
					viewport.Y,
					viewport.Width,
					viewport.Height
				);
			}

			if (vp.MinDepth != depthRangeMin || vp.MaxDepth != depthRangeMax)
			{
				depthRangeMin = vp.MinDepth;
				depthRangeMax = vp.MaxDepth;
				GL.DepthRange((double) depthRangeMin, (double) depthRangeMax);
			}
		}

		public void SetScissorRect(
			Rectangle scissorRect,
			bool renderTargetBound
		) {
			// Flip rectangle when target is not bound
			if (!renderTargetBound)
			{
				scissorRect.Y = viewport.Height - scissorRect.Y - scissorRect.Height;
			}

			if (scissorRect != scissorRectangle)
			{
				scissorRectangle = scissorRect;
				GL.Scissor(
					scissorRectangle.X,
					scissorRectangle.Y,
					scissorRectangle.Width,
					scissorRectangle.Height
				);
			}
		}

		public void SetBlendState(BlendState blendState)
		{
			bool newEnable = (
				!(	blendState.ColorSourceBlend == Blend.One &&
					blendState.ColorDestinationBlend == Blend.Zero &&
					blendState.AlphaSourceBlend == Blend.One &&
					blendState.AlphaDestinationBlend == Blend.Zero	)
			);
			if (newEnable != alphaBlendEnable)
			{
				alphaBlendEnable = newEnable;
				ToggleGLState(EnableCap.Blend, alphaBlendEnable);
			}

			if (alphaBlendEnable)
			{
				if (blendState.BlendFactor != blendColor)
				{
					blendColor = blendState.BlendFactor;
					GL.BlendColor(
						blendColor.R / 255.0f,
						blendColor.G / 255.0f,
						blendColor.B / 255.0f,
						blendColor.A / 255.0f
					);
				}

				if (	blendState.ColorSourceBlend != srcBlend ||
					blendState.ColorDestinationBlend != dstBlend ||
					blendState.AlphaSourceBlend != srcBlendAlpha ||
					blendState.AlphaDestinationBlend != dstBlendAlpha	)
				{
					srcBlend = blendState.ColorSourceBlend;
					dstBlend = blendState.ColorDestinationBlend;
					srcBlendAlpha = blendState.AlphaSourceBlend;
					dstBlendAlpha = blendState.AlphaDestinationBlend;
					GL.BlendFuncSeparate(
						XNAToGL.BlendModeSrc[srcBlend],
						XNAToGL.BlendModeDst[dstBlend],
						XNAToGL.BlendModeSrc[srcBlendAlpha],
						XNAToGL.BlendModeDst[dstBlendAlpha]
					);
				}

				if (	blendState.ColorBlendFunction != blendOp ||
					blendState.AlphaBlendFunction != blendOpAlpha	)
				{
					blendOp = blendState.ColorBlendFunction;
					blendOpAlpha = blendState.AlphaBlendFunction;
					GL.BlendEquationSeparate(
						XNAToGL.BlendEquation[blendOp],
						XNAToGL.BlendEquation[blendOpAlpha]
					);
				}

				if (blendState.ColorWriteChannels != colorWriteEnable)
				{
					colorWriteEnable = blendState.ColorWriteChannels;
					GL.ColorMask(
						(colorWriteEnable & ColorWriteChannels.Red) != 0,
						(colorWriteEnable & ColorWriteChannels.Green) != 0,
						(colorWriteEnable & ColorWriteChannels.Blue) != 0,
						(colorWriteEnable & ColorWriteChannels.Alpha) != 0
					);
				}
			}
		}

		public void SetDepthStencilState(DepthStencilState depthStencilState)
		{
			if (depthStencilState.DepthBufferEnable != zEnable)
			{
				zEnable = depthStencilState.DepthBufferEnable;
				ToggleGLState(EnableCap.DepthTest, zEnable);
			}

			if (zEnable)
			{
				if (depthStencilState.DepthBufferWriteEnable != zWriteEnable)
				{
					zWriteEnable = depthStencilState.DepthBufferWriteEnable;
					GL.DepthMask(zWriteEnable);
				}

				if (depthStencilState.DepthBufferFunction != depthFunc)
				{
					depthFunc = depthStencilState.DepthBufferFunction;
					GL.DepthFunc(XNAToGL.DepthFunc[depthFunc]);
				}
			}

			if (depthStencilState.StencilEnable != stencilEnable)
			{
				stencilEnable = depthStencilState.StencilEnable;
				ToggleGLState(EnableCap.StencilTest, stencilEnable);
			}

			if (stencilEnable)
			{
				if (depthStencilState.StencilWriteMask != stencilWriteMask)
				{
					stencilWriteMask = depthStencilState.StencilWriteMask;
					GL.StencilMask(stencilWriteMask);
				}

				// TODO: Can we split StencilFunc/StencilOp up nicely? -flibit
				if (	depthStencilState.TwoSidedStencilMode != separateStencilEnable ||
					depthStencilState.ReferenceStencil != stencilRef ||
					depthStencilState.StencilMask != stencilMask ||
					depthStencilState.StencilFunction != stencilFunc ||
					depthStencilState.CounterClockwiseStencilFunction != ccwStencilFunc ||
					depthStencilState.StencilFail != stencilFail ||
					depthStencilState.StencilDepthBufferFail != stencilZFail ||
					depthStencilState.StencilPass != stencilPass ||
					depthStencilState.CounterClockwiseStencilFail != ccwStencilFail ||
					depthStencilState.CounterClockwiseStencilDepthBufferFail != ccwStencilZFail ||
					depthStencilState.CounterClockwiseStencilPass != ccwStencilPass	)
				{
					separateStencilEnable = depthStencilState.TwoSidedStencilMode;
					stencilRef = depthStencilState.ReferenceStencil;
					stencilMask = depthStencilState.StencilMask;
					stencilFunc = depthStencilState.StencilFunction;
					stencilFail = depthStencilState.StencilFail;
					stencilZFail = depthStencilState.StencilDepthBufferFail;
					stencilPass = depthStencilState.StencilPass;
					if (separateStencilEnable)
					{
						ccwStencilFunc = depthStencilState.CounterClockwiseStencilFunction;
						ccwStencilFail = depthStencilState.CounterClockwiseStencilFail;
						ccwStencilZFail = depthStencilState.CounterClockwiseStencilDepthBufferFail;
						ccwStencilPass = depthStencilState.CounterClockwiseStencilPass;
						GL.StencilFuncSeparate(
							(Version20) CullFaceMode.Front,
							XNAToGL.StencilFunc[stencilFunc],
							stencilRef,
							stencilMask
						);
						GL.StencilFuncSeparate(
							(Version20) CullFaceMode.Back,
							XNAToGL.StencilFunc[ccwStencilFunc],
							stencilRef,
							stencilMask
						);
						GL.StencilOpSeparate(
							StencilFace.Front,
							XNAToGL.GLStencilOp[stencilFail],
							XNAToGL.GLStencilOp[stencilZFail],
							XNAToGL.GLStencilOp[stencilPass]
						);
						GL.StencilOpSeparate(
							StencilFace.Back,
							XNAToGL.GLStencilOp[ccwStencilFail],
							XNAToGL.GLStencilOp[ccwStencilZFail],
							XNAToGL.GLStencilOp[ccwStencilPass]
						);
					}
					else
					{
						GL.StencilFunc(
							XNAToGL.StencilFunc[stencilFunc],
							stencilRef,
							stencilMask
						);
						GL.StencilOp(
							XNAToGL.GLStencilOp[stencilFail],
							XNAToGL.GLStencilOp[stencilZFail],
							XNAToGL.GLStencilOp[stencilPass]
						);
					}
				}
			}
		}

		public void ApplyRasterizerState(
			RasterizerState rasterizerState,
			bool renderTargetBound
		) {
			if (rasterizerState.ScissorTestEnable != scissorTestEnable)
			{
				scissorTestEnable = rasterizerState.ScissorTestEnable;
				ToggleGLState(EnableCap.ScissorTest, scissorTestEnable);
			}

			CullMode actualMode;
			if (renderTargetBound)
			{
				actualMode = rasterizerState.CullMode;
			}
			else
			{
				// When not rendering offscreen the faces change order.
				if (rasterizerState.CullMode == CullMode.None)
				{
					actualMode = rasterizerState.CullMode;
				}
				else
				{
					actualMode = (
						rasterizerState.CullMode == CullMode.CullClockwiseFace ?
							CullMode.CullCounterClockwiseFace :
							CullMode.CullClockwiseFace
					);
				}
			}
			if (actualMode != cullFrontFace)
			{
				if ((actualMode == CullMode.None) != (cullFrontFace == CullMode.None))
				{
					ToggleGLState(EnableCap.CullFace, actualMode != CullMode.None);
					if (actualMode != CullMode.None)
					{
						// FIXME: XNA/MonoGame-specific behavior? -flibit
						GL.CullFace(CullFaceMode.Back);
					}
				}
				cullFrontFace = actualMode;
				if (cullFrontFace != CullMode.None)
				{
					GL.FrontFace(XNAToGL.FrontFace[cullFrontFace]);
				}
			}

			if (rasterizerState.FillMode != fillMode)
			{
				fillMode = rasterizerState.FillMode;
				GL.PolygonMode(
					MaterialFace.FrontAndBack,
					XNAToGL.GLFillMode[fillMode]
				);
			}

			if (zEnable)
			{
				if (	rasterizerState.DepthBias != depthBias ||
					rasterizerState.SlopeScaleDepthBias != slopeScaleDepthBias	)
				{
					depthBias = rasterizerState.DepthBias;
					slopeScaleDepthBias = rasterizerState.SlopeScaleDepthBias;
					if (depthBias == 0.0f && slopeScaleDepthBias == 0.0f)
					{
						ToggleGLState(EnableCap.PolygonOffsetFill, false);
					}
					else
					{
						ToggleGLState(EnableCap.PolygonOffsetFill, true);
						GL.PolygonOffset(slopeScaleDepthBias, depthBias);
					}
				}
			}
		}

		public void VerifySampler(int index, Texture texture, SamplerState sampler)
		{
			if (texture == null)
			{
				if (Textures[index] != OpenGLTexture.NullTexture)
				{
					if (index != 0)
					{
						GL.ActiveTexture(TextureUnit.Texture0 + index);
					}
					GL.BindTexture(Textures[index].Target, 0);
					if (index != 0)
					{
						// Keep this state sane. -flibit
						GL.ActiveTexture(TextureUnit.Texture0);
					}
					Textures[index] = OpenGLTexture.NullTexture;
				}
				return;
			}

			if (	texture.texture == Textures[index] &&
				sampler.AddressU == texture.texture.WrapS &&
				sampler.AddressV == texture.texture.WrapT &&
				sampler.AddressW == texture.texture.WrapR &&
				sampler.Filter == texture.texture.Filter &&
				sampler.MaxAnisotropy == texture.texture.Anistropy &&
				sampler.MaxMipLevel == texture.texture.MaxMipmapLevel &&
				sampler.MipMapLevelOfDetailBias == texture.texture.LODBias	)
			{
				// Nothing's changing, forget it.
				return;
			}

			// Set the active texture slot
			if (index != 0)
			{
				GL.ActiveTexture(TextureUnit.Texture0 + index);
			}

			// Bind the correct texture
			if (texture.texture != Textures[index])
			{
				if (texture.texture.Target != Textures[index].Target)
				{
					// If we're changing targets, unbind the old texture first!
					GL.BindTexture(Textures[index].Target, 0);
				}
				GL.BindTexture(texture.texture.Target, texture.texture.Handle);
				Textures[index] = texture.texture;
			}

			// Apply the sampler states to the GL texture
			if (sampler.AddressU != texture.texture.WrapS)
			{
				texture.texture.WrapS = sampler.AddressU;
				GL.TexParameter(
					texture.texture.Target,
					TextureParameterName.TextureWrapS,
					(int) XNAToGL.Wrap[texture.texture.WrapS]
				);
			}
			if (sampler.AddressV != texture.texture.WrapT)
			{
				texture.texture.WrapT = sampler.AddressV;
				GL.TexParameter(
					texture.texture.Target,
					TextureParameterName.TextureWrapT,
					(int) XNAToGL.Wrap[texture.texture.WrapT]
				);
			}
			if (sampler.AddressW != texture.texture.WrapR)
			{
				texture.texture.WrapR = sampler.AddressW;
				GL.TexParameter(
					texture.texture.Target,
					TextureParameterName.TextureWrapR,
					(int) XNAToGL.Wrap[texture.texture.WrapR]
				);
			}
			if (	sampler.Filter != texture.texture.Filter ||
				sampler.MaxAnisotropy != texture.texture.Anistropy	)
			{
				texture.texture.Filter = sampler.Filter;
				texture.texture.Anistropy = sampler.MaxAnisotropy;
				GL.TexParameter(
					texture.texture.Target,
					TextureParameterName.TextureMagFilter,
					(int) XNAToGL.MagFilter[texture.texture.Filter]
				);
				GL.TexParameter(
					texture.texture.Target,
					TextureParameterName.TextureMinFilter,
					(int) (
						texture.texture.HasMipmaps ?
							XNAToGL.MinMipFilter[texture.texture.Filter] :
							XNAToGL.MinFilter[texture.texture.Filter]
					)
				);
				GL.TexParameter(
					texture.texture.Target,
					(TextureParameterName) ExtTextureFilterAnisotropic.TextureMaxAnisotropyExt,
					(texture.texture.Filter == TextureFilter.Anisotropic) ?
						Math.Max(texture.texture.Anistropy, 1.0f) :
						1.0f
				);
			}
			if (sampler.MaxMipLevel != texture.texture.MaxMipmapLevel)
			{
				texture.texture.MaxMipmapLevel = sampler.MaxMipLevel;
				GL.TexParameter(
					texture.texture.Target,
					TextureParameterName.TextureBaseLevel,
					texture.texture.MaxMipmapLevel
				);
			}
			if (sampler.MipMapLevelOfDetailBias != texture.texture.LODBias)
			{
				texture.texture.LODBias = sampler.MipMapLevelOfDetailBias;
				GL.TexParameter(
					texture.texture.Target,
					TextureParameterName.TextureLodBias,
					texture.texture.LODBias
				);
			}

			if (index != 0)
			{
				// Keep this state sane. -flibit
				GL.ActiveTexture(TextureUnit.Texture0);
			}
		}

		#endregion

		#region Flush Vertex Attributes Method

		public void FlushGLVertexAttributes()
		{
			for (int i = 0; i < Attributes.Length; i += 1)
			{
				if (AttributeEnabled[i])
				{
					AttributeEnabled[i] = false;
					if (!previousAttributeEnabled[i])
					{
						GL.EnableVertexAttribArray(i);
						previousAttributeEnabled[i] = true;
					}
				}
				else if (previousAttributeEnabled[i])
				{
					GL.DisableVertexAttribArray(i);
					previousAttributeEnabled[i] = false;
				}

				if (Attributes[i].Divisor != previousAttributeDivisor[i])
				{
					GL.VertexAttribDivisor(i, Attributes[i].Divisor);
					previousAttributeDivisor[i] = Attributes[i].Divisor;
				}
			}
		}

		#endregion

		#region glVertexAttribPointer Method

		public void VertexAttribPointer(
			int location,
			int size,
			VertexAttribPointerType type,
			bool normalized,
			int stride,
			IntPtr pointer
		) {
			if (	Attributes[location].CurrentBuffer != currentVertexBuffer ||
				Attributes[location].CurrentPointer != pointer ||
				Attributes[location].CurrentSize != size ||
				Attributes[location].CurrentType != type ||
				Attributes[location].CurrentNormalized != normalized ||
				Attributes[location].CurrentStride != stride	)
			{
				GL.VertexAttribPointer(
					location,
					size,
					type,
					normalized,
					stride,
					pointer
				);
				Attributes[location].CurrentBuffer = currentVertexBuffer;
				Attributes[location].CurrentPointer = pointer;
				Attributes[location].CurrentSize = size;
				Attributes[location].CurrentType = type;
				Attributes[location].CurrentNormalized = normalized;
				Attributes[location].CurrentStride = stride;
			}
		}

		#endregion

		#region glBindBuffer Methods

		public void BindVertexBuffer(OpenGLVertexBuffer buffer)
		{
			if (buffer.Handle != currentVertexBuffer)
			{
				GL.BindBuffer(BufferTarget.ArrayBuffer, buffer.Handle);
				currentVertexBuffer = buffer.Handle;
			}
		}

		public void BindIndexBuffer(OpenGLIndexBuffer buffer)
		{
			if (buffer.Handle != currentIndexBuffer)
			{
				GL.BindBuffer(BufferTarget.ElementArrayBuffer, buffer.Handle);
				currentIndexBuffer = buffer.Handle;
			}
		}

		#endregion

		#region glSetBufferData Methods

		public void SetVertexBufferData<T>(
			OpenGLVertexBuffer handle,
			int bufferSize,
			int elementSizeInBytes,
			int offsetInBytes,
			T[] data,
			int startIndex,
			int elementCount,
			int vertexStride,
			SetDataOptions options
		) where T : struct {
			BindVertexBuffer(handle);

			if (options == SetDataOptions.Discard)
			{
				GL.BufferData(
					BufferTarget.ArrayBuffer,
					(IntPtr) bufferSize,
					IntPtr.Zero,
					handle.Dynamic
				);
			}

			GCHandle dataHandle = GCHandle.Alloc(data, GCHandleType.Pinned);

			GL.BufferSubData(
				BufferTarget.ArrayBuffer,
				(IntPtr) offsetInBytes,
				(IntPtr) (elementSizeInBytes * elementCount),
				(IntPtr) (dataHandle.AddrOfPinnedObject().ToInt64() + startIndex * elementSizeInBytes)
			);

			dataHandle.Free();
		}

		public void SetIndexBufferData<T>(
			OpenGLIndexBuffer handle,
			int offsetInBytes,
			T[] data,
			int startIndex,
			int elementCount,
			SetDataOptions options
		) where T : struct {
			BindIndexBuffer(handle);

			if (options == SetDataOptions.Discard)
			{
				GL.BufferData(
					BufferTarget.ElementArrayBuffer,
					handle.BufferSize,
					IntPtr.Zero,
					handle.Dynamic
				);
			}

			GCHandle dataHandle = GCHandle.Alloc(data, GCHandleType.Pinned);

			int elementSizeInBytes = Marshal.SizeOf(typeof(T));
			GL.BufferSubData(
				BufferTarget.ElementArrayBuffer,
				(IntPtr) offsetInBytes,
				(IntPtr) (elementSizeInBytes * elementCount),
				(IntPtr) (dataHandle.AddrOfPinnedObject().ToInt64() + startIndex * elementSizeInBytes)
			);

			dataHandle.Free();
		}

		#endregion

		#region glGetBufferData Methods

		public void GetVertexBufferData<T>(
			OpenGLVertexBuffer handle,
			int offsetInBytes,
			T[] data,
			int startIndex,
			int elementCount,
			int vertexStride
		) where T : struct {
			BindVertexBuffer(handle);

			IntPtr ptr = GL.MapBuffer(BufferTarget.ArrayBuffer, BufferAccess.ReadOnly);

			// Pointer to the start of data to read in the index buffer
			ptr = new IntPtr(ptr.ToInt64() + offsetInBytes);

			if (typeof(T) == typeof(byte))
			{
				/* If data is already a byte[] we can skip the temporary buffer.
				 * Copy from the vertex buffer to the destination array.
				 */
				byte[] buffer = data as byte[];
				Marshal.Copy(ptr, buffer, 0, buffer.Length);
			}
			else
			{
				// Temporary buffer to store the copied section of data
				byte[] buffer = new byte[elementCount * vertexStride - offsetInBytes];

				// Copy from the vertex buffer to the temporary buffer
				Marshal.Copy(ptr, buffer, 0, buffer.Length);

				GCHandle dataHandle = GCHandle.Alloc(data, GCHandleType.Pinned);
				IntPtr dataPtr = (IntPtr) (dataHandle.AddrOfPinnedObject().ToInt64() + startIndex * Marshal.SizeOf(typeof(T)));

				// Copy from the temporary buffer to the destination array
				int dataSize = Marshal.SizeOf(typeof(T));
				if (dataSize == vertexStride)
				{
					Marshal.Copy(buffer, 0, dataPtr, buffer.Length);
				}
				else
				{
					// If the user is asking for a specific element within the vertex buffer, copy them one by one...
					for (int i = 0; i < elementCount; i += 1)
					{
						Marshal.Copy(buffer, i * vertexStride, dataPtr, dataSize);
						dataPtr = (IntPtr)(dataPtr.ToInt64() + dataSize);
					}
				}

				dataHandle.Free();
			}

			GL.UnmapBuffer(BufferTarget.ArrayBuffer);
		}

		public void GetIndexBufferData<T>(
			OpenGLIndexBuffer handle,
			int offsetInBytes,
			T[] data,
			int startIndex,
			int elementCount
		) where T : struct {
			BindIndexBuffer(handle);

			IntPtr ptr = GL.MapBuffer(BufferTarget.ElementArrayBuffer, BufferAccess.ReadOnly);

			// Pointer to the start of data to read in the index buffer
			ptr = new IntPtr(ptr.ToInt64() + offsetInBytes);

			/* If data is already a byte[] we can skip the temporary buffer.
			 * Copy from the index buffer to the destination array.
			 */
			if (typeof(T) == typeof(byte))
			{
				byte[] buffer = data as byte[];
				Marshal.Copy(ptr, buffer, 0, buffer.Length);
			}
			else
			{
				int elementSizeInBytes = Marshal.SizeOf(typeof(T));
				byte[] buffer = new byte[elementCount * elementSizeInBytes];
				Marshal.Copy(ptr, buffer, 0, buffer.Length);
				Buffer.BlockCopy(buffer, 0, data, startIndex * elementSizeInBytes, elementCount * elementSizeInBytes);
			}

			GL.UnmapBuffer(BufferTarget.ArrayBuffer);
		}

		#endregion

		#region glDeleteBuffers Methods

		public void DeleteVertexBuffer(OpenGLVertexBuffer buffer)
		{
			if (buffer.Handle == currentVertexBuffer)
			{
				GL.BindBuffer(BufferTarget.ArrayBuffer, 0);
				currentVertexBuffer = 0;
			}
			GL.DeleteBuffer(0);
		}

		public void DeleteIndexBuffer(OpenGLIndexBuffer buffer)
		{
			if (buffer.Handle == currentIndexBuffer)
			{
				GL.BindBuffer(BufferTarget.ElementArrayBuffer, 0);
				currentIndexBuffer = 0;
			}
			GL.DeleteBuffer(0);
		}

		#endregion

		#region glCreateTexture Method

		public OpenGLTexture CreateTexture(Type target, SurfaceFormat format, bool hasMipmaps)
		{
			OpenGLTexture result = new OpenGLTexture(
				XNAToGL.TextureType[target],
				format,
				hasMipmaps
			);
			BindTexture(result);
			GL.TexParameter(
				result.Target,
				TextureParameterName.TextureWrapS,
				(int) XNAToGL.Wrap[result.WrapS]
			);
			GL.TexParameter(
				result.Target,
				TextureParameterName.TextureWrapT,
				(int) XNAToGL.Wrap[result.WrapT]
			);
			GL.TexParameter(
				result.Target,
				TextureParameterName.TextureWrapR,
				(int) XNAToGL.Wrap[result.WrapR]
			);
			GL.TexParameter(
				result.Target,
				TextureParameterName.TextureMagFilter,
				(int) XNAToGL.MagFilter[result.Filter]
			);
			GL.TexParameter(
				result.Target,
				TextureParameterName.TextureMinFilter,
				(int) (result.HasMipmaps ? XNAToGL.MinMipFilter[result.Filter] : XNAToGL.MinFilter[result.Filter])
			);
			GL.TexParameter(
				result.Target,
				(TextureParameterName) ExtTextureFilterAnisotropic.TextureMaxAnisotropyExt,
				(result.Filter == TextureFilter.Anisotropic) ? Math.Max(result.Anistropy, 1.0f) : 1.0f
			);
			GL.TexParameter(
				result.Target,
				TextureParameterName.TextureBaseLevel,
				result.MaxMipmapLevel
			);
			GL.TexParameter(
				result.Target,
				TextureParameterName.TextureLodBias,
				result.LODBias
			);
			return result;
		}

		#endregion

		#region glBindTexture Method

		public void BindTexture(OpenGLTexture texture)
		{
			if (texture.Target != Textures[0].Target)
			{
				GL.BindTexture(Textures[0].Target, 0);
			}
			if (texture != Textures[0])
			{
				GL.BindTexture(
					texture.Target,
					texture.Handle
				);
			}
			Textures[0] = texture;
		}

		#endregion

		#region glDeleteTexture Method

		public void DeleteTexture(OpenGLTexture texture)
		{
			for (int i = 0; i < currentAttachments.Length; i += 1)
			{
				if (texture.Handle == currentAttachments[i])
				{
					// Force an attachment update, this no longer exists!
					currentAttachments[i] = -1;
				}
			}
			texture.Dispose();
		}

		#endregion

		#region glReadPixels Method

		/// <summary>
		/// Attempts to read the texture data directly from the FBO using GL.ReadPixels
		/// </summary>
		/// <typeparam name="T">Texture data type</typeparam>
		/// <param name="texture">The texture to read from</param>
		/// <param name="level">The texture level</param>
		/// <param name="data">The texture data array</param>
		/// <param name="rect">The portion of the image to read from</param>
		/// <returns>True if we successfully read the texture data</returns>
		public bool ReadTargetIfApplicable<T>(
			OpenGLTexture texture,
			int level,
			T[] data,
			Rectangle? rect
		) where T : struct {
			if (	currentDrawBuffers == 1 &&
				currentAttachments != null &&				
				currentAttachments[0] == texture.Handle	)
			{
				int oldReadFramebuffer = Framebuffer.CurrentReadFramebuffer;
				if (oldReadFramebuffer != targetFramebuffer)
				{
					Framebuffer.BindReadFramebuffer(targetFramebuffer);
				}

				/* glReadPixels should be faster than reading
				 * back from the render target if we are already bound.
				 */
				if (rect.HasValue)
				{
					GL.ReadPixels(
						rect.Value.Left,
						rect.Value.Top,
						rect.Value.Width,
						rect.Value.Height,
						PixelFormat.Rgba,
						PixelType.UnsignedByte,
						data
					);
				}
				else
				{
					// FIXME: Using two glGet calls here! D:
					int width = 0;
					int height = 0;
					BindTexture(texture);
					GL.GetTexLevelParameter(
						texture.Target,
						level,
						GetTextureParameter.TextureWidth,
						out width
					);
					GL.GetTexLevelParameter(
						texture.Target,
						level,
						GetTextureParameter.TextureHeight,
						out height
					);

					GL.ReadPixels(
						0,
						0,
						width,
						height,
						PixelFormat.Rgba,
						PixelType.UnsignedByte,
						data
					);
				}
				Framebuffer.BindReadFramebuffer(oldReadFramebuffer);
				return true;
			}
			return false;
		}

		#endregion

		#region glDeleteRenderbuffer Method

		public void DeleteRenderbuffer(uint renderbuffer)
		{
			if (renderbuffer == currentRenderbuffer)
			{
				// Force a renderbuffer update, this no longer exists!
				currentRenderbuffer = uint.MaxValue;
			}
			Framebuffer.DeleteRenderbuffer(renderbuffer);
		}

		#endregion

		#region glEnable/glDisable Method

		private void ToggleGLState(EnableCap feature, bool enable)
		{
			if (enable)
			{
				GL.Enable(feature);
			}
			else
			{
				GL.Disable(feature);
			}
		}

		#endregion

		#region glClear Method

		public void Clear(ClearOptions options, Vector4 color, float depth, int stencil)
		{
			// Move some stuff around so the glClear works...
			if (scissorTestEnable)
			{
				GL.Disable(EnableCap.ScissorTest);
			}
			if (!zWriteEnable)
			{
				GL.DepthMask(true);
			}
			if (stencilWriteMask != -1)
			{
				// AKA 0xFFFFFFFF, ugh -flibit
				GL.StencilMask(-1);
			}

			// Get the clear mask, set the clear properties if needed
			ClearBufferMask clearMask = 0;
			if ((options & ClearOptions.Target) == ClearOptions.Target)
			{
				clearMask |= ClearBufferMask.ColorBufferBit;
				if (!color.Equals(currentClearColor))
				{
					GL.ClearColor(
						color.X,
						color.Y,
						color.Z,
						color.W
					);
					currentClearColor = color;
				}
			}
			if ((options & ClearOptions.DepthBuffer) == ClearOptions.DepthBuffer)
			{
				clearMask |= ClearBufferMask.DepthBufferBit;
				if (depth != currentClearDepth)
				{
					GL.ClearDepth((double) depth);
					currentClearDepth = depth;
				}
			}
			if ((options & ClearOptions.Stencil) == ClearOptions.Stencil)
			{
				clearMask |= ClearBufferMask.StencilBufferBit;
				if (stencil != currentClearStencil)
				{
					GL.ClearStencil(stencil);
					currentClearStencil = stencil;
				}
			}

			// CLEAR!
			GL.Clear(clearMask);

			// Clean up after ourselves.
			if (scissorTestEnable)
			{
				GL.Enable(EnableCap.ScissorTest);
			}
			if (!zWriteEnable)
			{
				GL.DepthMask(false);
			}
			if (stencilWriteMask != -1) // AKA 0xFFFFFFFF, ugh -flibit
			{
				GL.StencilMask(stencilWriteMask);
			}
		}

		#endregion

		#region SetRenderTargets Method

		public void SetRenderTargets(
			int[] attachments,
			TextureTarget[] textureTargets,
			uint renderbuffer,
			DepthFormat depthFormat
		) {
			// Bind the right framebuffer, if needed
			if (attachments == null)
			{
				Framebuffer.BindFramebuffer(Backbuffer.Handle);
				return;
			}
			else
			{
				Framebuffer.BindFramebuffer(targetFramebuffer);
			}

			// Update the color attachments, DrawBuffers state
			int i = 0;
			for (i = 0; i < attachments.Length; i += 1)
			{
				if (	attachments[i] != currentAttachments[i] ||
					textureTargets[i] != currentAttachmentFaces[i]	)
				{
					Framebuffer.AttachColor(attachments[i], i, textureTargets[i]);
					currentAttachments[i] = attachments[i];
					currentAttachmentFaces[i] = textureTargets[i];
				}
			}
			while (i < currentAttachments.Length)
			{
				if (currentAttachments[i] != 0)
				{
					Framebuffer.AttachColor(0, i, TextureTarget.Texture2D);
					currentAttachments[i] = 0;
					currentAttachmentFaces[i] = TextureTarget.Texture2D;
				}
				i += 1;
			}
			if (attachments.Length != currentDrawBuffers)
			{
				GL.DrawBuffers(attachments.Length, drawBuffersArray);
				currentDrawBuffers = attachments.Length;
			}

			// Update the depth/stencil attachment
			/* FIXME: Notice that we do separate attach calls for the stencil.
			 * We _should_ be able to do a single attach for depthstencil, but
			 * some drivers (like Mesa) cannot into GL_DEPTH_STENCIL_ATTACHMENT.
			 * Use XNAToGL.DepthStencilAttachment when this isn't a problem.
			 * -flibit
			 */
			if (renderbuffer != currentRenderbuffer)
			{
				if (currentDepthStencilFormat == DepthFormat.Depth24Stencil8)
				{
					Framebuffer.AttachDepthRenderbuffer(
						0,
						FramebufferAttachment.StencilAttachment
					);
				}
				currentDepthStencilFormat = depthFormat;
				Framebuffer.AttachDepthRenderbuffer(
					renderbuffer,
					FramebufferAttachment.DepthAttachment
				);
				if (currentDepthStencilFormat == DepthFormat.Depth24Stencil8)
				{
					Framebuffer.AttachDepthRenderbuffer(
						renderbuffer,
						FramebufferAttachment.StencilAttachment
					);
				}
				currentRenderbuffer = renderbuffer;
			}
		}

		#endregion

		#region XNA->GL Enum Conversion Class

		private static class XNAToGL
		{
			/* Ideally we would be using arrays, rather than Dictionaries.
			 * The problem is that we don't support every enum, and dealing
			 * with gaps would be a headache. So whatever, Dictionaries!
			 * -flibit
			 */

			public static readonly Dictionary<Type, TextureTarget> TextureType = new Dictionary<Type, TextureTarget>()
			{
				{ typeof(Texture2D), TextureTarget.Texture2D },
				{ typeof(Texture3D), TextureTarget.Texture3D },
				{ typeof(TextureCube), TextureTarget.TextureCubeMap }
			};

			public static readonly Dictionary<Blend, BlendingFactorSrc> BlendModeSrc = new Dictionary<Blend, BlendingFactorSrc>()
			{
				{ Blend.DestinationAlpha,		BlendingFactorSrc.DstAlpha },
				{ Blend.DestinationColor,		BlendingFactorSrc.DstColor },
				{ Blend.InverseDestinationAlpha,	BlendingFactorSrc.OneMinusDstAlpha },
				{ Blend.InverseDestinationColor,	BlendingFactorSrc.OneMinusDstColor },
				{ Blend.InverseSourceAlpha,		BlendingFactorSrc.OneMinusSrcAlpha },
				{ Blend.InverseSourceColor,		(BlendingFactorSrc) All.OneMinusSrcColor }, // Why -flibit
				{ Blend.One,				BlendingFactorSrc.One },
				{ Blend.SourceAlpha,			BlendingFactorSrc.SrcAlpha },
				{ Blend.SourceAlphaSaturation,		BlendingFactorSrc.SrcAlphaSaturate },
				{ Blend.SourceColor,			(BlendingFactorSrc) All.SrcColor }, // Why -flibit
				{ Blend.Zero,				BlendingFactorSrc.Zero }
			};

			public static readonly Dictionary<Blend, BlendingFactorDest> BlendModeDst = new Dictionary<Blend, BlendingFactorDest>()
			{
				{ Blend.DestinationAlpha,		BlendingFactorDest.DstAlpha },
				{ Blend.InverseDestinationAlpha,	BlendingFactorDest.OneMinusDstAlpha },
				{ Blend.InverseSourceAlpha,		BlendingFactorDest.OneMinusSrcAlpha },
				{ Blend.InverseSourceColor,		BlendingFactorDest.OneMinusSrcColor },
				{ Blend.One,				BlendingFactorDest.One },
				{ Blend.SourceAlpha,			BlendingFactorDest.SrcAlpha },
				{ Blend.SourceColor,			BlendingFactorDest.SrcColor },
				{ Blend.Zero,				BlendingFactorDest.Zero }
			};

			public static readonly Dictionary<BlendFunction, BlendEquationMode> BlendEquation = new Dictionary<BlendFunction, BlendEquationMode>()
			{
				{ BlendFunction.Add,			BlendEquationMode.FuncAdd },
				{ BlendFunction.Max,			BlendEquationMode.Max },
				{ BlendFunction.Min,			BlendEquationMode.Min },
				{ BlendFunction.ReverseSubtract,	BlendEquationMode.FuncReverseSubtract },
				{ BlendFunction.Subtract,		BlendEquationMode.FuncSubtract }
			};

			public static readonly Dictionary<CompareFunction, DepthFunction> DepthFunc = new Dictionary<CompareFunction, DepthFunction>()
			{
				{ CompareFunction.Always,	DepthFunction.Always },
				{ CompareFunction.Equal,	DepthFunction.Equal },
				{ CompareFunction.Greater,	DepthFunction.Greater },
				{ CompareFunction.GreaterEqual,	DepthFunction.Gequal },
				{ CompareFunction.Less,		DepthFunction.Less },
				{ CompareFunction.LessEqual,	DepthFunction.Lequal },
				{ CompareFunction.Never,	DepthFunction.Never },
				{ CompareFunction.NotEqual,	DepthFunction.Notequal }
			};

			public static readonly Dictionary<CompareFunction, StencilFunction> StencilFunc = new Dictionary<CompareFunction, StencilFunction>()
			{
				{ CompareFunction.Always,	StencilFunction.Always },
				{ CompareFunction.Equal,	StencilFunction.Equal },
				{ CompareFunction.Greater,	StencilFunction.Greater },
				{ CompareFunction.GreaterEqual,	StencilFunction.Gequal },
				{ CompareFunction.Less,		StencilFunction.Less },
				{ CompareFunction.LessEqual,	StencilFunction.Lequal },
				{ CompareFunction.Never,	StencilFunction.Never },
				{ CompareFunction.NotEqual,	StencilFunction.Notequal }
			};

			public static readonly Dictionary<StencilOperation, StencilOp> GLStencilOp = new Dictionary<StencilOperation, StencilOp>()
			{
				{ StencilOperation.Decrement,		StencilOp.DecrWrap },
				{ StencilOperation.DecrementSaturation,	StencilOp.Decr },
				{ StencilOperation.Increment,		StencilOp.IncrWrap },
				{ StencilOperation.IncrementSaturation,	StencilOp.Incr },
				{ StencilOperation.Invert,		StencilOp.Invert },
				{ StencilOperation.Keep,		StencilOp.Keep },
				{ StencilOperation.Replace,		StencilOp.Replace },
				{ StencilOperation.Zero,		StencilOp.Zero }
			};

			public static readonly Dictionary<CullMode, FrontFaceDirection> FrontFace = new Dictionary<CullMode, FrontFaceDirection>()
			{
				{ CullMode.CullClockwiseFace,		FrontFaceDirection.Cw },
				{ CullMode.CullCounterClockwiseFace,	FrontFaceDirection.Ccw }
			};

			public static readonly Dictionary<FillMode, PolygonMode> GLFillMode = new Dictionary<FillMode, PolygonMode>()
			{
				{ FillMode.Solid,	PolygonMode.Fill },
				{ FillMode.WireFrame,	PolygonMode.Line }
			};

			public static readonly Dictionary<TextureAddressMode, TextureWrapMode> Wrap = new Dictionary<TextureAddressMode, TextureWrapMode>()
			{
				{ TextureAddressMode.Clamp,	TextureWrapMode.ClampToEdge },
				{ TextureAddressMode.Mirror,	TextureWrapMode.MirroredRepeat },
				{ TextureAddressMode.Wrap,	TextureWrapMode.Repeat }
			};

			public static readonly Dictionary<TextureFilter, TextureMagFilter> MagFilter = new Dictionary<TextureFilter, TextureMagFilter>()
			{
				{ TextureFilter.Point,				TextureMagFilter.Nearest },
				{ TextureFilter.Linear,				TextureMagFilter.Linear },
				{ TextureFilter.Anisotropic,			TextureMagFilter.Linear },
				{ TextureFilter.LinearMipPoint,			TextureMagFilter.Linear },
				{ TextureFilter.MinPointMagLinearMipPoint,	TextureMagFilter.Linear },
				{ TextureFilter.MinPointMagLinearMipLinear,	TextureMagFilter.Linear },
				{ TextureFilter.MinLinearMagPointMipPoint,	TextureMagFilter.Nearest },
				{ TextureFilter.MinLinearMagPointMipLinear,	TextureMagFilter.Nearest }
			};

			public static readonly Dictionary<TextureFilter, TextureMinFilter> MinMipFilter = new Dictionary<TextureFilter, TextureMinFilter>()
			{
				{ TextureFilter.Point,				TextureMinFilter.NearestMipmapNearest },
				{ TextureFilter.Linear,				TextureMinFilter.LinearMipmapLinear },
				{ TextureFilter.Anisotropic,			TextureMinFilter.LinearMipmapLinear },
				{ TextureFilter.LinearMipPoint,			TextureMinFilter.LinearMipmapNearest },
				{ TextureFilter.MinPointMagLinearMipPoint,	TextureMinFilter.NearestMipmapNearest },
				{ TextureFilter.MinPointMagLinearMipLinear,	TextureMinFilter.NearestMipmapLinear },
				{ TextureFilter.MinLinearMagPointMipPoint,	TextureMinFilter.LinearMipmapNearest },
				{ TextureFilter.MinLinearMagPointMipLinear,	TextureMinFilter.LinearMipmapLinear }
			};

			public static readonly Dictionary<TextureFilter, TextureMinFilter> MinFilter = new Dictionary<TextureFilter, TextureMinFilter>()
			{
				{ TextureFilter.Point,				TextureMinFilter.Nearest },
				{ TextureFilter.Linear,				TextureMinFilter.Linear },
				{ TextureFilter.Anisotropic,			TextureMinFilter.Linear },
				{ TextureFilter.LinearMipPoint,			TextureMinFilter.Linear },
				{ TextureFilter.MinPointMagLinearMipPoint,	TextureMinFilter.Nearest },
				{ TextureFilter.MinPointMagLinearMipLinear,	TextureMinFilter.Nearest },
				{ TextureFilter.MinLinearMagPointMipPoint,	TextureMinFilter.Linear },
				{ TextureFilter.MinLinearMagPointMipLinear,	TextureMinFilter.Linear }
			};

			public static readonly Dictionary<DepthFormat, FramebufferAttachment> DepthStencilAttachment = new Dictionary<DepthFormat, FramebufferAttachment>()
			{
				{ DepthFormat.Depth16,		FramebufferAttachment.DepthAttachment },
				{ DepthFormat.Depth24,		FramebufferAttachment.DepthAttachment },
				{ DepthFormat.Depth24Stencil8,	FramebufferAttachment.DepthStencilAttachment }
			};

			public static readonly Dictionary<DepthFormat, RenderbufferStorage> DepthStorage = new Dictionary<DepthFormat, RenderbufferStorage>()
			{
				{ DepthFormat.Depth16,		RenderbufferStorage.DepthComponent16 },
				{ DepthFormat.Depth24,		RenderbufferStorage.DepthComponent24 },
				{ DepthFormat.Depth24Stencil8,	RenderbufferStorage.Depth24Stencil8 }
			};
		}

		#endregion

		#region Framebuffer ARB/EXT Wrapper Class

		public static class Framebuffer
		{
			private static int currentReadFramebuffer = 0;
			public static int CurrentReadFramebuffer
			{
				get
				{
					return currentReadFramebuffer;
				}
			}

			private static int currentDrawFramebuffer = 0;
			public static int CurrentDrawFramebuffer
			{
				get
				{
					return currentDrawFramebuffer;
				}
			}

			private static bool hasARB = false;

			public static void Initialize()
			{
				hasARB = (	SDL2.SDL.SDL_GL_GetProcAddress("glGenFramebuffers") != IntPtr.Zero &&
						SDL2.SDL.SDL_GL_GetProcAddress("glBlitFramebuffer") != IntPtr.Zero	);

				// If we don't have ARB_framebuffer_object, check for EXT as a fallback.
				if (!hasARB)
				{
					System.Console.WriteLine("ARB_framebuffer_object not found, falling back to EXT.");
					if (	SDL2.SDL.SDL_GL_GetProcAddress("glGenFramebuffersEXT") == IntPtr.Zero ||
						SDL2.SDL.SDL_GL_GetProcAddress("glBlitFramebufferEXT") == IntPtr.Zero	)
					{
						throw new NoSuitableGraphicsDeviceException("The graphics device does not support framebuffer objects.");
					}
				}
			}

			public static void Clear()
			{
				currentReadFramebuffer = 0;
				currentDrawFramebuffer = 0;
				hasARB = false;
			}

			public static int GenFramebuffer()
			{
				int handle;
				if (hasARB)
				{
					GL.GenFramebuffers(1, out handle);
				}
				else
				{
					GL.Ext.GenFramebuffers(1, out handle);
				}
				return handle;
			}

			public static void DeleteFramebuffer(int handle)
			{
				if (hasARB)
				{
					GL.DeleteFramebuffers(1, ref handle);
				}
				else
				{
					GL.Ext.DeleteFramebuffers(1, ref handle);
				}
			}

			public static void BindFramebuffer(int handle)
			{
				if (currentReadFramebuffer != handle && currentDrawFramebuffer != handle)
				{
					if (hasARB)
					{
						GL.BindFramebuffer(
							FramebufferTarget.Framebuffer,
							handle
						);
					}
					else
					{
						GL.Ext.BindFramebuffer(
							FramebufferTarget.FramebufferExt,
							handle
						);
					}

					currentReadFramebuffer = handle;
					currentDrawFramebuffer = handle;
				}
				else if (currentReadFramebuffer != handle)
				{
					BindReadFramebuffer(handle);
				}
				else if (currentDrawFramebuffer != handle)
				{
					BindDrawFramebuffer(handle);
				}
			}

			public static void BindReadFramebuffer(int handle)
			{
				if (handle == currentReadFramebuffer)
				{
					return;
				}

				if (hasARB)
				{
					GL.BindFramebuffer(
						FramebufferTarget.ReadFramebuffer,
						handle
					);
				}
				else
				{
					GL.Ext.BindFramebuffer(
						FramebufferTarget.ReadFramebuffer,
						handle
					);
				}

				currentReadFramebuffer = handle;
			}

			public static void BindDrawFramebuffer(int handle)
			{
				if (handle == currentDrawFramebuffer)
				{
					return;
				}

				if (hasARB)
				{
					GL.BindFramebuffer(
						FramebufferTarget.DrawFramebuffer,
						handle
					);
				}
				else
				{
					GL.Ext.BindFramebuffer(
						FramebufferTarget.DrawFramebuffer,
						handle
					);
				}

				currentDrawFramebuffer = handle;
			}

			public static uint GenRenderbuffer(int width, int height, DepthFormat format)
			{
				uint handle;
				if (hasARB)
				{
					GL.GenRenderbuffers(1, out handle);
					GL.BindRenderbuffer(RenderbufferTarget.Renderbuffer, handle);
					GL.RenderbufferStorage(
						RenderbufferTarget.Renderbuffer,
						XNAToGL.DepthStorage[format],
						width,
						height
					);
					GL.BindRenderbuffer(RenderbufferTarget.Renderbuffer, 0);
				}
				else
				{
					GL.Ext.GenRenderbuffers(1, out handle);
					GL.Ext.BindRenderbuffer(RenderbufferTarget.RenderbufferExt, handle);
					GL.Ext.RenderbufferStorage(
						RenderbufferTarget.RenderbufferExt,
						XNAToGL.DepthStorage[format],
						width,
						height
					);
					GL.Ext.BindRenderbuffer(RenderbufferTarget.RenderbufferExt, 0);
				}
				return handle;
			}

			public static void DeleteRenderbuffer(uint handle)
			{
				if (hasARB)
				{
					GL.DeleteRenderbuffers(1, ref handle);
				}
				else
				{
					GL.Ext.DeleteRenderbuffers(1, ref handle);
				}
			}

			public static void AttachColor(int colorAttachment, int index, TextureTarget target)
			{
				if (hasARB)
				{
					GL.FramebufferTexture2D(
						FramebufferTarget.Framebuffer,
						FramebufferAttachment.ColorAttachment0 + index,
						target,
						colorAttachment,
						0
					);
				}
				else
				{
					GL.Ext.FramebufferTexture2D(
						FramebufferTarget.FramebufferExt,
						FramebufferAttachment.ColorAttachment0Ext + index,
						target,
						colorAttachment,
						0
					);
				}
			}

			public static void AttachDepthTexture(
				int depthAttachment,
				FramebufferAttachment depthFormat
			) {
				if (hasARB)
				{
					GL.FramebufferTexture2D(
						FramebufferTarget.Framebuffer,
						depthFormat,
						TextureTarget.Texture2D,
						depthAttachment,
						0
					);
				}
				else
				{
					GL.Ext.FramebufferTexture2D(
						FramebufferTarget.FramebufferExt,
						depthFormat,
						TextureTarget.Texture2D,
						depthAttachment,
						0
					);
				}
			}

			public static void AttachDepthRenderbuffer(
				uint renderbuffer,
				FramebufferAttachment depthFormat
			) {
				if (hasARB)
				{
					GL.FramebufferRenderbuffer(
						FramebufferTarget.Framebuffer,
						depthFormat,
						RenderbufferTarget.Renderbuffer,
						renderbuffer
					);
				}
				else
				{
					GL.Ext.FramebufferRenderbuffer(
						FramebufferTarget.FramebufferExt,
						depthFormat,
						RenderbufferTarget.RenderbufferExt,
						renderbuffer
					);
				}
			}

			public static void BlitFramebuffer(
				int srcWidth,
				int srcHeight,
				int dstWidth,
				int dstHeight
			) {
				if (hasARB)
				{
					GL.BlitFramebuffer(
						0, 0, srcWidth, srcHeight,
						0, 0, dstWidth, dstHeight,
						ClearBufferMask.ColorBufferBit,
						BlitFramebufferFilter.Linear
					);
				}
				else
				{
					GL.Ext.BlitFramebuffer(
						0, 0, srcWidth, srcHeight,
						0, 0, dstWidth, dstHeight,
						ClearBufferMask.ColorBufferBit,
						BlitFramebufferFilter.Linear
					);
				}
			}
		}

		#endregion

		#region The Faux-Backbuffer

		public class FauxBackbuffer
		{
			public int Handle
			{
				get;
				private set;
			}

			public int Width
			{
				get;
				private set;
			}

			public int Height
			{
				get;
				private set;
			}

			private int colorAttachment;
			private int depthStencilAttachment;
			private DepthFormat depthStencilFormat;

			public FauxBackbuffer(int width, int height, DepthFormat depthFormat)
			{
#if DISABLE_FAUXBACKBUFFER
				Handle = 0;
				Width = width;
				Height = height;
#else
				Handle = Framebuffer.GenFramebuffer();
				colorAttachment = GL.GenTexture();
				depthStencilAttachment = GL.GenTexture();

				GL.BindTexture(TextureTarget.Texture2D, colorAttachment);
				GL.TexImage2D(
					TextureTarget.Texture2D,
					0,
					PixelInternalFormat.Rgba,
					width,
					height,
					0,
					PixelFormat.Rgba,
					PixelType.UnsignedByte,
					IntPtr.Zero
				);
				GL.BindTexture(TextureTarget.Texture2D, depthStencilAttachment);
				GL.TexImage2D(
					TextureTarget.Texture2D,
					0,
					PixelInternalFormat.DepthComponent16,
					width,
					height,
					0,
					PixelFormat.DepthComponent,
					PixelType.UnsignedByte,
					IntPtr.Zero
				);
				Framebuffer.BindFramebuffer(Handle);
				Framebuffer.AttachColor(
					colorAttachment,
					0,
					TextureTarget.Texture2D
				);
				Framebuffer.AttachDepthTexture(
					depthStencilAttachment,
					FramebufferAttachment.DepthAttachment
				);
				GL.BindTexture(TextureTarget.Texture2D, 0);

				Width = width;
				Height = height;
#endif
			}

			public void Dispose()
			{
#if !DISABLE_FAUXBACKBUFFER
				Framebuffer.DeleteFramebuffer(Handle);
				GL.DeleteTexture(colorAttachment);
				GL.DeleteTexture(depthStencilAttachment);
				Handle = 0;
#endif
			}

			public void ResetFramebuffer(
				GraphicsDevice graphicsDevice,
				int width,
				int height,
				DepthFormat depthFormat
			) {
#if DISABLE_FAUXBACKBUFFER
				Width = width;
				Height = height;
#else
				// Update our color attachment to the new resolution.
				GL.BindTexture(TextureTarget.Texture2D, colorAttachment);
				GL.TexImage2D(
					TextureTarget.Texture2D,
					0,
					PixelInternalFormat.Rgba,
					width,
					height,
					0,
					PixelFormat.Rgba,
					PixelType.UnsignedByte,
					IntPtr.Zero
				);

				// Update the depth attachment based on the desired DepthFormat.
				PixelFormat depthPixelFormat;
				PixelInternalFormat depthPixelInternalFormat;
				PixelType depthPixelType;
				FramebufferAttachment depthAttachmentType;
				if (depthFormat == DepthFormat.Depth16)
				{
					depthPixelFormat = PixelFormat.DepthComponent;
					depthPixelInternalFormat = PixelInternalFormat.DepthComponent16;
					depthPixelType = PixelType.UnsignedByte;
					depthAttachmentType = FramebufferAttachment.DepthAttachment;
				}
				else if (depthFormat == DepthFormat.Depth24)
				{
					depthPixelFormat = PixelFormat.DepthComponent;
					depthPixelInternalFormat = PixelInternalFormat.DepthComponent24;
					depthPixelType = PixelType.UnsignedByte;
					depthAttachmentType = FramebufferAttachment.DepthAttachment;
				}
				else
				{
					depthPixelFormat = PixelFormat.DepthStencil;
					depthPixelInternalFormat = PixelInternalFormat.Depth24Stencil8;
					depthPixelType = PixelType.UnsignedInt248;
					depthAttachmentType = FramebufferAttachment.DepthStencilAttachment;
				}

				GL.BindTexture(TextureTarget.Texture2D, depthStencilAttachment);
				GL.TexImage2D(
					TextureTarget.Texture2D,
					0,
					depthPixelInternalFormat,
					width,
					height,
					0,
					depthPixelFormat,
					depthPixelType,
					IntPtr.Zero
				);

				// If the depth format changes, detach before reattaching!
				if (depthFormat != depthStencilFormat)
				{
					FramebufferAttachment attach;
					if (depthStencilFormat == DepthFormat.Depth24Stencil8)
					{
						attach = FramebufferAttachment.DepthStencilAttachment;
					}
					else
					{
						attach = FramebufferAttachment.DepthAttachment;
					}

					Framebuffer.BindFramebuffer(Handle);

					Framebuffer.AttachDepthTexture(
						0,
						attach
					);
					Framebuffer.AttachDepthTexture(
						depthStencilAttachment,
						depthAttachmentType
					);

					if (graphicsDevice.RenderTargetCount > 0)
					{
						Framebuffer.BindFramebuffer(
							graphicsDevice.GLDevice.targetFramebuffer
						);
					}

					depthStencilFormat = depthFormat;
				}

				GL.BindTexture(
					TextureTarget.Texture2D,
					graphicsDevice.GLDevice.Textures[0].Handle
				);

				Width = width;
				Height = height;
#endif
			}
		}

		#endregion

		#region Private ARB_debug_output Wrapper

		private static class DebugOutput
		{
			private enum GLenum : uint
			{
				// Hint Enum Value
				GL_DONT_CARE =				0x1100,
				// Source Enum Values
				GL_DEBUG_SOURCE_API_ARB =		0x8246,
				GL_DEBUG_SOURCE_WINDOW_SYSTEM_ARB =	0x8247,
				GL_DEBUG_SOURCE_SHADER_COMPILER_ARB =	0x8248,
				GL_DEBUG_SOURCE_THIRD_PARTY_ARB =	0x8249,
				GL_DEBUG_SOURCE_APPLICATION_ARB =	0x824A,
				GL_DEBUG_SOURCE_OTHER_ARB =		0x824B,
				// Type Enum Values
				GL_DEBUG_TYPE_ERROR_ARB =		0x824C,
				GL_DEBUG_TYPE_DEPRECATED_BEHAVIOR_ARB =	0x824D,
				GL_DEBUG_TYPE_UNDEFINED_BEHAVIOR_ARB =	0x824E,
				GL_DEBUG_TYPE_PORTABILITY_ARB =		0x824F,
				GL_DEBUG_TYPE_PERFORMANCE_ARB =		0x8250,
				GL_DEBUG_TYPE_OTHER_ARB =		0x8251,
				// Severity Enum Values
				GL_DEBUG_SEVERITY_HIGH_ARB =		0x9146,
				GL_DEBUG_SEVERITY_MEDIUM_ARB =		0x9147,
				GL_DEBUG_SEVERITY_LOW_ARB =		0x9148,
			}

			// Entry Points
			private delegate void DebugMessageCallback(
				DebugProc callback,
				IntPtr userParam
			);
			private delegate void DebugMessageControl(
				GLenum source,
				GLenum type,
				GLenum severity,
				IntPtr count, // GLsizei
				IntPtr ids, // const GLuint*
				bool enabled
			);
			private static DebugMessageCallback glDebugMessageCallbackARB;
			private static DebugMessageControl glDebugMessageControlARB;

			// Function pointer
			private delegate void DebugProc(
				GLenum source,
				GLenum type,
				uint id,
				GLenum severity,
				IntPtr length, // GLsizei
				IntPtr message, // const GLchar*
				IntPtr userParam // const GLvoid*
			);
			private static DebugProc DebugCall = DebugCallback;

			// Debug callback
			private static void DebugCallback(
				GLenum source,
				GLenum type,
				uint id,
				GLenum severity,
				IntPtr length, // GLsizei
				IntPtr message, // const GLchar*
				IntPtr userParam // const GLvoid*
			) {
				System.Console.WriteLine(
					"{0}\n\tSource: {1}\n\tType: {2}\n\tSeverity: {3}",
					Marshal.PtrToStringAnsi(message),
					source.ToString(),
					type.ToString(),
					severity.ToString()
				);
				if (type == GLenum.GL_DEBUG_TYPE_ERROR_ARB)
				{
					throw new Exception("ARB_debug_output found an error.");
				}
			}

			[System.Diagnostics.ConditionalAttribute("DEBUG")]
			public static void Initialize()
			{
				IntPtr messageCallback = SDL2.SDL.SDL_GL_GetProcAddress("glDebugMessageCallbackARB");
				IntPtr messageControl = SDL2.SDL.SDL_GL_GetProcAddress("glDebugMessageControlARB");
				if (messageCallback == IntPtr.Zero || messageControl == IntPtr.Zero)
				{
					System.Console.WriteLine("ARB_debug_output not supported!");
					return;
				}
				glDebugMessageCallbackARB = (DebugMessageCallback) Marshal.GetDelegateForFunctionPointer(
					messageCallback,
					typeof(DebugMessageCallback)
				);
				glDebugMessageControlARB = (DebugMessageControl) Marshal.GetDelegateForFunctionPointer(
					messageControl,
					typeof(DebugMessageControl)
				);
				glDebugMessageCallbackARB(DebugCall, IntPtr.Zero);
				glDebugMessageControlARB(
					GLenum.GL_DONT_CARE,
					GLenum.GL_DONT_CARE,
					GLenum.GL_DONT_CARE,
					IntPtr.Zero,
					IntPtr.Zero,
					true
				);
				glDebugMessageControlARB(
					GLenum.GL_DONT_CARE,
					GLenum.GL_DEBUG_TYPE_OTHER_ARB,
					GLenum.GL_DEBUG_SEVERITY_LOW_ARB,
					IntPtr.Zero,
					IntPtr.Zero,
					false
				);
			}
		}

		#endregion
	}
}
