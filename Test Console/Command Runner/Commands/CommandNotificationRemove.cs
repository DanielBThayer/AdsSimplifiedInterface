using AdsSimplifiedInterface;

namespace Test_Console
{
    /// <summary>
    /// Builds a command for removing a notification to the PLC
    /// </summary>
    /// <param name="description">Description for the command</param>
    /// <param name="plc">Interface to the PLC</param>
    internal class CommandNotificationRemove(string description, AdsInterface plc) : ICommand
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

                // Check the arguments
                switch (Arguments.Length)
                {
                    case 0:
                        Console.WriteLine("Need to specify the variable name");
                        Help([]);
                        return true;
                    default:
                        variable = Arguments[0];
                        break;
                }

                // Remove the notification
                Plc.RemoveAllNotifications(variable);

                Console.WriteLine($"Notification removed for {variable}");
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
            Console.WriteLine("notification remove [variable path]");
        }
        #endregion
    }
}
