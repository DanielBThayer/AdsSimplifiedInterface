using AdsSimplifiedInterface;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Test_Console;

// Build configuration and logging interfaces
IConfiguration config = new ConfigurationBuilder().AddJsonFile($"appsettings.json", true, true).Build();
ILoggerFactory loggerFactory = LoggerFactory.Create(configure =>
{
    configure.AddConsole();
    configure.SetMinimumLevel(LogLevel.Information);
});

// Create the ADS connection
using AdsInterface adsInterface = new(config, loggerFactory.CreateLogger<AdsInterface>());

// Build the command runner
CommandRunner runner = new();
runner.RegisterCommand("cls", new CommandClearScreen("Clears the screen"));
runner.RegisterCommand("clear", new CommandClearScreen("Clears the screen"));
runner.RegisterCommand("quit", new CommandQuit("Quits the application"));
runner.RegisterCommand("exit", new CommandQuit("Quits the application"));
runner.RegisterCommand("read", new CommandRead("Reads a value from the PLC", adsInterface));
runner.RegisterCommand("write", new CommandWrite("Write a value to the PLC", adsInterface));
runner.RegisterCommand("notification", new CommandNotification("Notifications from the PLC", new Dictionary<string, ICommand>()
{
    { "add", new CommandNotificationAdd("Add a notification to the PLC", adsInterface) },
    { "remove", new CommandNotificationRemove("Remove a notification to the PLC", adsInterface) }
}));

// Loop until commanded to exit
do
{
    // Prompt for input
    Console.Write("> ");
    string command = Console.ReadLine() ?? string.Empty;

    // Parse if not an empty string
    if (!string.IsNullOrEmpty(command))
    {
        runner.ExecuteCommand(command);
    }
} while (runner.ContinueExecuting);
