using Bearcat.Cli;
using Bearcat.Cli.Commands;
using Spectre.Console.Cli;

var app = new CommandApp();
app.Configure(config =>
{
    config.SetApplicationName("Bearcat.Cli");
    config
        .AddCommand<SetupCommand>("setup")
        .WithDescription("Configure and register the Windows service");
    config
        .AddCommand<SetDbPasswordCommand>("set-db-password")
        .WithDescription("Change the database password and restart the service");
    config
        .AddCommand<UninstallCommand>("uninstall")
        .WithDescription("Stop and remove the Windows service");
});

return await app.RunAsync(args);
