using AutoDbBackup;

IHost host = Host.CreateDefaultBuilder(args)
    
    .ConfigureServices((context,services) =>
    {
        services.Configure<ServerConfig>(context.Configuration.GetSection("ServerConfig"));
        services.AddHostedService<Worker>();
    })
    .Build();

host.Run();