using AdsSimplifiedInterface;
using Newtonsoft.Json;
using System.Reflection;

namespace Test_Console
{
    /// <summary>
    /// Builds a command for writing to the PLC
    /// </summary>
    /// <param name="description">Description for the command</param>
    /// <param name="plc">Interface to the PLC</param>
    internal class CommandWrite(string description, AdsInterface plc) : ICommand
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
            if (Arguments.Length < 2)
            {
                Console.WriteLine("Variable path and value required!");
                Console.WriteLine("Syntax:");
                Console.WriteLine("write [variable path] [value]");
                return true;
            }

            // Pull the variable and value from the command
            string variable = Arguments[0];
            string value = Command[(Command.IndexOf(variable) + variable.Length + 1)..];

            // Get the data type of the variable for inspection
            Type? type;
            try
            {
                type = Plc.GetDataType(variable);
                if (type == null)
                {
                    Console.WriteLine($"Variable ({variable}) does not exist or can't have its type extracted from the PLC");
                    Console.WriteLine("Syntax:");
                    Console.WriteLine("write [variable path] [value]");
                    return true;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Unable to get data type of variable ({variable}): {ex.Message}");
                Console.WriteLine("Syntax:");
                Console.WriteLine("write [variable path] [value]");
                return true;
            }

            // Check if the value is a primitive or structured type
            if (type.IsEnum)
            {
                try
                {
                    if (long.TryParse(value, out long numValue))
                    {
                        Plc.SetValue(variable, numValue);
                    }
                    else
                    {
                        var sendValue = Enum.Parse(type, value);
                        Plc.SetValue(variable, sendValue);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Unable to write to {variable}: {ex.Message}");
                    return true;
                }
            }
            else if (type.IsPrimitive)
            {
                try
                {
                    Enum.TryParse(type.Name, true, out TypeCode code);
                    var sendValue = Convert.ChangeType(value, code);
                    Plc.SetValue(variable, sendValue);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Unable to write to {variable}: {ex.Message}");
                    return true;
                }
            }
            else if (type == typeof(string))
            {
                try
                {
                    Plc.SetValue(variable, value);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Unable to write to {variable}: {ex.Message}");
                    return true;
                }
            }
            else
            {
                // Must deserialize from JSON
                try
                {
                    // Get method for deserialization
                    MethodInfo? method = typeof(JsonConvert).GetMethod("DeserializeObject", 1, [typeof(string)]);
                    if (method == null)
                    {
                        Console.WriteLine($"Unable to get method for deserialization");
                        return true;
                    }
                    method = method.MakeGenericMethod(type);

                    // Deserialize the value and send
                    var sendValue = method.Invoke(null, [value]);
                    if (sendValue == null)
                    {
                        Console.WriteLine($"Unable to deserialize data: {value}");
                        return true;
                    }
                    Plc.SetValue(variable, sendValue);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Unable to write to {variable}: {ex.Message}");
                    return true;
                }
            }

            return true;
        }

        /// <inheritdoc/>
        public void Help(string[] Arguments)
        {
            Console.WriteLine("Help:");
            Console.WriteLine("Syntax:");
            Console.WriteLine("write [variable path] [value]");
        }
        #endregion
    }
}
