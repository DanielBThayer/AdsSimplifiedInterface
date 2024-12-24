namespace Test_Console
{
    /// <summary>
    /// Interface for all commands
    /// </summary>
    public interface ICommand
    {
        /// <summary>
        /// Execute the command
        /// </summary>
        /// <param name="Command">User input after command word</param>
        /// <param name="Arguments">All arguments for the command</param>
        /// <returns>Execution may continue</returns>
        bool Execute(string Command, string[] Arguments);

        /// <summary>
        /// Description of the command
        /// </summary>
        string Description { get; set; }

        /// <summary>
        /// Prints the help information to the console
        /// </summary>
        void Help(string[] Arguments);
    }
}
