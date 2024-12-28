namespace Test_Console
{
    /// <summary>
    /// Notification commands for the PLC
    /// </summary>
    /// <param name="description">Description for the command</param>
    /// <param name="commands">Subcommands for the notification command</param>
    internal class CommandNotification(string description, Dictionary<string, ICommand> commands) : ICommand
    {
        #region Members
        /// <summary>
        /// Sub commands for the notification command
        /// </summary>
        private readonly Dictionary<string, ICommand> subCommands = commands;
        #endregion

        #region ICommand
        /// <inheritdoc/>
        public string Description { get; set; } = description;

        /// <inheritdoc/>
        public bool Execute(string Command, string[] Arguments)
        {
            if (subCommands.TryGetValue(Arguments[0], out ICommand? value))
            {
                int nextCommandOffset = (Command.IndexOf(Arguments[0]) + Arguments[0].Length + 1);
                return value.Execute(nextCommandOffset >= Command.Length ? string.Empty : Command[nextCommandOffset..], Arguments.Length > 1 ? Arguments[1..] : []);
            }

            // Bad command, display the help too
            Console.WriteLine($"Unknown command '{Arguments[0]}'");
            Help([]);
            return true;
        }

        /// <inheritdoc/>
        public void Help(string[] Arguments)
        {
            if (Arguments.Length == 0)
            {
                Console.WriteLine("Help:");
                Console.WriteLine("Syntax:");
                Console.WriteLine("notification [command] [command arguments]");
                Console.WriteLine("Commands:");

                foreach (var command in subCommands)
                {
                    Console.WriteLine($"{command.Key} - {command.Value.Description}");
                }
                return;
            }
            else if (subCommands.TryGetValue(Arguments[0], out ICommand? value))
            {
                value.Help(Arguments[1..]);
                return;
            }

            // Bad command
            Console.WriteLine($"Unknown command '{Arguments[0]}'");
            Help([]);
        }
        #endregion
    }
}
