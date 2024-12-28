// Ignore Spelling: App

using Microsoft.UI.Xaml;
using System;

namespace Test_Client
{
    /// <summary>
    /// Provides application-specific behavior to supplement the default Application class.
    /// </summary>
    public partial class App : Application, IDisposable
    {
        #region Members
        /// <summary>
        /// Main window for the application
        /// </summary>
        private MainWindow? m_window;
        /// <summary>
        /// Dispose flag
        /// </summary>
        private bool disposedValue;
        #endregion

        #region Constructor
        /// <summary>
        /// Initializes the singleton application object.  This is the first line of authored code
        /// executed, and as such is the logical equivalent of main() or WinMain().
        /// </summary>
        public App()
        {
            InitializeComponent();
        }
        #endregion

        #region Application Overrides
        /// <summary>
        /// Invoked when the application is launched.
        /// </summary>
        /// <param name="args">Details about the launch request and process.</param>
        protected override void OnLaunched(LaunchActivatedEventArgs args)
        {
            m_window = new MainWindow();
            m_window.Activate();
        }
        #endregion

        #region IDisposable Support
        /// <summary>
        /// Dispose of the object
        /// </summary>
        /// <param name="disposing">Dispose of managed objects</param>
        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    m_window?.Dispose();
                }

                disposedValue = true;
            }
        }

        /// <summary>
        /// Dispose of the object
        /// </summary>
        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
        #endregion
    }
}
