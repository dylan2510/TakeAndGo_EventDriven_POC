using Microsoft.Extensions.Logging;
using Tag.Infrastructure;
using Tag.DisplayRelay;
using Microsoft.Extensions.FileProviders;
using Microsoft.AspNetCore.StaticFiles;

var builder = WebApplication.CreateBuilder(args);

// DB connection (use GetConnectionString if present; else fallback)
var cs = builder.Configuration.GetConnectionString("MainDb")
         ?? builder.Configuration["ConnectionStrings:MainDb"]
         ?? "Host=localhost;Username=tag;Password=tagpw;Database=tagdb";

// Services BEFORE Build()
builder.Services.AddTagDb(cs);
builder.Services.AddControllers();
builder.Services.AddSingleton<GroupHub>();
builder.Services.AddSingleton(new RabbitOptions {
    Uri = builder.Configuration["Rabbit:Uri"] ?? "amqp://guest:guest@localhost:5672"
});
builder.Services.AddHostedService<DisplayBusConsumer>();
builder.Services.AddLogging(b => b.AddConsole());

var app = builder.Build();

app.UseWebSockets();     // required for WS from a controller

// serve Tag.TvApp (index.html + tv.js) at the root of Display Relay
var tvPath = Path.Combine(builder.Environment.ContentRootPath, "../Tag.TvApp");
if (Directory.Exists(tvPath))
{
    var provider = new PhysicalFileProvider(tvPath);
    app.UseDefaultFiles(new DefaultFilesOptions { FileProvider = provider }); // serves / -> index.html
    app.UseStaticFiles(new StaticFileOptions {
        FileProvider = provider,
        ServeUnknownFileTypes = true,
        ContentTypeProvider = new FileExtensionContentTypeProvider()
    });
}

app.MapControllers();    // maps /ws and /display/state

app.Run();

