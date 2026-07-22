using MantisBTMailTask;
using Microsoft.Extensions.Hosting;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddHostedService<Worker>();

if (OperatingSystem.IsWindows())
{
    builder.Services.AddWindowsService();
}
else if (OperatingSystem.IsLinux())
{
    builder.Services.AddSystemd();
}

var host = builder.Build();
host.Run();
