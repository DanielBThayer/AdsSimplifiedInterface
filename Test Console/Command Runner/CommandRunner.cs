namespace Test_Console
{
    /// <summary>
    /// Runs the commands from the user
    /// </summary>
    internal class CommandRunner
    {
        #region Members
        /// <summary>
        /// Quit/Exit command have not been given
        /// </summary>
        public bool ContinueExecuting { get; set; }
        /// <summary>
        /// Dictionary of available commands
        /// </summary>
        private readonly Dictionary<string, ICommand> Commands;
        #endregion

        #region Constructor
        /// <summary>
        /// Default constructor
        /// </summary>
        public CommandRunner()
        {
            ContinueExecuting = true;
            Commands = new Dictionary<string, ICommand>(StringComparer.OrdinalIgnoreCase);
        }
        #endregion

        #region Command Registration
        /// <summary>
        /// Adds a command to the available commands
        /// </summary>
        /// <param name="command">Command to add</param>
        /// <param name="action">Action to happen</param>
        public void RegisterCommand(string command, ICommand action)
        {
            Commands.Add(command, action);
        }
        #endregion

        #region Command Parser
        /// <summary>
        /// Executes the user input
        /// </summary>
        /// <param name="command">Raw user input</param>
        public void ExecuteCommand(string command)
        {
            string[] commands = command.SplitArgs()
                                       .ToArray();

            if (Commands.TryGetValue(commands[0], out ICommand? value))
            {
                int nextCommandOffset = (command.IndexOf(commands[0]) + commands[0].Length + 1);
                ContinueExecuting = value.Execute(nextCommandOffset >= command.Length ? string.Empty : command[nextCommandOffset..], commands.Length > 1 ? commands[1..] : []);
                return;
            }
            else if (commands[0].Equals("help", StringComparison.OrdinalIgnoreCase))
            {
                if (commands.Length == 1)
                {
                    Help();
                    return;
                }
                else if (Commands.TryGetValue(commands[1], out ICommand? help))
                {
                    help.Help(commands[1..]);
                    return;
                }
            }

            // Bad command, display the help too
            Console.WriteLine($"Unknown command '{command}'");
            Help();
        }
        #endregion

        #region Help
        /// <summary>
        /// Prints the generic help information
        /// </summary>
        private void Help()
        {
            Console.WriteLine("Help:");
            Console.WriteLine("Syntax:");
            Console.WriteLine("[command] [command arguments]");
            Console.WriteLine("Commands:");

            foreach (var command in Commands)
            {
                Console.WriteLine($"{command.Key} - {command.Value.Description}");
            }
        }
        #endregion
    }
}