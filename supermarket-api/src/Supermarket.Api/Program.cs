using Microsoft.EntityFrameworkCore;
using Supermarket.Api.Data;
using Supermarket.Api.Models;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "Supermarket API",
        Version = "v1",
        Description = "API REST para gestión de listas de compra del supermercado",
        Contact = new Microsoft.OpenApi.Models.OpenApiContact
        {
            Name = "Roman",
            Email = "roman@local"
        }
    });
});

// Configure PostgreSQL
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// Configure CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "Supermarket API v1");
        options.RoutePrefix = "swagger";
    });
}

app.UseHttpsRedirection();
app.UseCors("AllowAll");
app.UseAuthorization();
app.MapControllers();

// Health check endpoint
app.MapGet("/health", () => Results.Ok(new { status = "healthy", timestamp = DateTime.UtcNow }));

// Root endpoint with API info
app.MapGet("/", () => Results.Ok(new
{
    name = "Supermarket API",
    version = "1.0.0",
    description = "API REST para gestión de listas de compra",
    swagger = "/swagger",
    endpoints = new
    {
        items = "/api/items",
        lists = "/api/lists",
        health = "/health"
    }
}));

app.Run();
