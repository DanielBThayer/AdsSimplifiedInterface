using AdsSimplifiedInterface;
using Newtonsoft.Json;

namespace Test_Console
{
    /// <summary>
    /// Builds a command for reading to the PLC
    /// </summary>
    /// <param name="description">Description for the command</param>
    /// <param name="plc">Interface to the PLC</param>
    internal class CommandRead(string description, AdsInterface plc) : ICommand
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
                var result = Plc.GetValue(Arguments[0]);

                if (result == null)
                {
                    Console.WriteLine($"{Arguments[0]}: No value found!");
                }
                else if (result.GetType().IsEnum)
                {
                    Console.WriteLine($"{Arguments[0]}: {Enum.GetName(result.GetType(), result)}");
                }
                else if (result.GetType().IsPrimitive)
                {
                    Console.WriteLine($"{Arguments[0]}: {result}");
                }
                else
                {
                    string strResult = JsonConvert.SerializeObject(result, Formatting.Indented);
                    Console.WriteLine($"{Arguments[0]}: {strResult}");
                }
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
            Console.WriteLine("read [variable path]");
        }
        #endregion
    }
}
