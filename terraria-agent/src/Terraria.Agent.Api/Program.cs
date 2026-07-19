using Terraria.Agent.Api.Services;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllers();
builder.Services.AddSingleton<CommandParser>();
builder.Services.AddHttpClient<TShockClient>();
builder.Services.AddHttpClient<GroqService>();
builder.Services.AddHttpClient<IntentParser>();

var app = builder.Build();

app.MapGet("/health", () => Results.Ok());
app.MapControllers();

app.Run();
