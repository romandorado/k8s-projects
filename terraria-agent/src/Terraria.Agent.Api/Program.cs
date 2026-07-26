using System.Reflection;
using System.Text;
using Microsoft.OpenApi.Models;
using Terraria.Agent.Api.Services;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllers();
builder.Services.AddSingleton<ChatHistory>();
builder.Services.AddSingleton<CommandParser>();
builder.Services.AddHttpClient<WikiService>();
builder.Services.AddSingleton<CraftingService>();
builder.Services.AddSingleton<KnowledgeService>();
builder.Services.AddHttpClient<TShockClient>();
builder.Services.AddHttpClient<GroqService>();
builder.Services.AddHttpClient<IntentParser>();
builder.Services.AddHostedService<AutoEventService>();

// Swagger with JWT auth support
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Terraria Agent API",
        Version = "v1",
        Description = "AI-powered narrator and controller for Terraria server. " +
                      "Send chat messages, execute commands, and narrate game events."
    });
    c.AddServer(new OpenApiServer { Url = "/terraria-agent" });

    // JWT/Token auth support
    c.AddSecurityDefinition("AgentToken", new OpenApiSecurityScheme
    {
        Name = "X-Agent-Token",
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.ApiKey,
        Scheme = "apiKey",
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Description = "Agent authentication token"
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "AgentToken"
                }
            },
            Array.Empty<string>()
        }
    });

    // Include XML comments if available
    var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath))
        c.IncludeXmlComments(xmlPath);
});

var app = builder.Build();

// Enable Swagger in development and production
app.UseSwagger(c =>
{
    c.RouteTemplate = "swagger/{documentName}/swagger.json";
});
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("v1/swagger.json", "Terraria Agent API v1");
    c.RoutePrefix = "swagger";
});

app.MapGet("/health", () => Results.Ok());
app.MapControllers();

app.Run();
