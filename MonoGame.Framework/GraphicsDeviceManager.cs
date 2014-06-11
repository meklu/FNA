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

using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input.Touch;
#endregion

namespace Microsoft.Xna.Framework
{
	public class GraphicsDeviceManager : IGraphicsDeviceService, IDisposable, IGraphicsDeviceManager
	{
		#region Public Properties

		public GraphicsProfile GraphicsProfile
		{ 
			get;
			set;
		}

		public GraphicsDevice GraphicsDevice
		{
			get
			{
				return graphicsDevice;
			}
		}

		public bool IsFullScreen
		{
			get;
			set;
		}

		public bool PreferMultiSampling
		{
			get;
			set;
		}

		public SurfaceFormat PreferredBackBufferFormat
		{
			get;
			set;
		}

		public int PreferredBackBufferHeight
		{
			get;
			set;
		}

		public int PreferredBackBufferWidth
		{
			get;
			set;
		}

		public DepthFormat PreferredDepthStencilFormat
		{
			get;
			set;
		}

		public bool SynchronizeWithVerticalRetrace
		{
			get;
			set;
		}

		public DisplayOrientation SupportedOrientations
		{
			get
			{
				return supportedOrientations;
			}
			set
			{
				supportedOrientations = value;
				if (game.Window != null)
				{
					game.Window.SetSupportedOrientations(supportedOrientations);
				}
			}
		}

		#endregion

		#region Private Variables

		private Game game;
		private GraphicsDevice graphicsDevice;
		private DisplayOrientation supportedOrientations;
		private bool drawBegun;
		private bool disposed;

		#endregion

		#region Public Static Fields

		public static readonly int DefaultBackBufferWidth = 800;
		public static readonly int DefaultBackBufferHeight = 480;

		#endregion

		#region IGraphicsDeviceService Events

		public event EventHandler<EventArgs> DeviceCreated;
		public event EventHandler<EventArgs> DeviceDisposing;
		public event EventHandler<EventArgs> DeviceReset;
		public event EventHandler<EventArgs> DeviceResetting;
		public event EventHandler<PreparingDeviceSettingsEventArgs> PreparingDeviceSettings;

		#endregion

		#region Public Constructor

		public GraphicsDeviceManager(Game game)
		{
			if (game == null)
			{
				throw new ArgumentNullException("The game cannot be null!");
			}

			this.game = game;

			supportedOrientations = DisplayOrientation.Default;

			PreferredBackBufferHeight = DefaultBackBufferHeight;
			PreferredBackBufferWidth = DefaultBackBufferWidth;

			PreferredBackBufferFormat = SurfaceFormat.Color;
			PreferredDepthStencilFormat = DepthFormat.Depth24;

			SynchronizeWithVerticalRetrace = true;

			if (game.Services.GetService(typeof(IGraphicsDeviceManager)) != null)
			{
				throw new ArgumentException("Graphics Device Manager Already Present");
			}

			game.Services.AddService(typeof(IGraphicsDeviceManager), this);
			game.Services.AddService(typeof(IGraphicsDeviceService), this);
		}

		#endregion

		#region Deconstructor

		~GraphicsDeviceManager()
		{
			Dispose(false);
		}

		#endregion

		#region Dispose Methods

		public void Dispose()
		{
			Dispose(true);
			GC.SuppressFinalize(this);
		}

		protected virtual void Dispose(bool disposing)
		{
			if (!disposed)
			{
				if (disposing)
				{
					if (graphicsDevice != null)
					{
						graphicsDevice.Dispose();
						graphicsDevice = null;
					}
				}
				disposed = true;
			}
		}

		#endregion

		#region Public Methods

		public void CreateDevice()
		{
			PresentationParameters presentationParameters = new PresentationParameters();
			presentationParameters.DepthStencilFormat = DepthFormat.Depth24;
			presentationParameters.IsFullScreen = false;

			if (PreparingDeviceSettings != null)
			{
				// Generate default information to provide to the application.
				GraphicsDeviceInformation gdi = new GraphicsDeviceInformation();
				gdi.GraphicsProfile = GraphicsProfile;
				gdi.Adapter = GraphicsAdapter.DefaultAdapter;
				gdi.PresentationParameters = presentationParameters;

				// Prepare the settings, pass to the application, apply the changes.
				PreparingDeviceSettingsEventArgs settings = new PreparingDeviceSettingsEventArgs(gdi);
				PreparingDeviceSettings(this, settings);
				presentationParameters = settings.GraphicsDeviceInformation.PresentationParameters;

				// Set the GraphicsProfile based on the new settings.
				GraphicsProfile = settings.GraphicsDeviceInformation.GraphicsProfile;

				// Change our settings based on the new PresentationParameters.
				PreferredBackBufferFormat = presentationParameters.BackBufferFormat;
				PreferredDepthStencilFormat = presentationParameters.DepthStencilFormat;
			}

			// Create the GraphicsDevice, apply the initial settings.
			graphicsDevice = new GraphicsDevice(
				GraphicsAdapter.DefaultAdapter,
				GraphicsProfile,
				presentationParameters
			);
			ApplyChanges();

			/* Set the new display orientation on the touch panel.
			 *
			 * TODO: In XNA this seems to be done as part of the
			 * GraphicsDevice.DeviceReset event... we need to get
			 * those working.
			 */
			TouchPanel.DisplayOrientation =
				graphicsDevice.PresentationParameters.DisplayOrientation;

			// Call the DeviceCreated Event
			OnDeviceCreated(EventArgs.Empty);
		}

		public bool BeginDraw()
		{
			if (graphicsDevice == null)
			{
				return false;
			}

			drawBegun = true;
			return true;
		}

		public void EndDraw()
		{
			if (graphicsDevice != null && drawBegun)
			{
				drawBegun = false;
				graphicsDevice.Present();
			}
		}

		public void ApplyChanges()
		{
			// Calling ApplyChanges() before CreateDevice() should have no effect.
			if (graphicsDevice == null)
			{
				return;
			}
			
			BeginDeviceReset();

			// Apply the GraphicsDevice changes internally.
			GraphicsDevice.PresentationParameters.BackBufferFormat =
				PreferredBackBufferFormat;
			GraphicsDevice.PresentationParameters.BackBufferWidth =
				PreferredBackBufferWidth;
			GraphicsDevice.PresentationParameters.BackBufferHeight =
				PreferredBackBufferHeight;
			GraphicsDevice.PresentationParameters.DepthStencilFormat =
				PreferredDepthStencilFormat;
			GraphicsDevice.PresentationParameters.IsFullScreen =
				IsFullScreen;

			// Make the Platform device changes.
			game.Platform.BeginScreenDeviceChange(
				GraphicsDevice.PresentationParameters.IsFullScreen
			);
			game.Platform.EndScreenDeviceChange(
				"FNA",
				GraphicsDevice.PresentationParameters.BackBufferWidth,
				GraphicsDevice.PresentationParameters.BackBufferHeight
			);

			// Apply the PresentInterval.
			game.Platform.SetPresentationInterval(
				SynchronizeWithVerticalRetrace ?
					GraphicsDevice.PresentationParameters.PresentationInterval :
					PresentInterval.Immediate
			);

			EndDeviceReset();
		}

		public void ToggleFullScreen()
		{
			// Change settings.
			IsFullScreen = !IsFullScreen;
			graphicsDevice.PresentationParameters.IsFullScreen = IsFullScreen;

			// Apply settings.
			game.Platform.BeginScreenDeviceChange(IsFullScreen);
			game.Platform.EndScreenDeviceChange(
				"FNA",
				Graphics.OpenGLDevice.Instance.Backbuffer.Width,
				Graphics.OpenGLDevice.Instance.Backbuffer.Height
			);
		}

		#endregion

		#region Internal Methods

		internal void INTERNAL_ResizeGraphicsDevice(int width, int height)
		{
			PresentationParameters pp = GraphicsDevice.PresentationParameters;

			// Only reset if there's an actual change in size
			if (pp.BackBufferWidth != width || pp.BackBufferHeight != height)
			{
				BeginDeviceReset();

				pp.BackBufferWidth = width;
				pp.BackBufferHeight = height;

				EndDeviceReset();
			}
		}

		#endregion

		#region Private Methods

		private void BeginDeviceReset()
		{
			// Notify DeviceResetting EventHandlers.
			OnDeviceResetting(null);
			GraphicsDevice.OnDeviceResetting();
		}

		private void EndDeviceReset()
		{
			// Now, update the viewport
			GraphicsDevice.Viewport = new Viewport(
				0,
				0,
				GraphicsDevice.PresentationParameters.BackBufferWidth,
				GraphicsDevice.PresentationParameters.BackBufferHeight
			);

			// Update the scissor rectangle to our new default target size
			GraphicsDevice.ScissorRectangle = new Rectangle(
				0,
				0,
				GraphicsDevice.PresentationParameters.BackBufferWidth,
				GraphicsDevice.PresentationParameters.BackBufferHeight
			);

			OpenGLDevice.Instance.Backbuffer.ResetFramebuffer(
				GraphicsDevice.PresentationParameters.BackBufferWidth,
				GraphicsDevice.PresentationParameters.BackBufferHeight,
				GraphicsDevice.PresentationParameters.DepthStencilFormat
			);

			// Notify DeviceReset EventHandlers.
			OnDeviceReset(null);
			GraphicsDevice.OnDeviceReset();

			/* Set the new display size on the touch panel.
			 * 
			 * TODO: In XNA this seems to be done as part of the
			 * GraphicsDevice.DeviceReset event... we need to get
			 * those working.
			 */
			TouchPanel.DisplayWidth =
				graphicsDevice.PresentationParameters.BackBufferWidth;
			TouchPanel.DisplayHeight =
				graphicsDevice.PresentationParameters.BackBufferHeight;
		}

		#endregion

		#region Internal IGraphicsDeviceService Methods

		/* FIXME: Why does the GraphicsDeviceManager not know enough about the
		 * GraphicsDevice to raise these events without help?
		 */
		internal void OnDeviceDisposing(EventArgs e)
		{
			Raise(DeviceDisposing, e);
		}

		/* FIXME: Why does the GraphicsDeviceManager not know enough about the
		 * GraphicsDevice to raise these events without help?
		 */
		internal void OnDeviceResetting(EventArgs e)
		{
			Raise(DeviceResetting, e);
		}

		/* FIXME: Why does the GraphicsDeviceManager not know enough about the
		 * GraphicsDevice to raise these events without help?
		 */
		internal void OnDeviceReset(EventArgs e)
		{
			Raise(DeviceReset, e);
		}

		/* FIXME: Why does the GraphicsDeviceManager not know enough about the
		 * GraphicsDevice to raise these events without help?
		 */
		internal void OnDeviceCreated(EventArgs e)
		{
			Raise(DeviceCreated, e);
		}

		#endregion

		#region Private IGraphicsDeviceService Methods

		private void Raise<TEventArgs>(EventHandler<TEventArgs> handler, TEventArgs e)
			where TEventArgs : EventArgs
		{
			if (handler != null)
			{
				handler(this, e);
			}
		}

		#endregion
	}
}
