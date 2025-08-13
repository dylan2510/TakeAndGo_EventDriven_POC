using Tag.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

var cs = builder.Configuration.GetConnectionString("MainDb") 
    ?? "Host=localhost;Username=tag;Password=tagpw;Database=tagdb";
builder.Services.AddTagDb(cs); // from infrastructure, to inject DBContext
builder.Services.AddControllers();
var app = builder.Build();

app.MapControllers();

// quick DB ping for sanity
app.MapGet("/db-check", (TagDbContext db) => $"DB OK: {db.Database.ProviderName}");

app.Run();
