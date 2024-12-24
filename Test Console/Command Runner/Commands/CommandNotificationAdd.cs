using AdsSimplifiedInterface;
using Newtonsoft.Json;
using System.Reflection;

namespace Test_Console
{
    /// <summary>
    /// Builds a command for adding a notification to the PLC
    /// </summary>
    /// <param name="description">Description for the command</param>
    /// <param name="plc">Interface to the PLC</param>
    internal class CommandNotificationAdd(string description, AdsInterface plc) : ICommand
    {
        #region Members
        /// <summary>
        /// PLC Interface
        /// </summary>
        private readonly AdsInterface Plc = plc;
        #endregion

        #region ICommand
        /// <inheritdoc/>
        public string Description { get; set; } = description;

        /// <inheritdoc/>
        public bool Execute(string Command, string[] Arguments)
        {
            try
            {
                string variable;
                int updateRate;

                // Check the arguments
                switch (Arguments.Length)
                {
                    case 0:
                        Console.WriteLine("Need to at least specify the variable name");
                        Help([]);
                        return true;
                    case 1:
                        Console.WriteLine("Update Rate missing, using 100 ms");
                        variable = Arguments[0];
                        updateRate = 100;
                        break;
                    default:
                        variable = Arguments[0];

                        if (!int.TryParse(Arguments[1], out updateRate))
                        {
                            Console.WriteLine("Update Rate not valid, using 100 ms");
                            updateRate = 100;
                        }
                        break;
                }

                // Get the data type of the variable
                Type type = Plc.GetDataType(variable);

                // Get the notification method, via reflection
                MethodInfo? method = GetType().GetMethods(BindingFlags.NonPublic | BindingFlags.Instance)
                                              .FirstOrDefault(m => m.Name.Equals("AddNotification", StringComparison.OrdinalIgnoreCase));
                if (method == null)
                {
                    Console.WriteLine("Unable to find method for adding a notification");
                    return true;
                }
                method = method.MakeGenericMethod(type);

                // Invoke the method to create the notification
                method.Invoke(this, [variable, updateRate]);

                Console.WriteLine($"Notification added for {variable}, scanning at {updateRate} ms");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"{ex.Message}");
            }
            return true;
        }

        /// <inheritdoc/>
        public void Help(string[] Arguments)
        {
            Console.WriteLine("Help:");
            Console.WriteLine("Syntax:");
            Console.WriteLine("notification add [variable path] <[Update Rate] = 100>");
        }
        #endregion

        #region Notification Event Handler
        /// <summary>
        /// Generic add notification to help with binding in the runtime
        /// </summary>
        /// <typeparam name="T">Data type of the variable</typeparam>
        /// <param name="variable">Instance path of the variable</param>
        /// <param name="updateRate">Update rate</param>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("CodeQuality", "IDE0051:Remove unused private members", Justification = "Called by reflection only")]
        private void AddNotification<T>(string variable, int updateRate)
        {
            Plc.AddNotification<T>(variable, Notification, updateRate);
        }

        /// <summary>
        /// Handler for the notification event
        /// </summary>
        /// <typeparam name="T">Type of the PLC variable</typeparam>
        /// <param name="instancePath">Path to the PLC variable</param>
        /// <param name="oldValue">Old value of the variable</param>
        /// <param name="newValue">New value of the variable</param>
        private static void Notification<T>(string instancePath, T oldValue, T newValue)
        {
            try
            {
                if (newValue == null)
                {
                    Console.WriteLine($"{instancePath}: No value found!");
                }
                else if (newValue.GetType().IsEnum)
                {
                    Console.WriteLine($"{instancePath}: {Enum.GetName(newValue.GetType(), newValue)}");
                }
                else if (newValue.GetType().IsPrimitive)
                {
                    Console.WriteLine($"{instancePath}: {newValue}");
                }
                else
                {
                    string strResult = JsonConvert.SerializeObject(newValue, Formatting.Indented);
                    Console.WriteLine($"{instancePath}: {strResult}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"{instancePath}: {ex.Message}");
            }
        }
        #endregion
    }
}
