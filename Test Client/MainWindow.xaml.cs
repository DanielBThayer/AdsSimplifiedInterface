using AdsSimplifiedInterface;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.TextBox;

namespace Test_Client
{
    /// <summary>
    /// An empty window that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainWindow : Window, IDisposable
    {
        #region Non-Visible Members
        /// <summary>
        /// PLC Connection
        /// </summary>
        private readonly AdsInterface adsInterface;
        /// <summary>
        /// Dispose flag
        /// </summary>
        private bool disposedValue;
        #endregion

        #region Constructor
        /// <summary>
        /// Main constructor for the window
        /// </summary>
        public MainWindow()
        {
            InitializeComponent();

            // Build configuration and logging interfaces
            IConfiguration config = new ConfigurationBuilder().AddJsonFile($"appsettings.json", true, true).Build();
            ILoggerFactory loggerFactory = LoggerFactory.Create(configure =>
            {
                configure.AddConsole();
                configure.SetMinimumLevel(LogLevel.Information);
            });

            // Create the ADS connection
            adsInterface = new(config, loggerFactory.CreateLogger<AdsInterface>());

            // Get a list of the PLC variables
            List<string> variables = adsInterface.GetVariables();

            // Add the variables to the list view
            foreach (string variable in variables)
            {
                CreateNode(variable);
            }

            tvVariables.UpdateLayout();
        }
        #endregion

        #region Window Events
        /// <summary>
        /// Event for when a variable is tapped
        /// </summary>
        /// <param name="sender">Object selecting the variable</param>
        /// <param name="args">Parameters of the selection</param>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0060:Remove unused parameter", Justification = "Required by Event API")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("CodeQuality", "IDE0051:Remove unused private members", Justification = "Bug in VS doesn't see usage in MainWindow.xaml")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("CodeQuality", "IDE0079:Remove unnecessary suppression", Justification = "Bug in VS doesn't see the need for suppression when the item needing it is suppressed")]
        private void TvVariables_Tapped(object sender, Microsoft.UI.Xaml.Input.TappedRoutedEventArgs e)
        {
            if (tvVariables.SelectedItem != null)
            {
                try
                {
                    string path = BuildPath((TreeViewNode)tvVariables.SelectedItem);
                    PlcVariableTypeInfo? variable = adsInterface.GetVariableInfos(path).FirstOrDefault();

                    if (variable != null)
                    {
                        tbPath.Text = path;
                        tbType.Text = variable.DataType;
                        tbSize.Text = variable.Size.ToString();

                        object? value = adsInterface.GetValue(path);
                        if (value == null)
                        {
                            tbValue.Text = "";
                        }
                        else if (variable.IsEnum)
                        {
                            tbValue.Text = variable.Children.FirstOrDefault(ev => ev.EnumValue.Equals(value))?.Name ?? value.ToString();
                        }
                        else if (value.GetType().IsPrimitive)
                        {
                            tbValue.Text = value.ToString();
                        }
                        else
                        {
                            tbValue.Text = JsonConvert.SerializeObject(value, Formatting.Indented);
                        }
                    }
                }
                catch
                {
                    tbPath.Text = string.Empty;
                    tbType.Text = string.Empty;
                    tbSize.Text = string.Empty;
                    tbValue.Text = string.Empty;
                }
            }
        }
        #endregion

        #region Helper Functions
        /// <summary>
        /// Create a node in the tree view
        /// </summary>
        /// <param name="name">Name of the node</param>
        private void CreateNode(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return;
            }
            else if (name.Contains('.') && !name[..name.IndexOf('.')].Contains('['))
            {
                string parent = name[..name.IndexOf('.')];
                string child = name[(name.IndexOf('.') + 1)..];

                TreeViewNode? parentNode = tvVariables.RootNodes.FirstOrDefault(node => ((string)node.Content).Equals(parent, StringComparison.OrdinalIgnoreCase));
                if (parentNode == null)
                {
                    parentNode = new TreeViewNode() { Content = parent };
                    tvVariables.RootNodes.Add(parentNode);
                }

                CreateNode(child, parentNode);
            }
            else if (name.StartsWith('['))
            {
                string self = name[..(name.IndexOf(']') + 1)];
                string child = (name.IndexOf(']') + 2) <= name.Length ? name[(name.IndexOf(']') + 2)..] : string.Empty;

                TreeViewNode? parentNode = tvVariables.RootNodes.FirstOrDefault(node => ((string)node.Content).Equals(self, StringComparison.OrdinalIgnoreCase));
                if (parentNode == null)
                {
                    parentNode = new TreeViewNode() { Content = self };
                    tvVariables.RootNodes.Add(parentNode);
                }

                CreateNode(child, parentNode);
            }
            else if (name.Contains('['))
            {
                string parent = name[..name.IndexOf('[')];
                string child = name[name.IndexOf('[')..];

                TreeViewNode? parentNode = tvVariables.RootNodes.FirstOrDefault(node => ((string)node.Content).Equals(parent, StringComparison.OrdinalIgnoreCase));
                if (parentNode == null)
                {
                    parentNode = new TreeViewNode() { Content = parent };
                    tvVariables.RootNodes.Add(parentNode);
                }

                CreateNode(child, parentNode);
            }
            else
            {
                TreeViewNode? self = tvVariables.RootNodes.FirstOrDefault(node => ((string)node.Content).Equals(name, StringComparison.OrdinalIgnoreCase));
                if (self == null)
                {
                    self = new TreeViewNode() { Content = name };
                    tvVariables.RootNodes.Add(self);
                }
            }
        }

        /// <summary>
        /// Create a node in the tree view
        /// </summary>
        /// <param name="name">Name of the node</param>
        /// <param name="parent">Parent node</param>
        private void CreateNode(string name, TreeViewNode parent)
        {
            if (string.IsNullOrEmpty(name))
            {
                return;
            }
            else if (name.Contains('.') && !name[..name.IndexOf('.')].Contains('['))
            {
                string self = name[..name.IndexOf('.')];
                string child = name[(name.IndexOf('.') + 1)..];

                TreeViewNode? parentNode = parent.Children.FirstOrDefault(node => ((string)node.Content).Equals(self, StringComparison.OrdinalIgnoreCase));
                if (parentNode == null)
                {
                    parentNode = new TreeViewNode() { Content = self };
                    parent.Children.Add(parentNode);
                }

                CreateNode(child, parentNode);
            }
            else if (name.StartsWith('['))
            {
                string self = name[..(name.IndexOf(']') + 1)];
                string child = (name.IndexOf(']') + 2) <= name.Length ? name[(name.IndexOf(']') + 2)..] : string.Empty;

                TreeViewNode? parentNode = parent.Children.FirstOrDefault(node => ((string)node.Content).Equals(self, StringComparison.OrdinalIgnoreCase));
                if (parentNode == null)
                {
                    parentNode = new TreeViewNode() { Content = self };
                    parent.Children.Add(parentNode);
                }

                CreateNode(child, parentNode);
            }
            else if (name.Contains('['))
            {
                string self = name[..name.IndexOf('[')];
                string child = name[name.IndexOf('[')..];

                TreeViewNode? parentNode = parent.Children.FirstOrDefault(node => ((string)node.Content).Equals(self, StringComparison.OrdinalIgnoreCase));
                if (parentNode == null)
                {
                    parentNode = new TreeViewNode() { Content = self };
                    parent.Children.Add(parentNode);
                }

                CreateNode(child, parentNode);
            }
            else
            {
                TreeViewNode? self = tvVariables.RootNodes.FirstOrDefault(node => ((string)node.Content).Equals(name, StringComparison.OrdinalIgnoreCase));
                if (self == null)
                {
                    self = new TreeViewNode() { Content = name };
                    parent.Children.Add(self);
                }
            }
        }

        /// <summary>
        /// Build the path to the variable
        /// </summary>
        /// <param name="node">Node for the variable</param>
        /// <returns>Path to the variable</returns>
        private static string BuildPath(TreeViewNode node)
        {
            string path = (string)node.Content;
            while (node.Parent != null)
            {
                node = node.Parent;

                if (node.Content == null)
                {
                    continue;
                }

                if (path.StartsWith('['))
                {
                    path = (string)node.Content + path;
                }
                else
                {
                    path = (string)node.Content + "." + path;
                }
            }
            return path;
        }
        #endregion

        #region IDisposable Support
        /// <summary>
        /// Dispose of the object
        /// </summary>
        /// <param name="disposing">Dispose managed objects</param>
        private void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    adsInterface.Dispose();
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
