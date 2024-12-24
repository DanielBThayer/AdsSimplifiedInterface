namespace Test_Console
{
    /// <summary>
    /// Builds a quit command
    /// </summary>
    /// <param name="description">Description for the command</param>
    internal class CommandQuit(string description) : ICommand
    {
        #region ICommand
        /// <inheritdoc/>
        public string Description { get; set; } = description;

        /// <inheritdoc/>
        public bool Execute(string Command, string[] Arguments)
        {
            return false;
        }

        /// <inheritdoc/>
        public void Help(string[] Arguments)
        {
            Console.WriteLine(Description);
        }
        #endregion
    }
}
