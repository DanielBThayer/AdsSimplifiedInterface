namespace Test_Console
{
    /// <summary>
    /// Builds a command for clearing the screen
    /// </summary>
    /// <param name="description">Description for the command</param>
    internal class CommandClearScreen(string description) : ICommand
    {

        #region ICommand
        /// <inheritdoc/>
        public string Description { get; set; } = description;

        /// <inheritdoc/>
        public bool Execute(string Command, string[] Arguments)
        {
            Console.Clear();
            return true;
        }

        /// <inheritdoc/>
        public void Help(string[] Arguments)
        {
            Console.WriteLine(Description);
        }
        #endregion
    }
}
