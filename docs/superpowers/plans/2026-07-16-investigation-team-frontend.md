# InvestigationTeam Frontend Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build a full-stack frontend for the InvestigationTeam API with Angular, JWT auth, CRUD dashboard, and AI-powered chat using Google Gemini.

**Architecture:** Angular 22 frontend (Nginx) → .NET 10 Chat Backend (Gemini SDK + JWT) → InvestigationTeam API (existing) + PostgreSQL (chat DB). All deployed on K3s.

**Tech Stack:** Angular 22, .NET 10, PostgreSQL 16, Google Gemini SDK, JWT, Nginx Alpine, K3s

## Global Constraints

- Angular 22 with TypeScript 6.0
- .NET 10 with C# 13
- PostgreSQL 16
- imagePullPolicy: IfNotPresent for K3s local images
- Docker images built locally, imported via `docker save | k3s ctr images import -`
- Socat forwards for Windows access
- Swagger disabled for production (remember to re-enable for dev)
- Dark theme matching supermarket frontend (#0f172a background)

---

## File Structure

### Chat Backend (.NET)
```
investigation-team-chat-backend/
├── Dockerfile
├── src/
│   └── InvestigationTeam.Chat.Api/
│       ├── InvestigationTeam.Chat.Api.csproj
│       ├── Program.cs
│       ├── appsettings.json
│       ├── Controllers/
│       │   ├── AuthController.cs
│       │   ├── AgentsController.cs
│       │   ├── TeamsController.cs
│       │   └── ChatController.cs
│       ├── Models/
│       │   ├── User.cs
│       │   ├── ChatSession.cs
│       │   ├── ChatMessage.cs
│       │   └── Requests.cs
│       ├── Data/
│       │   └── ChatDbContext.cs
│       ├── Services/
│       │   ├── IJwtService.cs
│       │   ├── JwtService.cs
│       │   ├── IGeminiService.cs
│       │   ├── GeminiService.cs
│       │   ├── IInvestigationTeamProxy.cs
│       │   └── InvestigationTeamProxy.cs
│       └── Middleware/
│           └── ExceptionHandlingMiddleware.cs
├── k8s/
│   ├── namespace.yaml
│   ├── secret.yaml
│   ├── postgres-pvc.yaml
│   ├── postgres-deployment.yaml
│   ├── postgres-service.yaml
│   ├── api-deployment.yaml
│   └── api-service.yaml
```

### Angular Frontend
```
investigation-team-frontend/
├── Dockerfile
├── nginx.conf
├── package.json
├── tsconfig.json
├── angular.json
├── src/
│   ├── index.html
│   ├── main.ts
│   ├── styles.css
│   ├── app/
│   │   ├── app.component.ts
│   │   ├── app.config.ts
│   │   ├── app.routes.ts
│   │   ├── models/
│   │   │   ├── agent.model.ts
│   │   │   ├── team.model.ts
│   │   │   ├── user.model.ts
│   │   │   └── chat.model.ts
│   │   ├── services/
│   │   │   ├── auth.service.ts
│   │   │   ├── agents.service.ts
│   │   │   ├── teams.service.ts
│   │   │   └── chat.service.ts
│   │   ├── guards/
│   │   │   └── auth.guard.ts
│   │   ├── components/
│   │   │   ├── login/
│   │   │   │   └── login.component.ts
│   │   │   ├── register/
│   │   │   │   └── register.component.ts
│   │   │   ├── dashboard/
│   │   │   │   ├── dashboard.component.ts
│   │   │   │   ├── agents-list.component.ts
│   │   │   │   └── teams-list.component.ts
│   │   │   ├── chat/
│   │   │   │   ├── chat.component.ts
│   │   │   │   ├── conversation-list.component.ts
│   │   │   │   └── message-thread.component.ts
│   │   │   └── profile/
│   │   │       └── profile.component.ts
│   │   └── interceptors/
│   │       └── auth.interceptor.ts
│   └── environments/
│       ├── environment.ts
│       └── environment.prod.ts
└── k8s/
    ├── namespace.yaml
    ├── deployment.yaml
    └── service.yaml
```

---

## Task 1: Chat Backend - Project Structure & Models

**Files:**
- Create: `investigation-team-chat-backend/src/InvestigationTeam.Chat.Api/InvestigationTeam.Chat.Api.csproj`
- Create: `investigation-team-chat-backend/src/InvestigationTeam.Chat.Api/Models/User.cs`
- Create: `investigation-team-chat-backend/src/InvestigationTeam.Chat.Api/Models/ChatSession.cs`
- Create: `investigation-team-chat-backend/src/InvestigationTeam.Chat.Api/Models/ChatMessage.cs`
- Create: `investigation-team-chat-backend/src/InvestigationTeam.Chat.Api/Models/Requests.cs`
- Create: `investigation-team-chat-backend/src/InvestigationTeam.Chat.Api/Data/ChatDbContext.cs`
- Create: `investigation-team-chat-backend/src/InvestigationTeam.Chat.Api/Program.cs`
- Create: `investigation-team-chat-backend/src/InvestigationTeam.Chat.Api/appsettings.json`

**Interfaces:**
- Consumes: Nothing (first task)
- Produces: `User`, `ChatSession`, `ChatMessage` models, `ChatDbContext`

- [ ] **Step 1: Create project directory structure**

```bash
mkdir -p investigation-team-chat-backend/src/InvestigationTeam.Chat.Api/{Models,Data,Controllers,Services,Middleware}
```

- [ ] **Step 2: Create .csproj file**

```xml
<Project Sdk="Microsoft.NET.Sdk.Web">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Npgsql.EntityFrameworkCore.PostgreSQL" Version="10.0.0" />
    <PackageReference Include="Microsoft.EntityFrameworkCore.Tools" Version="10.0.0" />
    <PackageReference Include="Microsoft.AspNetCore.Authentication.JwtBearer" Version="10.0.0" />
    <PackageReference Include="Google.Apis.GenerativeLanguage" Version="1.68.0" />
    <PackageReference Include="BCrypt.Net-Next" Version="4.0.3" />
  </ItemGroup>
</Project>
```

- [ ] **Step 3: Create User model**

```csharp
namespace InvestigationTeam.Chat.Api.Models;

public class User
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string GeminiApiKey { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
```

- [ ] **Step 4: Create ChatSession model**

```csharp
namespace InvestigationTeam.Chat.Api.Models;

public class ChatSession
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public Guid? AgentId { get; set; }
    public Guid? TeamId { get; set; }
    public string Title { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
```

- [ ] **Step 5: Create ChatMessage model**

```csharp
namespace InvestigationTeam.Chat.Api.Models;

public class ChatMessage
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SessionId { get; set; }
    public string Role { get; set; } = string.Empty; // "user" or "assistant"
    public string Content { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
```

- [ ] **Step 6: Create request models**

```csharp
namespace InvestigationTeam.Chat.Api.Models;

public record RegisterRequest(string Email, string Password, string GeminiApiKey);
public record LoginRequest(string Email, string Password);
public record UpdateProfileRequest(string? Email, string? GeminiApiKey);
public record ChangePasswordRequest(string CurrentPassword, string NewPassword);
public record CreateSessionRequest(Guid? AgentId, Guid? TeamId, string? Title);
public record SendMessageRequest(string Content);
```

- [ ] **Step 7: Create ChatDbContext**

```csharp
using Microsoft.EntityFrameworkCore;
using InvestigationTeam.Chat.Api.Models;

namespace InvestigationTeam.Chat.Api.Data;

public class ChatDbContext : DbContext
{
    public ChatDbContext(DbContextOptions<ChatDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<ChatSession> ChatSessions => Set<ChatSession>();
    public DbSet<ChatMessage> ChatMessages => Set<ChatMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(e =>
        {
            e.HasIndex(u => u.Email).IsUnique();
            e.Property(u => u.Email).HasMaxLength(255);
        });

        modelBuilder.Entity<ChatSession>(e =>
        {
            e.HasIndex(s => new { s.UserId, s.CreatedAt });
        });

        modelBuilder.Entity<ChatMessage>(e =>
        {
            e.HasIndex(m => new { m.SessionId, m.CreatedAt });
        });
    }
}
```

- [ ] **Step 8: Create appsettings.json**

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=postgres-chat-svc;Port=5432;Database=investigation_team_chat;Username=postgres;Password=postgres"
  },
  "Jwt": {
    "Key": "super-secret-key-change-in-production-1234567890123456",
    "Issuer": "InvestigationTeamChat",
    "Audience": "InvestigationTeamChat",
    "ExpirationInMinutes": 1440
  },
  "InvestigationTeamApi": {
    "BaseUrl": "http://investigation-team-api.investigation-team.svc.cluster.local"
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  }
}
```

- [ ] **Step 9: Create minimal Program.cs**

```csharp
using Microsoft.EntityFrameworkCore;
using InvestigationTeam.Chat.Api.Data;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddDbContext<ChatDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

var app = builder.Build();

app.MapControllers();

app.Run();
```

- [ ] **Step 10: Verify build compiles**

```bash
cd investigation-team-chat-backend/src/InvestigationTeam.Chat.Api
dotnet build
```

- [ ] **Step 11: Commit**

```bash
cd /home/roman/k8s-projects
git add investigation-team-chat-backend/
git commit -m "feat(chat-backend): add project structure and data models"
```

---

## Task 2: Chat Backend - JWT Authentication

**Files:**
- Create: `investigation-team-chat-backend/src/InvestigationTeam.Chat.Api/Services/IJwtService.cs`
- Create: `investigation-team-chat-backend/src/InvestigationTeam.Chat.Api/Services/JwtService.cs`
- Create: `investigation-team-chat-backend/src/InvestigationTeam.Chat.Api/Controllers/AuthController.cs`
- Modify: `investigation-team-chat-backend/src/InvestigationTeam.Chat.Api/Program.cs`

**Interfaces:**
- Consumes: `User` model, `ChatDbContext`
- Produces: JWT tokens, `/api/auth/*` endpoints

- [ ] **Step 1: Create IJwtService interface**

```csharp
using InvestigationTeam.Chat.Api.Models;

namespace InvestigationTeam.Chat.Api.Services;

public interface IJwtService
{
    string GenerateToken(User user);
    Guid? ValidateToken(string token);
}
```

- [ ] **Step 2: Create JwtService implementation**

```csharp
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using InvestigationTeam.Chat.Api.Models;

namespace InvestigationTeam.Chat.Api.Services;

public class JwtService : IJwtService
{
    private readonly string _key;
    private readonly string _issuer;
    private readonly string _audience;
    private readonly int _expirationMinutes;

    public JwtService(IConfiguration config)
    {
        _key = config["Jwt:Key"]!;
        _issuer = config["Jwt:Issuer"]!;
        _audience = config["Jwt:Audience"]!;
        _expirationMinutes = int.Parse(config["Jwt:ExpirationInMinutes"]!);
    }

    public string GenerateToken(User user)
    {
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Email, user.Email)
        };

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_key));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _issuer,
            audience: _audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(_expirationMinutes),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public Guid? ValidateToken(string token)
    {
        var handler = new JwtSecurityTokenHandler();
        var parameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_key)),
            ValidateIssuer = true,
            ValidIssuer = _issuer,
            ValidateAudience = true,
            ValidAudience = _audience,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero
        };

        try
        {
            var principal = handler.ValidateToken(token, parameters, out _);
            var userId = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return userId != null ? Guid.Parse(userId) : null;
        }
        catch
        {
            return null;
        }
    }
}
```

- [ ] **Step 3: Create AuthController**

```csharp
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using InvestigationTeam.Chat.Api.Data;
using InvestigationTeam.Chat.Api.Models;
using InvestigationTeam.Chat.Api.Services;

namespace InvestigationTeam.Chat.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly ChatDbContext _context;
    private readonly IJwtService _jwt;

    public AuthController(ChatDbContext context, IJwtService jwt)
    {
        _context = context;
        _jwt = jwt;
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register(RegisterRequest request)
    {
        if (await _context.Users.AnyAsync(u => u.Email == request.Email))
            return Conflict("Email already registered");

        var user = new User
        {
            Email = request.Email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
            GeminiApiKey = request.GeminiApiKey
        };

        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        return Ok(new { token = _jwt.GenerateToken(user) });
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginRequest request)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == request.Email);
        if (user == null || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
            return Unauthorized("Invalid credentials");

        return Ok(new { token = _jwt.GenerateToken(user) });
    }

    [Microsoft.AspNetCore.Authorization.Authorize]
    [HttpGet("me")]
    public async Task<IActionResult> GetMe()
    {
        var userId = Guid.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);
        var user = await _context.Users.FindAsync(userId);
        if (user == null) return NotFound();

        return Ok(new { user.Id, user.Email, user.CreatedAt });
    }

    [Microsoft.AspNetCore.Authorization.Authorize]
    [HttpPut("me")]
    public async Task<IActionResult> UpdateMe(UpdateProfileRequest request)
    {
        var userId = Guid.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);
        var user = await _context.Users.FindAsync(userId);
        if (user == null) return NotFound();

        if (request.Email != null) user.Email = request.Email;
        if (request.GeminiApiKey != null) user.GeminiApiKey = request.GeminiApiKey;
        user.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        return Ok(new { user.Id, user.Email, user.CreatedAt });
    }

    [Microsoft.AspNetCore.Authorization.Authorize]
    [HttpPut("me/password")]
    public async Task<IActionResult> ChangePassword(ChangePasswordRequest request)
    {
        var userId = Guid.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);
        var user = await _context.Users.FindAsync(userId);
        if (user == null) return NotFound();

        if (!BCrypt.Net.BCrypt.Verify(request.CurrentPassword, user.PasswordHash))
            return BadRequest("Current password is incorrect");

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
        user.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        return Ok(new { message = "Password updated" });
    }
}
```

- [ ] **Step 4: Update Program.cs with JWT auth**

```csharp
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using InvestigationTeam.Chat.Api.Data;
using InvestigationTeam.Chat.Api.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddDbContext<ChatDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddScoped<IJwtService, JwtService>();

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]!)),
            ValidateIssuer = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidateAudience = true,
            ValidAudience = builder.Configuration["Jwt:Audience"],
            ValidateLifetime = true
        };
    });

builder.Services.AddAuthorization();

var app = builder.Build();

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();
```

- [ ] **Step 5: Verify build compiles**

```bash
cd investigation-team-chat-backend/src/InvestigationTeam.Chat.Api
dotnet build
```

- [ ] **Step 6: Commit**

```bash
cd /home/roman/k8s-projects
git add investigation-team-chat-backend/
git commit -m "feat(chat-backend): add JWT authentication"
```

---

## Task 3: Chat Backend - InvestigationTeam API Proxy

**Files:**
- Create: `investigation-team-chat-backend/src/InvestigationTeam.Chat.Api/Services/IInvestigationTeamProxy.cs`
- Create: `investigation-team-chat-backend/src/InvestigationTeam.Chat.Api/Services/InvestigationTeamProxy.cs`
- Create: `investigation-team-chat-backend/src/InvestigationTeam.Chat.Api/Controllers/AgentsController.cs`
- Create: `investigation-team-chat-backend/src/InvestigationTeam.Chat.Api/Controllers/TeamsController.cs`
- Modify: `investigation-team-chat-backend/src/InvestigationTeam.Chat.Api/Program.cs`

**Interfaces:**
- Consumes: InvestigationTeam API (existing)
- Produces: `/api/agents/*`, `/api/teams/*` endpoints

- [ ] **Step 1: Create IInvestigationTeamProxy interface**

```csharp
using InvestigationTeam.Chat.Api.Models;

namespace InvestigationTeam.Chat.Api.Services;

public interface IInvestigationTeamProxy
{
    Task<List<AgentDto>?> GetAgentsAsync();
    Task<AgentDto?> GetAgentAsync(Guid id);
    Task<TeamDto?> GetTeamAsync(Guid id);
    Task<List<TeamDto>?> GetTeamsAsync();
}

public record AgentDto(Guid Id, string Name, int Role, string? Description, List<string> Skills, int Status);
public record TeamDto(Guid Id, string Name, string? Description, List<Guid> AgentIds);
```

- [ ] **Step 2: Create InvestigationTeamProxy implementation**

```csharp
using System.Text.Json;
using InvestigationTeam.Chat.Api.Models;

namespace InvestigationTeam.Chat.Api.Services;

public class InvestigationTeamProxy : IInvestigationTeamProxy
{
    private readonly HttpClient _http;
    private readonly string _baseUrl;

    public InvestigationTeamProxy(HttpClient http, IConfiguration config)
    {
        _http = http;
        _baseUrl = config["InvestigationTeamApi:BaseUrl"]!;
    }

    public async Task<List<AgentDto>?> GetAgentsAsync()
    {
        var response = await _http.GetAsync($"{_baseUrl}/api/Agents");
        if (!response.IsSuccessStatusCode) return null;
        var json = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<List<AgentDto>>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
    }

    public async Task<AgentDto?> GetAgentAsync(Guid id)
    {
        var response = await _http.GetAsync($"{_baseUrl}/api/Agents/{id}");
        if (!response.IsSuccessStatusCode) return null;
        var json = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<AgentDto>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
    }

    public async Task<TeamDto?> GetTeamAsync(Guid id)
    {
        var response = await _http.GetAsync($"{_baseUrl}/api/Teams/{id}");
        if (!response.IsSuccessStatusCode) return null;
        var json = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<TeamDto>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
    }

    public async Task<List<TeamDto>?> GetTeamsAsync()
    {
        var response = await _http.GetAsync($"{_baseUrl}/api/Teams");
        if (!response.IsSuccessStatusCode) return null;
        var json = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<List<TeamDto>>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
    }
}
```

- [ ] **Step 3: Create AgentsController (proxy)**

```csharp
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using InvestigationTeam.Chat.Api.Services;

namespace InvestigationTeam.Chat.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class AgentsController : ControllerBase
{
    private readonly IInvestigationTeamProxy _proxy;

    public AgentsController(IInvestigationTeamProxy proxy) => _proxy = proxy;

    [HttpGet]
    public async Task<IActionResult> GetAgents()
    {
        var agents = await _proxy.GetAgentsAsync();
        return agents != null ? Ok(agents) : StatusCode(502);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetAgent(Guid id)
    {
        var agent = await _proxy.GetAgentAsync(id);
        return agent != null ? Ok(agent) : NotFound();
    }
}
```

- [ ] **Step 4: Create TeamsController (proxy)**

```csharp
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using InvestigationTeam.Chat.Api.Services;

namespace InvestigationTeam.Chat.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class TeamsController : ControllerBase
{
    private readonly IInvestigationTeamProxy _proxy;

    public TeamsController(IInvestigationTeamProxy proxy) => _proxy = proxy;

    [HttpGet]
    public async Task<IActionResult> GetTeams()
    {
        var teams = await _proxy.GetTeamsAsync();
        return teams != null ? Ok(teams) : StatusCode(502);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetTeam(Guid id)
    {
        var team = await _proxy.GetTeamAsync(id);
        return team != null ? Ok(team) : NotFound();
    }
}
```

- [ ] **Step 5: Update Program.cs with proxy registration**

```csharp
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using InvestigationTeam.Chat.Api.Data;
using InvestigationTeam.Chat.Api.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddDbContext<ChatDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddScoped<IJwtService, JwtService>();
builder.Services.AddHttpClient<IInvestigationTeamProxy, InvestigationTeamProxy>();

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]!)),
            ValidateIssuer = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidateAudience = true,
            ValidAudience = builder.Configuration["Jwt:Audience"],
            ValidateLifetime = true
        };
    });

builder.Services.AddAuthorization();

var app = builder.Build();

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();
```

- [ ] **Step 6: Verify build compiles**

```bash
cd investigation-team-chat-backend/src/InvestigationTeam.Chat.Api
dotnet build
```

- [ ] **Step 7: Commit**

```bash
cd /home/roman/k8s-projects
git add investigation-team-chat-backend/
git commit -m "feat(chat-backend): add InvestigationTeam API proxy"
```

---

## Task 4: Chat Backend - Gemini Service

**Files:**
- Create: `investigation-team-chat-backend/src/InvestigationTeam.Chat.Api/Services/IGeminiService.cs`
- Create: `investigation-team-chat-backend/src/InvestigationTeam.Chat.Api/Services/GeminiService.cs`
- Modify: `investigation-team-chat-backend/src/InvestigationTeam.Chat.Api/Program.cs`

**Interfaces:**
- Consumes: `User` model (GeminiApiKey), `AgentDto`, `ChatMessage` history
- Produces: AI responses via Gemini API

- [ ] **Step 1: Create IGeminiService interface**

```csharp
namespace InvestigationTeam.Chat.Api.Services;

public interface IGeminiService
{
    Task<string> GenerateResponseAsync(string apiKey, string systemPrompt, List<(string Role, string Content)> history, string userMessage);
}
```

- [ ] **Step 2: Create GeminiService implementation**

```csharp
using Google.Apis.GenerativeLanguage.v1;
using Google.Apis.GenerativeLanguage.v1.Data;
using Google.Apis.Services;
using InvestigationTeam.Chat.Api.Models;

namespace InvestigationTeam.Chat.Api.Services;

public class GeminiService : IGeminiService
{
    public async Task<string> GenerateResponseAsync(string apiKey, string systemPrompt, List<(string Role, string Content)> history, string userMessage)
    {
        var service = new GenerativeService(new BaseClientService.Initializer
        {
            ApiKey = apiKey,
            ApplicationName = "InvestigationTeamChat"
        });

        var contents = new List<Content>();

        foreach (var msg in history)
        {
            contents.Add(new Content
            {
                Role = msg.Role == "user" ? "user" : "model",
                Parts = new[] { new Part { Text = msg.Content } }
            });
        }

        contents.Add(new Content
        {
            Role = "user",
            Parts = new[] { new Part { Text = userMessage } }
        });

        var request = new GenerateContentRequest
        {
            Contents = contents,
            SystemInstruction = new Content
            {
                Parts = new[] { new Part { Text = systemPrompt } }
            },
            GenerationConfig = new GenerationConfig
            {
                Temperature = 0.7f,
                MaxOutputTokens = 2048
            }
        };

        var response = await service.Models.GenerateContentAsync(request, "gemini-2.0-flash");
        return response.Candidates[0].Content.Parts[0].Text;
    }
}
```

- [ ] **Step 3: Register GeminiService in Program.cs**

Add to Program.cs after `builder.Services.AddHttpClient<...>();`:

```csharp
builder.Services.AddScoped<IGeminiService, GeminiService>();
```

- [ ] **Step 4: Verify build compiles**

```bash
cd investigation-team-chat-backend/src/InvestigationTeam.Chat.Api
dotnet build
```

- [ ] **Step 5: Commit**

```bash
cd /home/roman/k8s-projects
git add investigation-team-chat-backend/
git commit -m "feat(chat-backend): add Gemini AI service"
```

---

## Task 5: Chat Backend - Chat Controller & Session Management

**Files:**
- Create: `investigation-team-chat-backend/src/InvestigationTeam.Chat.Api/Controllers/ChatController.cs`
- Modify: `investigation-team-chat-backend/src/InvestigationTeam.Chat.Api/Program.cs`

**Interfaces:**
- Consumes: `User`, `ChatSession`, `ChatMessage`, `AgentDto`, `IJwtService`, `IGeminiService`, `IInvestigationTeamProxy`
- Produces: `/api/chat/*` endpoints

- [ ] **Step 1: Create ChatController**

```csharp
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using InvestigationTeam.Chat.Api.Data;
using InvestigationTeam.Chat.Api.Models;
using InvestigationTeam.Chat.Api.Services;

namespace InvestigationTeam.Chat.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ChatController : ControllerBase
{
    private readonly ChatDbContext _context;
    private readonly IGeminiService _gemini;
    private readonly IInvestigationTeamProxy _proxy;

    public ChatController(ChatDbContext context, IGeminiService gemini, IInvestigationTeamProxy proxy)
    {
        _context = context;
        _gemini = gemini;
        _proxy = proxy;
    }

    private Guid GetUserId() => Guid.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);

    [HttpGet("sessions")]
    public async Task<IActionResult> GetSessions()
    {
        var sessions = await _context.ChatSessions
            .Where(s => s.UserId == GetUserId())
            .OrderByDescending(s => s.UpdatedAt)
            .Select(s => new { s.Id, s.AgentId, s.TeamId, s.Title, s.CreatedAt, s.UpdatedAt })
            .ToListAsync();
        return Ok(sessions);
    }

    [HttpPost("sessions")]
    public async Task<IActionResult> CreateSession(CreateSessionRequest request)
    {
        var title = request.Title ?? "New Chat";
        if (request.AgentId.HasValue)
        {
            var agent = await _proxy.GetAgentAsync(request.AgentId.Value);
            title = $"Chat with {agent?.Name ?? "Agent"}";
        }
        else if (request.TeamId.HasValue)
        {
            var team = await _proxy.GetTeamAsync(request.TeamId.Value);
            title = $"Chat with {team?.Name ?? "Team"}";
        }

        var session = new ChatSession
        {
            UserId = GetUserId(),
            AgentId = request.AgentId,
            TeamId = request.TeamId,
            Title = title
        };

        _context.ChatSessions.Add(session);
        await _context.SaveChangesAsync();

        return CreatedAtAction(nameof(GetMessages), new { id = session.Id }, new { session.Id, session.Title });
    }

    [HttpGet("sessions/{id}/messages")]
    public async Task<IActionResult> GetMessages(Guid id)
    {
        var session = await _context.ChatSessions.FirstOrDefaultAsync(s => s.Id == id && s.UserId == GetUserId());
        if (session == null) return NotFound();

        var messages = await _context.ChatMessages
            .Where(m => m.SessionId == id)
            .OrderBy(m => m.CreatedAt)
            .Select(m => new { m.Id, m.Role, m.Content, m.CreatedAt })
            .ToListAsync();

        return Ok(messages);
    }

    [HttpPost("sessions/{id}/messages")]
    public async Task<IActionResult> SendMessage(Guid id, SendMessageRequest request)
    {
        var session = await _context.ChatSessions.FirstOrDefaultAsync(s => s.Id == id && s.UserId == GetUserId());
        if (session == null) return NotFound();

        var user = await _context.Users.FirstAsync(u => u.Id == GetUserId());
        if (string.IsNullOrEmpty(user.GeminiApiKey))
            return BadRequest("Gemini API key not configured. Update your profile.");

        // Save user message
        var userMessage = new ChatMessage
        {
            SessionId = id,
            Role = "user",
            Content = request.Content
        };
        _context.ChatMessages.Add(userMessage);
        await _context.SaveChangesAsync();

        // Build system prompt
        string systemPrompt = "Eres un asistente de investigación útil y profesional.";
        if (session.AgentId.HasValue)
        {
            var agent = await _proxy.GetAgentAsync(session.AgentId.Value);
            if (agent != null)
            {
                var roleNames = new[] { "Investigador", "Analista", "Escritor", "Coordinador", "Revisor" };
                systemPrompt = $"Eres {agent.Name}, un {roleNames[agent.Role]} con habilidades en {string.Join(", ", agent.Skills)}. {agent.Description}";
            }
        }
        else if (session.TeamId.HasValue)
        {
            var team = await _proxy.GetTeamAsync(session.TeamId.Value);
            if (team != null)
            {
                var agents = new List<string>();
                foreach (var agentId in team.AgentIds)
                {
                    var agent = await _proxy.GetAgentAsync(agentId);
                    if (agent != null)
                    {
                        var roleNames = new[] { "Investigador", "Analista", "Escritor", "Coordinador", "Revisor" };
                        agents.Add($"{agent.Name} ({roleNames[agent.Role]})");
                    }
                }
                systemPrompt = $"Eres el equipo de investigación '{team.Name}'. {team.Description}. Miembros: {string.Join(", ", agents)}. Responde como el miembro más adecuado según el tema.";
            }
        }

        // Build history
        var history = await _context.ChatMessages
            .Where(m => m.SessionId == id)
            .OrderBy(m => m.CreatedAt)
            .Take(20)
            .Select(m => new { m.Role, m.Content })
            .ToListAsync();

        var historyTuples = history.Select(m => (m.Role, m.Content)).ToList();

        // Call Gemini
        var response = await _gemini.GenerateResponseAsync(user.GeminiApiKey, systemPrompt, historyTuples, request.Content);

        // Save assistant message
        var assistantMessage = new ChatMessage
        {
            SessionId = id,
            Role = "assistant",
            Content = response
        };
        _context.ChatMessages.Add(assistantMessage);

        session.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        return Ok(new { content = response });
    }

    [HttpDelete("sessions/{id}")]
    public async Task<IActionResult> DeleteSession(Guid id)
    {
        var session = await _context.ChatSessions.FirstOrDefaultAsync(s => s.Id == id && s.UserId == GetUserId());
        if (session == null) return NotFound();

        var messages = await _context.ChatMessages.Where(m => m.SessionId == id).ToListAsync();
        _context.ChatMessages.RemoveRange(messages);
        _context.ChatSessions.Remove(session);
        await _context.SaveChangesAsync();

        return Ok(new { message = "Session deleted" });
    }
}
```

- [ ] **Step 2: Verify build compiles**

```bash
cd investigation-team-chat-backend/src/InvestigationTeam.Chat.Api
dotnet build
```

- [ ] **Step 3: Commit**

```bash
cd /home/roman/k8s-projects
git add investigation-team-chat-backend/
git commit -m "feat(chat-backend): add chat controller with Gemini integration"
```

---

## Task 6: Chat Backend - Dockerfile & K8s Manifests

**Files:**
- Create: `investigation-team-chat-backend/Dockerfile`
- Create: `investigation-team-chat-backend/k8s/namespace.yaml`
- Create: `investigation-team-chat-backend/k8s/secret.yaml`
- Create: `investigation-team-chat-backend/k8s/postgres-pvc.yaml`
- Create: `investigation-team-chat-backend/k8s/postgres-deployment.yaml`
- Create: `investigation-team-chat-backend/k8s/postgres-service.yaml`
- Create: `investigation-team-chat-backend/k8s/api-deployment.yaml`
- Create: `investigation-team-chat-backend/k8s/api-service.yaml`

**Interfaces:**
- Consumes: Chat Backend .NET app
- Produces: K8s deployment ready

- [ ] **Step 1: Create Dockerfile**

```dockerfile
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY src/InvestigationTeam.Chat.Api/*.csproj src/InvestigationTeam.Chat.Api/
RUN dotnet restore src/InvestigationTeam.Chat.Api/InvestigationTeam.Chat.Api.csproj

COPY src/InvestigationTeam.Chat.Api/ src/InvestigationTeam.Chat.Api/
RUN dotnet publish src/InvestigationTeam.Chat.Api/InvestigationTeam.Chat.Api.csproj -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8000
EXPOSE 8000

ENTRYPOINT ["dotnet", "InvestigationTeam.Chat.Api.dll"]
```

- [ ] **Step 2: Create namespace.yaml**

```yaml
apiVersion: v1
kind: Namespace
metadata:
  name: investigation-team-frontend
```

- [ ] **Step 3: Create secret.yaml**

```yaml
apiVersion: v1
kind: Secret
metadata:
  name: chat-db-secret
  namespace: investigation-team-frontend
type: Opaque
stringData:
  POSTGRES_DB: investigation_team_chat
  POSTGRES_USER: postgres
  POSTGRES_PASSWORD: postgres
  ConnectionStrings__DefaultConnection: "Host=postgres-chat-svc;Port=5432;Database=investigation_team_chat;Username=postgres;Password=postgres"
  Jwt__Key: "super-secret-key-change-in-production-1234567890123456"
```

- [ ] **Step 4: Create postgres-pvc.yaml**

```yaml
apiVersion: v1
kind: PersistentVolumeClaim
metadata:
  name: postgres-chat-pvc
  namespace: investigation-team-frontend
spec:
  accessModes:
    - ReadWriteOnce
  resources:
    requests:
      storage: 2Gi
```

- [ ] **Step 5: Create postgres-deployment.yaml**

```yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: postgres-chat
  namespace: investigation-team-frontend
spec:
  replicas: 1
  selector:
    matchLabels:
      app: postgres-chat
  template:
    metadata:
      labels:
        app: postgres-chat
    spec:
      containers:
        - name: postgres
          image: postgres:16-alpine
          ports:
            - containerPort: 5432
          env:
            - name: POSTGRES_DB
              valueFrom:
                secretKeyRef:
                  name: chat-db-secret
                  key: POSTGRES_DB
            - name: POSTGRES_USER
              valueFrom:
                secretKeyRef:
                  name: chat-db-secret
                  key: POSTGRES_USER
            - name: POSTGRES_PASSWORD
              valueFrom:
                secretKeyRef:
                  name: chat-db-secret
                  key: POSTGRES_PASSWORD
          volumeMounts:
            - name: postgres-data
              mountPath: /var/lib/postgresql/data
          resources:
            requests:
              cpu: 100m
              memory: 256Mi
            limits:
              cpu: 500m
              memory: 512Mi
      volumes:
        - name: postgres-data
          persistentVolumeClaim:
            claimName: postgres-chat-pvc
```

- [ ] **Step 6: Create postgres-service.yaml**

```yaml
apiVersion: v1
kind: Service
metadata:
  name: postgres-chat-svc
  namespace: investigation-team-frontend
spec:
  type: ClusterIP
  selector:
    app: postgres-chat
  ports:
    - port: 5432
      targetPort: 5432
```

- [ ] **Step 7: Create api-deployment.yaml**

```yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: investigation-team-chat-api
  namespace: investigation-team-frontend
spec:
  replicas: 1
  selector:
    matchLabels:
      app: investigation-team-chat-api
  template:
    metadata:
      labels:
        app: investigation-team-chat-api
    spec:
      containers:
        - name: api
          image: investigation-team-chat-api:latest
          ports:
            - containerPort: 8000
          env:
            - name: ConnectionStrings__DefaultConnection
              valueFrom:
                secretKeyRef:
                  name: chat-db-secret
                  key: ConnectionStrings__DefaultConnection
            - name: Jwt__Key
              valueFrom:
                secretKeyRef:
                  name: chat-db-secret
                  key: Jwt__Key
            - name: InvestigationTeamApi__BaseUrl
              value: "http://investigation-team-api.investigation-team.svc.cluster.local"
          imagePullPolicy: IfNotPresent
          readinessProbe:
            httpGet:
              path: /api/auth/me
              port: 8000
            initialDelaySeconds: 10
            periodSeconds: 5
          resources:
            requests:
              cpu: 100m
              memory: 128Mi
            limits:
              cpu: 500m
              memory: 256Mi
```

- [ ] **Step 8: Create api-service.yaml**

```yaml
apiVersion: v1
kind: Service
metadata:
  name: investigation-team-chat-api-svc
  namespace: investigation-team-frontend
spec:
  type: LoadBalancer
  selector:
    app: investigation-team-chat-api
  ports:
    - port: 80
      targetPort: 8000
      nodePort: 32445
```

- [ ] **Step 9: Commit**

```bash
cd /home/roman/k8s-projects
git add investigation-team-chat-backend/
git commit -m "feat(chat-backend): add Dockerfile and K8s manifests"
```

---

## Task 7: Angular Frontend - Project Structure & Models

**Files:**
- Create: `investigation-team-frontend/package.json`
- Create: `investigation-team-frontend/tsconfig.json`
- Create: `investigation-team-frontend/angular.json`
- Create: `investigation-team-frontend/src/index.html`
- Create: `investigation-team-frontend/src/main.ts`
- Create: `investigation-team-frontend/src/styles.css`
- Create: `investigation-team-frontend/src/app/app.component.ts`
- Create: `investigation-team-frontend/src/app/app.config.ts`
- Create: `investigation-team-frontend/src/app/app.routes.ts`
- Create: `investigation-team-frontend/src/app/models/agent.model.ts`
- Create: `investigation-team-frontend/src/app/models/team.model.ts`
- Create: `investigation-team-frontend/src/app/models/user.model.ts`
- Create: `investigation-team-frontend/src/app/models/chat.model.ts`

**Interfaces:**
- Consumes: Chat Backend API endpoints
- Produces: Angular app structure with models

- [ ] **Step 1: Create directory structure**

```bash
mkdir -p investigation-team-frontend/src/app/{models,services,guards,interceptors,components/{login,register,dashboard,chat,profile}}
```

- [ ] **Step 2: Create package.json**

```json
{
  "name": "investigation-team-frontend",
  "version": "1.0.0",
  "scripts": {
    "ng": "ng",
    "start": "ng serve",
    "build": "ng build",
    "watch": "ng build --watch --configuration development"
  },
  "private": true,
  "dependencies": {
    "@angular/animations": "^22.0.0",
    "@angular/common": "^22.0.0",
    "@angular/compiler": "^22.0.0",
    "@angular/core": "^22.0.0",
    "@angular/forms": "^22.0.0",
    "@angular/platform-browser": "^22.0.0",
    "@angular/platform-browser-dynamic": "^22.0.0",
    "@angular/router": "^22.0.0",
    "rxjs": "~7.8.0",
    "tslib": "^2.6.0",
    "zone.js": "~0.15.0"
  },
  "devDependencies": {
    "@angular-devkit/build-angular": "^22.0.0",
    "@angular/cli": "^22.0.0",
    "@angular/compiler-cli": "^22.0.0",
    "typescript": "~6.0.0"
  }
}
```

- [ ] **Step 3: Create tsconfig.json**

```json
{
  "compileOnSave": false,
  "compilerOptions": {
    "outDir": "./dist/out-tsc",
    "forceConsistentCasingInFileNames": true,
    "strict": true,
    "noImplicitOverride": true,
    "noPropertyAccessFromIndexSignature": true,
    "noImplicitReturns": true,
    "noFallthroughCasesInSwitch": true,
    "sourceMap": true,
    "declaration": false,
    "downlevelIteration": true,
    "experimentalDecorators": true,
    "moduleResolution": "bundler",
    "importHelpers": true,
    "target": "ES2022",
    "module": "ES2022",
    "lib": ["ES2022", "dom"],
    "skipLibCheck": true
  },
  "angularCompilerOptions": {
    "enableI18nLegacyMessageIdFormat": false,
    "strictInjectionParameters": true,
    "strictInputAccessModifiers": true,
    "strictTemplates": true
  }
}
```

- [ ] **Step 4: Create angular.json**

```json
{
  "$schema": "./node_modules/@angular/cli/lib/config/schema.json",
  "version": 1,
  "newProjectRoot": "projects",
  "projects": {
    "investigation-team-frontend": {
      "projectType": "application",
      "schematics": {
        "@schematics/angular:component": {
          "style": "css",
          "standalone": true
        }
      },
      "root": "",
      "sourceRoot": "src",
      "prefix": "app",
      "architect": {
        "build": {
          "builder": "@angular-devkit/build-angular:application",
          "options": {
            "outputPath": "dist/investigation-team-frontend",
            "index": "src/index.html",
            "browser": "src/main.ts",
            "polyfills": ["zone.js"],
            "tsConfig": "tsconfig.json",
            "inlineStyleLanguage": "scss",
            "styles": ["src/styles.css"],
            "scripts": []
          },
          "configurations": {
            "production": {
              "budgets": [
                { "type": "initial", "maximumWarning": "500kB", "maximumError": "1MB" },
                { "type": "anyComponentStyle", "maximumWarning": "4kB", "maximumError": "8kB" }
              ],
              "outputHashing": "all"
            },
            "development": {
              "optimization": false,
              "extractLicenses": false,
              "sourceMap": true
            }
          },
          "defaultConfiguration": "production"
        },
        "serve": {
          "builder": "@angular-devkit/build-angular:dev-server",
          "configurations": {
            "production": { "buildTarget": "investigation-team-frontend:build:production" },
            "development": { "buildTarget": "investigation-team-frontend:build:development" }
          },
          "defaultConfiguration": "development"
        }
      }
    }
  }
}
```

- [ ] **Step 5: Create src/index.html**

```html
<!doctype html>
<html lang="es">
<head>
  <meta charset="utf-8">
  <title>Investigation Team</title>
  <base href="/">
  <meta name="viewport" content="width=device-width, initial-scale=1">
</head>
<body>
  <app-root></app-root>
</body>
</html>
```

- [ ] **Step 6: Create src/main.ts**

```typescript
import { bootstrapApplication } from '@angular/platform-browser';
import { AppComponent } from './app/app.component';
import { appConfig } from './app/app.config';

bootstrapApplication(AppComponent, appConfig).catch(err => console.error(err));
```

- [ ] **Step 7: Create src/styles.css**

```css
* {
  margin: 0;
  padding: 0;
  box-sizing: border-box;
}

body {
  font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif;
  background-color: #0f172a;
  color: #e2e8f0;
  min-height: 100vh;
}

a { color: #38bdf8; text-decoration: none; }
a:hover { text-decoration: underline; }

button {
  cursor: pointer;
  border: none;
  border-radius: 6px;
  padding: 8px 16px;
  font-size: 14px;
  transition: background-color 0.2s;
}

.btn-primary { background-color: #38bdf8; color: #0f172a; }
.btn-primary:hover { background-color: #0ea5e9; }

.btn-secondary { background-color: #334155; color: #e2e8f0; }
.btn-secondary:hover { background-color: #475569; }

.btn-danger { background-color: #ef4444; color: white; }
.btn-danger:hover { background-color: #dc2626; }

input, textarea, select {
  width: 100%;
  padding: 10px 12px;
  border: 1px solid #334155;
  border-radius: 6px;
  background-color: #1e293b;
  color: #e2e8f0;
  font-size: 14px;
}

input:focus, textarea:focus, select:focus {
  outline: none;
  border-color: #38bdf8;
}

.card {
  background-color: #1e293b;
  border-radius: 8px;
  padding: 20px;
  border: 1px solid #334155;
}

.error { color: #ef4444; font-size: 12px; }
.success { color: #22c55e; font-size: 12px; }
```

- [ ] **Step 8: Create app.component.ts**

```typescript
import { Component } from '@angular/core';
import { RouterOutlet } from '@angular/router';

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [RouterOutlet],
  template: `<router-outlet />`
})
export class AppComponent {}
```

- [ ] **Step 9: Create app.config.ts**

```typescript
import { ApplicationConfig, provideZoneChangeDetection } from '@angular/core';
import { provideRouter } from '@angular/router';
import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { routes } from './app.routes';
import { authInterceptor } from './interceptors/auth.interceptor';

export const appConfig: ApplicationConfig = {
  providers: [
    provideZoneChangeDetection({ eventCoalescing: true }),
    provideRouter(routes),
    provideHttpClient(withInterceptors([authInterceptor]))
  ]
};
```

- [ ] **Step 10: Create app.routes.ts**

```typescript
import { Routes } from '@angular/router';
import { authGuard } from './guards/auth.guard';

export const routes: Routes = [
  { path: 'login', loadComponent: () => import('./components/login/login.component').then(m => m.LoginComponent) },
  { path: 'register', loadComponent: () => import('./components/register/register.component').then(m => m.RegisterComponent) },
  {
    path: '',
    loadComponent: () => import('./components/dashboard/dashboard.component').then(m => m.DashboardComponent),
    canActivate: [authGuard],
    children: [
      { path: 'agents', loadComponent: () => import('./components/dashboard/agents-list.component').then(m => m.AgentsListComponent) },
      { path: 'teams', loadComponent: () => import('./components/dashboard/teams-list.component').then(m => m.TeamsListComponent) },
      { path: 'chat', loadComponent: () => import('./components/chat/chat.component').then(m => m.ChatComponent) },
      { path: 'profile', loadComponent: () => import('./components/profile/profile.component').then(m => m.ProfileComponent) },
      { path: '', redirectTo: 'agents', pathMatch: 'full' }
    ]
  },
  { path: '**', redirectTo: '' }
];
```

- [ ] **Step 11: Create model files**

```typescript
// models/agent.model.ts
export interface Agent {
  id: string;
  name: string;
  role: number;
  description?: string;
  skills: string[];
  status: number;
}

export const ROLE_NAMES = ['Investigador', 'Analista', 'Escritor', 'Coordinador', 'Revisor'];
export const ROLE_EMOJIS = ['🔍', '📊', '✍️', '🎯', '✅'];
```

```typescript
// models/team.model.ts
export interface Team {
  id: string;
  name: string;
  description?: string;
  agentIds: string[];
}
```

```typescript
// models/user.model.ts
export interface User {
  id: string;
  email: string;
  createdAt: string;
}
```

```typescript
// models/chat.model.ts
export interface ChatSession {
  id: string;
  agentId?: string;
  teamId?: string;
  title: string;
  createdAt: string;
  updatedAt: string;
}

export interface ChatMessage {
  id: string;
  role: 'user' | 'assistant';
  content: string;
  createdAt: string;
}
```

- [ ] **Step 12: Commit**

```bash
cd /home/roman/k8s-projects
git add investigation-team-frontend/
git commit -m "feat(frontend): add Angular project structure and models"
```

---

## Task 8: Angular Frontend - Auth Service & Guards

**Files:**
- Create: `investigation-team-frontend/src/app/services/auth.service.ts`
- Create: `investigation-team-frontend/src/app/guards/auth.guard.ts`
- Create: `investigation-team-frontend/src/app/interceptors/auth.interceptor.ts`

**Interfaces:**
- Consumes: `User` model
- Produces: Auth service, guard, interceptor

- [ ] **Step 1: Create auth.service.ts**

```typescript
import { Injectable, signal, computed } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Router } from '@angular/router';
import { tap } from 'rxjs';
import { User } from '../models/user.model';

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly API_URL = '/api/auth';
  private tokenSignal = signal<string | null>(localStorage.getItem('token'));
  private userSignal = signal<User | null>(null);

  token = this.tokenSignal.asReadonly();
  isAuthenticated = computed(() => !!this.tokenSignal());
  user = this.userSignal.asReadonly();

  constructor(private http: HttpClient, private router: Router) {
    if (this.tokenSignal()) {
      this.loadUser();
    }
  }

  login(email: string, password: string) {
    return this.http.post<{ token: string }>(`${this.API_URL}/login`, { email, password })
      .pipe(tap(res => {
        localStorage.setItem('token', res.token);
        this.tokenSignal.set(res.token);
        this.loadUser();
      }));
  }

  register(email: string, password: string, geminiApiKey: string) {
    return this.http.post<{ token: string }>(`${this.API_URL}/register`, { email, password, geminiApiKey })
      .pipe(tap(res => {
        localStorage.setItem('token', res.token);
        this.tokenSignal.set(res.token);
        this.loadUser();
      }));
  }

  logout() {
    localStorage.removeItem('token');
    this.tokenSignal.set(null);
    this.userSignal.set(null);
    this.router.navigate(['/login']);
  }

  loadUser() {
    this.http.get<User>(`${this.API_URL}/me`).subscribe({
      next: user => this.userSignal.set(user),
      error: () => this.logout()
    });
  }

  updateProfile(email?: string, geminiApiKey?: string) {
    return this.http.put<User>(`${this.API_URL}/me`, { email, geminiApiKey })
      .pipe(tap(() => this.loadUser()));
  }

  changePassword(currentPassword: string, newPassword: string) {
    return this.http.put(`${this.API_URL}/me/password`, { currentPassword, newPassword });
  }
}
```

- [ ] **Step 2: Create auth.guard.ts**

```typescript
import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AuthService } from '../services/auth.service';

export const authGuard: CanActivateFn = () => {
  const auth = inject(AuthService);
  const router = inject(Router);

  if (auth.isAuthenticated()) {
    return true;
  }

  router.navigate(['/login']);
  return false;
};
```

- [ ] **Step 3: Create auth.interceptor.ts**

```typescript
import { HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { AuthService } from '../services/auth.service';

export const authInterceptor: HttpInterceptorFn = (req, next) => {
  const auth = inject(AuthService);
  const token = auth.token();

  if (token) {
    req = req.clone({
      setHeaders: { Authorization: `Bearer ${token}` }
    });
  }

  return next(req);
};
```

- [ ] **Step 4: Commit**

```bash
cd /home/roman/k8s-projects
git add investigation-team-frontend/
git commit -m "feat(frontend): add auth service, guard, and interceptor"
```

---

## Task 9: Angular Frontend - Login & Register Components

**Files:**
- Create: `investigation-team-frontend/src/app/components/login/login.component.ts`
- Create: `investigation-team-frontend/src/app/components/register/register.component.ts`

**Interfaces:**
- Consumes: `AuthService`
- Produces: Login/Register pages

- [ ] **Step 1: Create login.component.ts**

```typescript
import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { AuthService } from '../../services/auth.service';

@Component({
  selector: 'app-login',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink],
  template: `
    <div class="login-container">
      <div class="card login-card">
        <h1>🔍 Investigation Team</h1>
        <p>Inicia sesión para continuar</p>

        <form (ngSubmit)="onSubmit()">
          <div class="form-group">
            <label>Email</label>
            <input type="email" [(ngModel)]="email" name="email" required>
          </div>

          <div class="form-group">
            <label>Password</label>
            <input type="password" [(ngModel)]="password" name="password" required>
          </div>

          <div class="error" *ngIf="error">{{ error }}</div>

          <button type="submit" class="btn-primary full-width" [disabled]="loading">
            {{ loading ? 'Ingresando...' : 'Iniciar Sesión' }}
          </button>
        </form>

        <p class="link">¿No tienes cuenta? <a routerLink="/register">Regístrate</a></p>
      </div>
    </div>
  `,
  styles: [`
    .login-container { display: flex; justify-content: center; align-items: center; min-height: 100vh; }
    .login-card { width: 100%; max-width: 400px; text-align: center; }
    h1 { margin-bottom: 8px; }
    .form-group { margin-bottom: 16px; text-align: left; }
    .form-group label { display: block; margin-bottom: 4px; font-size: 14px; }
    .full-width { width: 100%; margin-top: 16px; padding: 12px; }
    .link { margin-top: 16px; font-size: 14px; }
  `]
})
export class LoginComponent {
  email = '';
  password = '';
  loading = false;
  error = '';

  constructor(private auth: AuthService, private router: Router) {}

  onSubmit() {
    this.loading = true;
    this.error = '';
    this.auth.login(this.email, this.password).subscribe({
      next: () => this.router.navigate(['/']),
      error: (err) => {
        this.error = err.error || 'Error al iniciar sesión';
        this.loading = false;
      }
    });
  }
}
```

- [ ] **Step 2: Create register.component.ts**

```typescript
import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { AuthService } from '../../services/auth.service';

@Component({
  selector: 'app-register',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink],
  template: `
    <div class="register-container">
      <div class="card register-card">
        <h1>🔍 Investigation Team</h1>
        <p>Crea tu cuenta</p>

        <form (ngSubmit)="onSubmit()">
          <div class="form-group">
            <label>Email</label>
            <input type="email" [(ngModel)]="email" name="email" required>
          </div>

          <div class="form-group">
            <label>Password</label>
            <input type="password" [(ngModel)]="password" name="password" required minlength="6">
          </div>

          <div class="form-group">
            <label>Gemini API Key</label>
            <input type="password" [(ngModel)]="geminiApiKey" name="geminiApiKey" required>
            <small>Obtén tu key en <a href="https://aistudio.google.com/apikey" target="_blank">Google AI Studio</a></small>
          </div>

          <div class="error" *ngIf="error">{{ error }}</div>

          <button type="submit" class="btn-primary full-width" [disabled]="loading">
            {{ loading ? 'Creando...' : 'Crear Cuenta' }}
          </button>
        </form>

        <p class="link">¿Ya tienes cuenta? <a routerLink="/login">Inicia sesión</a></p>
      </div>
    </div>
  `,
  styles: [`
    .register-container { display: flex; justify-content: center; align-items: center; min-height: 100vh; }
    .register-card { width: 100%; max-width: 400px; text-align: center; }
    h1 { margin-bottom: 8px; }
    .form-group { margin-bottom: 16px; text-align: left; }
    .form-group label { display: block; margin-bottom: 4px; font-size: 14px; }
    .form-group small { display: block; margin-top: 4px; font-size: 12px; color: #94a3b8; }
    .full-width { width: 100%; margin-top: 16px; padding: 12px; }
    .link { margin-top: 16px; font-size: 14px; }
  `]
})
export class RegisterComponent {
  email = '';
  password = '';
  geminiApiKey = '';
  loading = false;
  error = '';

  constructor(private auth: AuthService, private router: Router) {}

  onSubmit() {
    this.loading = true;
    this.error = '';
    this.auth.register(this.email, this.password, this.geminiApiKey).subscribe({
      next: () => this.router.navigate(['/']),
      error: (err) => {
        this.error = err.error || 'Error al crear cuenta';
        this.loading = false;
      }
    });
  }
}
```

- [ ] **Step 3: Commit**

```bash
cd /home/roman/k8s-projects
git add investigation-team-frontend/
git commit -m "feat(frontend): add login and register components"
```

---

## Task 10: Angular Frontend - Dashboard & CRUD Components

**Files:**
- Create: `investigation-team-frontend/src/app/services/agents.service.ts`
- Create: `investigation-team-frontend/src/app/services/teams.service.ts`
- Create: `investigation-team-frontend/src/app/components/dashboard/dashboard.component.ts`
- Create: `investigation-team-frontend/src/app/components/dashboard/agents-list.component.ts`
- Create: `investigation-team-frontend/src/app/components/dashboard/teams-list.component.ts`

**Interfaces:**
- Consumes: `Agent`, `Team` models
- Produces: Dashboard with CRUD for agents and teams

- [ ] **Step 1: Create agents.service.ts**

```typescript
import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Agent } from '../models/agent.model';

@Injectable({ providedIn: 'root' })
export class AgentsService {
  private readonly API_URL = '/api/agents';

  constructor(private http: HttpClient) {}

  getAll() { return this.http.get<Agent[]>(this.API_URL); }
  getById(id: string) { return this.http.get<Agent>(`${this.API_URL}/${id}`); }
  create(agent: Partial<Agent>) { return this.http.post<Agent>(this.API_URL, agent); }
  update(id: string, agent: Partial<Agent>) { return this.http.put(`${this.API_URL}/${id}`, agent); }
  delete(id: string) { return this.http.delete(`${this.API_URL}/${id}`); }
}
```

- [ ] **Step 2: Create teams.service.ts**

```typescript
import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Team } from '../models/team.model';

@Injectable({ providedIn: 'root' })
export class TeamsService {
  private readonly API_URL = '/api/teams';

  constructor(private http: HttpClient) {}

  getAll() { return this.http.get<Team[]>(this.API_URL); }
  getById(id: string) { return this.http.get<Team>(`${this.API_URL}/${id}`); }
  create(team: Partial<Team>) { return this.http.post<Team>(this.API_URL, team); }
  update(id: string, team: Partial<Team>) { return this.http.put(`${this.API_URL}/${id}`, team); }
  delete(id: string) { return this.http.delete(`${this.API_URL}/${id}`); }
  addAgent(teamId: string, agentId: string) { return this.http.post(`${this.API_URL}/${teamId}/agents/${agentId}`, {}); }
  removeAgent(teamId: string, agentId: string) { return this.http.delete(`${this.API_URL}/${teamId}/agents/${agentId}`); }
}
```

- [ ] **Step 3: Create dashboard.component.ts**

```typescript
import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { AuthService } from '../../services/auth.service';

@Component({
  selector: 'app-dashboard',
  standalone: true,
  imports: [CommonModule, RouterOutlet, RouterLink, RouterLinkActive],
  template: `
    <div class="dashboard-layout">
      <aside class="sidebar">
        <div class="logo">🔍 Investigation Team</div>
        <nav>
          <a routerLink="/agents" routerLinkActive="active">Agents</a>
          <a routerLink="/teams" routerLinkActive="active">Teams</a>
          <a routerLink="/chat" routerLinkActive="active">Chat</a>
          <a routerLink="/profile" routerLinkActive="active">Profile</a>
        </nav>
        <button class="btn-secondary logout" (click)="auth.logout()">Logout</button>
      </aside>
      <main class="content">
        <router-outlet />
      </main>
    </div>
  `,
  styles: [`
    .dashboard-layout { display: flex; min-height: 100vh; }
    .sidebar { width: 240px; background-color: #1e293b; padding: 20px; display: flex; flex-direction: column; }
    .logo { font-size: 18px; font-weight: bold; margin-bottom: 30px; }
    nav { flex: 1; }
    nav a { display: block; padding: 10px 12px; border-radius: 6px; margin-bottom: 4px; color: #94a3b8; }
    nav a:hover { background-color: #334155; color: #e2e8f0; }
    nav a.active { background-color: #38bdf8; color: #0f172a; }
    .logout { margin-top: auto; width: 100%; }
    .content { flex: 1; padding: 20px; }
  `]
})
export class DashboardComponent {
  constructor(public auth: AuthService) {}
}
```

- [ ] **Step 4: Create agents-list.component.ts**

```typescript
import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { AgentsService } from '../../services/agents.service';
import { Agent, ROLE_NAMES } from '../../models/agent.model';

@Component({
  selector: 'app-agents-list',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <div class="header">
      <h2>Agents</h2>
      <button class="btn-primary" (click)="showForm = true">+ New Agent</button>
    </div>

    <table *ngIf="agents.length > 0">
      <thead>
        <tr>
          <th>Name</th>
          <th>Role</th>
          <th>Status</th>
          <th>Skills</th>
          <th>Actions</th>
        </tr>
      </thead>
      <tbody>
        <tr *ngFor="let agent of agents">
          <td>{{ agent.name }}</td>
          <td>{{ roleNames[agent.role] }}</td>
          <td>{{ agent.status === 0 ? 'Active' : agent.status === 1 ? 'Inactive' : 'Busy' }}</td>
          <td>{{ agent.skills.join(', ') }}</td>
          <td>
            <button class="btn-secondary" (click)="edit(agent)">Edit</button>
            <button class="btn-danger" (click)="delete(agent.id)">Delete</button>
          </td>
        </tr>
      </tbody>
    </table>

    <p *ngIf="agents.length === 0 && !loading">No agents yet. Create one!</p>

    <!-- Modal -->
    <div class="modal-overlay" *ngIf="showForm" (click)="closeForm()">
      <div class="card modal" (click)="$event.stopPropagation()">
        <h3>{{ editing ? 'Edit Agent' : 'New Agent' }}</h3>
        <form (ngSubmit)="save()">
          <div class="form-group">
            <label>Name</label>
            <input [(ngModel)]="form.name" name="name" required>
          </div>
          <div class="form-group">
            <label>Role</label>
            <select [(ngModel)]="form.role" name="role">
              <option *ngFor="let r of roleNames; let i = index" [value]="i">{{ r }}</option>
            </select>
          </div>
          <div class="form-group">
            <label>Description</label>
            <input [(ngModel)]="form.description" name="description">
          </div>
          <div class="form-group">
            <label>Skills (comma-separated)</label>
            <input [(ngModel)]="form.skillsText" name="skills">
          </div>
          <div class="modal-actions">
            <button type="button" class="btn-secondary" (click)="closeForm()">Cancel</button>
            <button type="submit" class="btn-primary">Save</button>
          </div>
        </form>
      </div>
    </div>
  `,
  styles: [`
    .header { display: flex; justify-content: space-between; align-items: center; margin-bottom: 20px; }
    table { width: 100%; border-collapse: collapse; }
    th, td { padding: 12px; text-align: left; border-bottom: 1px solid #334155; }
    th { color: #94a3b8; }
    .modal-overlay { position: fixed; top: 0; left: 0; right: 0; bottom: 0; background: rgba(0,0,0,0.7); display: flex; justify-content: center; align-items: center; }
    .modal { width: 400px; }
    .form-group { margin-bottom: 12px; }
    .form-group label { display: block; margin-bottom: 4px; font-size: 14px; }
    .modal-actions { display: flex; gap: 8px; justify-content: flex-end; margin-top: 16px; }
  `]
})
export class AgentsListComponent implements OnInit {
  agents: Agent[] = [];
  roleNames = ROLE_NAMES;
  loading = true;
  showForm = false;
  editing: Agent | null = null;
  form = { name: '', role: 0, description: '', skillsText: '' };

  constructor(private agentsService: AgentsService) {}

  ngOnInit() { this.load(); }

  load() {
    this.agentsService.getAll().subscribe({
      next: agents => { this.agents = agents; this.loading = false; },
      error: () => this.loading = false
    });
  }

  edit(agent: Agent) {
    this.editing = agent;
    this.form = { name: agent.name, role: agent.role, description: agent.description || '', skillsText: agent.skills.join(', ') };
    this.showForm = true;
  }

  save() {
    const agent = { name: this.form.name, role: +this.form.role, description: this.form.description, skills: this.form.skillsText.split(',').map(s => s.trim()).filter(Boolean) };
    const req = this.editing ? this.agentsService.update(this.editing.id, agent) : this.agentsService.create(agent);
    req.subscribe(() => { this.closeForm(); this.load(); });
  }

  delete(id: string) {
    if (confirm('Delete this agent?')) {
      this.agentsService.delete(id).subscribe(() => this.load());
    }
  }

  closeForm() {
    this.showForm = false;
    this.editing = null;
    this.form = { name: '', role: 0, description: '', skillsText: '' };
  }
}
```

- [ ] **Step 5: Create teams-list.component.ts**

```typescript
import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { TeamsService } from '../../services/teams.service';
import { AgentsService } from '../../services/agents.service';
import { Team } from '../../models/team.model';
import { Agent } from '../../models/agent.model';

@Component({
  selector: 'app-teams-list',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <div class="header">
      <h2>Teams</h2>
      <button class="btn-primary" (click)="showForm = true">+ New Team</button>
    </div>

    <table *ngIf="teams.length > 0">
      <thead>
        <tr>
          <th>Name</th>
          <th>Description</th>
          <th>Members</th>
          <th>Actions</th>
        </tr>
      </thead>
      <tbody>
        <tr *ngFor="let team of teams">
          <td>{{ team.name }}</td>
          <td>{{ team.description }}</td>
          <td>{{ team.agentIds.length }}</td>
          <td>
            <button class="btn-secondary" (click)="edit(team)">Edit</button>
            <button class="btn-danger" (click)="delete(team.id)">Delete</button>
          </td>
        </tr>
      </tbody>
    </table>

    <p *ngIf="teams.length === 0 && !loading">No teams yet. Create one!</p>

    <!-- Modal -->
    <div class="modal-overlay" *ngIf="showForm" (click)="closeForm()">
      <div class="card modal" (click)="$event.stopPropagation()">
        <h3>{{ editing ? 'Edit Team' : 'New Team' }}</h3>
        <form (ngSubmit)="save()">
          <div class="form-group">
            <label>Name</label>
            <input [(ngModel)]="form.name" name="name" required>
          </div>
          <div class="form-group">
            <label>Description</label>
            <input [(ngModel)]="form.description" name="description">
          </div>
          <div class="form-group" *ngIf="editing">
            <label>Members</label>
            <div *ngFor="let agent of allAgents" class="checkbox">
              <input type="checkbox" [checked]="form.agentIds.includes(agent.id)" (change)="toggleAgent(agent.id)">
              {{ agent.name }}
            </div>
          </div>
          <div class="modal-actions">
            <button type="button" class="btn-secondary" (click)="closeForm()">Cancel</button>
            <button type="submit" class="btn-primary">Save</button>
          </div>
        </form>
      </div>
    </div>
  `,
  styles: [`
    .header { display: flex; justify-content: space-between; align-items: center; margin-bottom: 20px; }
    table { width: 100%; border-collapse: collapse; }
    th, td { padding: 12px; text-align: left; border-bottom: 1px solid #334155; }
    th { color: #94a3b8; }
    .modal-overlay { position: fixed; top: 0; left: 0; right: 0; bottom: 0; background: rgba(0,0,0,0.7); display: flex; justify-content: center; align-items: center; }
    .modal { width: 400px; }
    .form-group { margin-bottom: 12px; }
    .form-group label { display: block; margin-bottom: 4px; font-size: 14px; }
    .checkbox { padding: 4px 0; }
    .modal-actions { display: flex; gap: 8px; justify-content: flex-end; margin-top: 16px; }
  `]
})
export class TeamsListComponent implements OnInit {
  teams: Team[] = [];
  allAgents: Agent[] = [];
  loading = true;
  showForm = false;
  editing: Team | null = null;
  form = { name: '', description: '', agentIds: [] as string[] };

  constructor(private teamsService: TeamsService, private agentsService: AgentsService) {}

  ngOnInit() {
    this.teamsService.getAll().subscribe({ next: t => { this.teams = t; this.loading = false; } });
    this.agentsService.getAll().subscribe(a => this.allAgents = a);
  }

  edit(team: Team) {
    this.editing = team;
    this.form = { name: team.name, description: team.description || '', agentIds: [...team.agentIds] };
    this.showForm = true;
  }

  save() {
    const team = { name: this.form.name, description: this.form.description };
    const req = this.editing ? this.teamsService.update(this.editing.id, team) : this.teamsService.create(team);
    req.subscribe(() => {
      if (this.editing) {
        const toAdd = this.form.agentIds.filter(id => !this.editing!.agentIds.includes(id));
        const toRemove = this.editing.agentIds.filter(id => !this.form.agentIds.includes(id));
        toAdd.forEach(id => this.teamsService.addAgent(this.editing!.id, id).subscribe());
        toRemove.forEach(id => this.teamsService.removeAgent(this.editing!.id, id).subscribe());
      }
      this.closeForm();
      this.teamsService.getAll().subscribe(t => this.teams = t);
    });
  }

  delete(id: string) {
    if (confirm('Delete this team?')) {
      this.teamsService.delete(id).subscribe(() => this.teamsService.getAll().subscribe(t => this.teams = t));
    }
  }

  toggleAgent(agentId: string) {
    const idx = this.form.agentIds.indexOf(agentId);
    idx > -1 ? this.form.agentIds.splice(idx, 1) : this.form.agentIds.push(agentId);
  }

  closeForm() {
    this.showForm = false;
    this.editing = null;
    this.form = { name: '', description: '', agentIds: [] };
  }
}
```

- [ ] **Step 6: Commit**

```bash
cd /home/roman/k8s-projects
git add investigation-team-frontend/
git commit -m "feat(frontend): add dashboard and CRUD components"
```

---

## Task 11: Angular Frontend - Chat Components

**Files:**
- Create: `investigation-team-frontend/src/app/services/chat.service.ts`
- Create: `investigation-team-frontend/src/app/components/chat/chat.component.ts`
- Create: `investigation-team-frontend/src/app/components/chat/conversation-list.component.ts`
- Create: `investigation-team-frontend/src/app/components/chat/message-thread.component.ts`

**Interfaces:**
- Consumes: `ChatSession`, `ChatMessage`, `Agent`, `Team` models
- Produces: Chat interface

- [ ] **Step 1: Create chat.service.ts**

```typescript
import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { ChatSession, ChatMessage } from '../models/chat.model';

@Injectable({ providedIn: 'root' })
export class ChatService {
  private readonly API_URL = '/api/chat';

  constructor(private http: HttpClient) {}

  getSessions() { return this.http.get<ChatSession[]>(`${this.API_URL}/sessions`); }
  createSession(agentId?: string, teamId?: string) { return this.http.post<ChatSession>(`${this.API_URL}/sessions`, { agentId, teamId }); }
  getMessages(sessionId: string) { return this.http.get<ChatMessage[]>(`${this.API_URL}/sessions/${sessionId}/messages`); }
  sendMessage(sessionId: string, content: string) { return this.http.post<{ content: string }>(`${this.API_URL}/sessions/${sessionId}/messages`, { content }); }
  deleteSession(sessionId: string) { return this.http.delete(`${this.API_URL}/sessions/${sessionId}`); }
}
```

- [ ] **Step 2: Create conversation-list.component.ts**

```typescript
import { Component, EventEmitter, Input, Output } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ChatSession } from '../../models/chat.model';

@Component({
  selector: 'app-conversation-list',
  standalone: true,
  imports: [CommonModule],
  template: `
    <div class="conversation-list">
      <div class="list-header">
        <h3>Conversations</h3>
        <button class="btn-primary" (click)="newChat.emit()">+ New</button>
      </div>
      <div class="conversations">
        <div *ngFor="let session of sessions" class="conversation-item" [class.active]="session.id === activeId" (click)="select.emit(session.id)">
          <span class="title">{{ session.title }}</span>
          <button class="btn-icon" (click)="delete.emit(session.id); $event.stopPropagation()">×</button>
        </div>
        <p *ngIf="sessions.length === 0" class="empty">No conversations yet</p>
      </div>
    </div>
  `,
  styles: [`
    .conversation-list { height: 100%; display: flex; flex-direction: column; }
    .list-header { display: flex; justify-content: space-between; align-items: center; padding: 12px; border-bottom: 1px solid #334155; }
    .conversations { flex: 1; overflow-y: auto; }
    .conversation-item { display: flex; justify-content: space-between; align-items: center; padding: 12px; cursor: pointer; border-bottom: 1px solid #1e293b; }
    .conversation-item:hover { background-color: #334155; }
    .conversation-item.active { background-color: #38bdf8; color: #0f172a; }
    .title { flex: 1; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
    .btn-icon { background: none; color: inherit; padding: 4px 8px; font-size: 16px; }
    .empty { padding: 20px; text-align: center; color: #64748b; }
  `]
})
export class ConversationListComponent {
  @Input() sessions: ChatSession[] = [];
  @Input() activeId: string | null = null;
  @Output() select = new EventEmitter<string>();
  @Output() delete = new EventEmitter<string>();
  @Output() newChat = new EventEmitter<void>();
}
```

- [ ] **Step 3: Create message-thread.component.ts**

```typescript
import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ChatMessage } from '../../models/chat.model';

@Component({
  selector: 'app-message-thread',
  standalone: true,
  imports: [CommonModule],
  template: `
    <div class="thread">
      <div *ngFor="let msg of messages" class="message" [class.user]="msg.role === 'user'" [class.assistant]="msg.role === 'assistant'">
        <div class="bubble">{{ msg.content }}</div>
      </div>
      <div *ngIf="loading" class="message assistant">
        <div class="bubble typing">Typing...</div>
      </div>
    </div>
  `,
  styles: [`
    .thread { flex: 1; overflow-y: auto; padding: 20px; display: flex; flex-direction: column; gap: 12px; }
    .message { display: flex; }
    .message.user { justify-content: flex-end; }
    .message.assistant { justify-content: flex-start; }
    .bubble { max-width: 70%; padding: 12px 16px; border-radius: 12px; line-height: 1.5; }
    .message.user .bubble { background-color: #38bdf8; color: #0f172a; }
    .message.assistant .bubble { background-color: #334155; color: #e2e8f0; }
    .typing { font-style: italic; color: #94a3b8; }
  `]
})
export class MessageThreadComponent {
  @Input() messages: ChatMessage[] = [];
  @Input() loading = false;
}
```

- [ ] **Step 4: Create chat.component.ts**

```typescript
import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ConversationListComponent } from './conversation-list.component';
import { MessageThreadComponent } from './message-thread.component';
import { ChatService } from '../../services/chat.service';
import { AgentsService } from '../../services/agents.service';
import { TeamsService } from '../../services/teams.service';
import { ChatSession, ChatMessage } from '../../models/chat.model';
import { Agent, ROLE_EMOJIS } from '../../models/agent.model';
import { Team } from '../../models/team.model';

@Component({
  selector: 'app-chat',
  standalone: true,
  imports: [CommonModule, FormsModule, ConversationListComponent, MessageThreadComponent],
  template: `
    <div class="chat-layout">
      <div class="sidebar">
        <app-conversation-list
          [sessions]="sessions"
          [activeId]="activeSessionId"
          (select)="selectSession($event)"
          (delete)="deleteSession($event)"
          (newChat)="showNewChat = true"
        />
      </div>
      <div class="chat-main">
        <div *ngIf="!activeSessionId && !showNewChat" class="empty-state">
          <h2>🔍 Investigation Team Chat</h2>
          <p>Select a conversation or start a new one</p>
        </div>

        <div *ngIf="showNewChat" class="new-chat">
          <h3>New Conversation</h3>
          <div class="options">
            <h4>Chat with Agent</h4>
            <div class="agent-grid">
              <button *ngFor="let agent of agents" class="agent-card" (click)="createSession(agent.id)">
                <span class="emoji">{{ roleEmojis[agent.role] }}</span>
                <span>{{ agent.name }}</span>
                <small>{{ roleNames[agent.role] }}</small>
              </button>
            </div>
            <h4>Chat with Team</h4>
            <div class="agent-grid">
              <button *ngFor="let team of teams" class="agent-card" (click)="createSession(undefined, team.id)">
                <span class="emoji">👥</span>
                <span>{{ team.name }}</span>
                <small>{{ team.agentIds.length }} members</small>
              </button>
            </div>
            <button class="btn-secondary" (click)="showNewChat = false">Cancel</button>
          </div>
        </div>

        <div *ngIf="activeSessionId && !showNewChat" class="active-chat">
          <app-message-thread [messages]="messages" [loading]="sending" />
          <div class="input-area">
            <textarea [(ngModel)]="newMessage" placeholder="Type your message..." (keydown.enter)="$event.preventDefault(); send()"></textarea>
            <button class="btn-primary" (click)="send()" [disabled]="!newMessage.trim() || sending">Send</button>
          </div>
        </div>
      </div>
    </div>
  `,
  styles: [`
    .chat-layout { display: flex; height: calc(100vh - 40px); }
    .sidebar { width: 280px; border-right: 1px solid #334155; }
    .chat-main { flex: 1; display: flex; flex-direction: column; }
    .empty-state { display: flex; flex-direction: column; align-items: center; justify-content: center; height: 100%; color: #64748b; }
    .new-chat { padding: 20px; }
    .options { margin-top: 16px; }
    .agent-grid { display: grid; grid-template-columns: repeat(auto-fill, minmax(150px, 1fr)); gap: 12px; margin: 12px 0 24px; }
    .agent-card { display: flex; flex-direction: column; align-items: center; padding: 16px; background-color: #1e293b; border: 1px solid #334155; border-radius: 8px; cursor: pointer; }
    .agent-card:hover { border-color: #38bdf8; }
    .emoji { font-size: 24px; margin-bottom: 8px; }
    .active-chat { display: flex; flex-direction: column; flex: 1; }
    .input-area { display: flex; gap: 8px; padding: 16px; border-top: 1px solid #334155; }
    .input-area textarea { flex: 1; resize: none; height: 60px; }
    .input-area button { align-self: flex-end; }
  `]
})
export class ChatComponent implements OnInit {
  sessions: ChatSession[] = [];
  messages: ChatMessage[] = [];
  agents: Agent[] = [];
  teams: Team[] = [];
  activeSessionId: string | null = null;
  showNewChat = false;
  newMessage = '';
  sending = false;
  roleEmojis = ROLE_EMOJIS;
  roleNames = ['Investigador', 'Analista', 'Escritor', 'Coordinador', 'Revisor'];

  constructor(private chatService: ChatService, private agentsService: AgentsService, private teamsService: TeamsService) {}

  ngOnInit() {
    this.chatService.getSessions().subscribe(s => this.sessions = s);
    this.agentsService.getAll().subscribe(a => this.agents = a);
    this.teamsService.getAll().subscribe(t => this.teams = t);
  }

  selectSession(id: string) {
    this.activeSessionId = id;
    this.showNewChat = false;
    this.chatService.getMessages(id).subscribe(m => this.messages = m);
  }

  createSession(agentId?: string, teamId?: string) {
    this.chatService.createSession(agentId, teamId).subscribe(session => {
      this.sessions.unshift(session);
      this.selectSession(session.id);
      this.showNewChat = false;
    });
  }

  send() {
    if (!this.newMessage.trim() || !this.activeSessionId) return;
    this.sending = true;
    const msg = this.newMessage;
    this.newMessage = '';

    this.messages.push({ id: '', role: 'user', content: msg, createdAt: new Date().toISOString() });

    this.chatService.sendMessage(this.activeSessionId, msg).subscribe({
      next: res => {
        this.messages.push({ id: '', role: 'assistant', content: res.content, createdAt: new Date().toISOString() });
        this.sending = false;
      },
      error: () => {
        this.messages.push({ id: '', role: 'assistant', content: 'Error: No se pudo obtener respuesta.', createdAt: new Date().toISOString() });
        this.sending = false;
      }
    });
  }

  deleteSession(id: string) {
    this.chatService.deleteSession(id).subscribe(() => {
      this.sessions = this.sessions.filter(s => s.id !== id);
      if (this.activeSessionId === id) this.activeSessionId = null;
    });
  }
}
```

- [ ] **Step 5: Commit**

```bash
cd /home/roman/k8s-projects
git add investigation-team-frontend/
git commit -m "feat(frontend): add chat components"
```

---

## Task 12: Angular Frontend - Profile Component

**Files:**
- Create: `investigation-team-frontend/src/app/components/profile/profile.component.ts`

**Interfaces:**
- Consumes: `AuthService`
- Produces: Profile page

- [ ] **Step 1: Create profile.component.ts**

```typescript
import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { AuthService } from '../../services/auth.service';

@Component({
  selector: 'app-profile',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <div class="profile-container">
      <h2>Profile</h2>

      <div class="card">
        <h3>Account Info</h3>
        <form (ngSubmit)="updateProfile()">
          <div class="form-group">
            <label>Email</label>
            <input type="email" [(ngModel)]="email" name="email">
          </div>
          <div class="form-group">
            <label>Gemini API Key</label>
            <input type="password" [(ngModel)]="geminiApiKey" name="geminiApiKey">
          </div>
          <div class="success" *ngIf="profileSuccess">{{ profileSuccess }}</div>
          <div class="error" *ngIf="profileError">{{ profileError }}</div>
          <button type="submit" class="btn-primary">Update Profile</button>
        </form>
      </div>

      <div class="card">
        <h3>Change Password</h3>
        <form (ngSubmit)="changePassword()">
          <div class="form-group">
            <label>Current Password</label>
            <input type="password" [(ngModel)]="currentPassword" name="currentPassword">
          </div>
          <div class="form-group">
            <label>New Password</label>
            <input type="password" [(ngModel)]="newPassword" name="newPassword">
          </div>
          <div class="success" *ngIf="passwordSuccess">{{ passwordSuccess }}</div>
          <div class="error" *ngIf="passwordError">{{ passwordError }}</div>
          <button type="submit" class="btn-primary">Change Password</button>
        </form>
      </div>
    </div>
  `,
  styles: [`
    .profile-container { max-width: 500px; }
    .card { margin-bottom: 20px; }
    .form-group { margin-bottom: 12px; }
    .form-group label { display: block; margin-bottom: 4px; font-size: 14px; }
  `]
})
export class ProfileComponent implements OnInit {
  email = '';
  geminiApiKey = '';
  currentPassword = '';
  newPassword = '';
  profileSuccess = '';
  profileError = '';
  passwordSuccess = '';
  passwordError = '';

  constructor(private auth: AuthService) {}

  ngOnInit() {
    const user = this.auth.user();
    if (user) {
      this.email = user.email;
    }
  }

  updateProfile() {
    this.profileSuccess = '';
    this.profileError = '';
    this.auth.updateProfile(this.email, this.geminiApiKey || undefined).subscribe({
      next: () => this.profileSuccess = 'Profile updated!',
      error: (err) => this.profileError = err.error || 'Error updating profile'
    });
  }

  changePassword() {
    this.passwordSuccess = '';
    this.passwordError = '';
    this.auth.changePassword(this.currentPassword, this.newPassword).subscribe({
      next: () => { this.passwordSuccess = 'Password changed!'; this.currentPassword = ''; this.newPassword = ''; },
      error: (err) => this.passwordError = err.error || 'Error changing password'
    });
  }
}
```

- [ ] **Step 2: Commit**

```bash
cd /home/roman/k8s-projects
git add investigation-team-frontend/
git commit -m "feat(frontend): add profile component"
```

---

## Task 13: Angular Frontend - Dockerfile, Nginx & K8s

**Files:**
- Create: `investigation-team-frontend/Dockerfile`
- Create: `investigation-team-frontend/nginx.conf`
- Create: `investigation-team-frontend/k8s/namespace.yaml`
- Create: `investigation-team-frontend/k8s/deployment.yaml`
- Create: `investigation-team-frontend/k8s/service.yaml`

**Interfaces:**
- Consumes: Angular app
- Produces: K8s deployment ready

- [ ] **Step 1: Create Dockerfile**

```dockerfile
FROM node:22-alpine AS build
WORKDIR /app

COPY package.json package-lock.json* ./
RUN npm install

COPY . .
RUN npm run build

FROM nginx:alpine
COPY --from=build /app/dist/investigation-team-frontend/browser /usr/share/nginx/html
COPY nginx.conf /etc/nginx/conf.d/default.conf

EXPOSE 80
CMD ["nginx", "-g", "daemon off;"]
```

- [ ] **Step 2: Create nginx.conf**

```nginx
server {
    listen 80;
    server_name localhost;
    root /usr/share/nginx/html;
    index index.html;

    location /api/ {
        proxy_pass http://investigation-team-chat-api-svc:80;
        proxy_http_version 1.1;
        proxy_set_header Upgrade $http_upgrade;
        proxy_set_header Connection 'upgrade';
        proxy_set_header Host $host;
        proxy_cache_bypass $http_upgrade;
    }

    location / {
        try_files $uri $uri/ /index.html;
    }

    location /assets {
        expires 1y;
        add_header Cache-Control "public, immutable";
    }

    gzip on;
    gzip_types text/plain text/css application/json application/javascript text/xml application/xml text/javascript;
}
```

- [ ] **Step 3: Create k8s/namespace.yaml**

```yaml
apiVersion: v1
kind: Namespace
metadata:
  name: investigation-team-frontend
```

- [ ] **Step 4: Create k8s/deployment.yaml**

```yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: investigation-team-frontend
  namespace: investigation-team-frontend
spec:
  replicas: 1
  selector:
    matchLabels:
      app: investigation-team-frontend
  template:
    metadata:
      labels:
        app: investigation-team-frontend
    spec:
      containers:
        - name: frontend
          image: investigation-team-frontend:latest
          ports:
            - containerPort: 80
          imagePullPolicy: IfNotPresent
          readinessProbe:
            httpGet:
              path: /
              port: 80
            initialDelaySeconds: 5
            periodSeconds: 10
          resources:
            requests:
              cpu: 50m
              memory: 64Mi
            limits:
              cpu: 200m
              memory: 128Mi
```

- [ ] **Step 5: Create k8s/service.yaml**

```yaml
apiVersion: v1
kind: Service
metadata:
  name: investigation-team-frontend-svc
  namespace: investigation-team-frontend
spec:
  type: LoadBalancer
  selector:
    app: investigation-team-frontend
  ports:
    - port: 80
      targetPort: 80
      nodePort: 30081
```

- [ ] **Step 6: Commit**

```bash
cd /home/roman/k8s-projects
git add investigation-team-frontend/
git commit -m "feat(frontend): add Dockerfile, Nginx, and K8s manifests"
```

---

## Task 14: Build & Deploy All Services

**Files:**
- All existing files

**Interfaces:**
- Consumes: All previous tasks
- Produces: Running services on K3s

- [ ] **Step 1: Build Chat Backend Docker image**

```bash
cd /home/roman/k8s-projects/investigation-team-chat-backend
sudo docker build -t investigation-team-chat-api:latest .
```

- [ ] **Step 2: Import Chat Backend image to K3s**

```bash
sudo docker save investigation-team-chat-api:latest | sudo k3s ctr images import -
```

- [ ] **Step 3: Apply Chat Backend K8s manifests**

```bash
kubectl apply -f /home/roman/k8s-projects/investigation-team-chat-backend/k8s/
```

- [ ] **Step 4: Wait for Chat Backend pods to be ready**

```bash
kubectl wait --for=condition=ready pod -l app=investigation-team-chat-api -n investigation-team-frontend --timeout=120s
```

- [ ] **Step 5: Build Frontend Docker image**

```bash
cd /home/roman/k8s-projects/investigation-team-frontend
sudo docker build -t investigation-team-frontend:latest .
```

- [ ] **Step 6: Import Frontend image to K3s**

```bash
sudo docker save investigation-team-frontend:latest | sudo k3s ctr images import -
```

- [ ] **Step 7: Apply Frontend K8s manifests**

```bash
kubectl apply -f /home/roman/k8s-projects/investigation-team-frontend/k8s/
```

- [ ] **Step 8: Wait for Frontend pods to be ready**

```bash
kubectl wait --for=condition=ready pod -l app=investigation-team-frontend -n investigation-team-frontend --timeout=120s
```

- [ ] **Step 9: Set up Socat forwards for Windows access**

```bash
# Chat Backend (32445)
nohup sudo socat TCP-LISTEN:32445,fork,reuseaddr,bind=0.0.0.0 TCP:$(kubectl get svc investigation-team-chat-api-svc -n investigation-team-frontend -o jsonpath='{.spec.clusterIP}'):80 > /dev/null 2>&1 &

# Frontend (30081)
nohup sudo socat TCP-LISTEN:30081,fork,reuseaddr,bind=0.0.0.0 TCP:$(kubectl get svc investigation-team-frontend-svc -n investigation-team-frontend -o jsonpath='{.spec.clusterIP}'):80 > /dev/null 2>&1 &

# InvestigationTeam API (32444)
nohup sudo socat TCP-LISTEN:32444,fork,reuseaddr,bind=0.0.0.0 TCP:$(kubectl get svc investigation-team-api -n investigation-team -o jsonpath='{.spec.clusterIP}'):8000 > /dev/null 2>&1 &
```

- [ ] **Step 10: Verify services are accessible**

```bash
curl -s localhost:30081 | head -5
curl -s localhost:32445/api/auth/me
curl -s localhost:32444/api/agents
```

- [ ] **Step 11: Commit**

```bash
cd /home/roman/k8s-projects
git add -A
git commit -m "feat: deploy investigation team frontend stack"
```

---

## Task 15: Update Project Documentation

**Files:**
- Modify: `CONTEXT.md`

**Interfaces:**
- Consumes: All previous tasks
- Produces: Updated documentation

- [ ] **Step 1: Update CONTEXT.md with new services**

Add to the architecture diagram:
```
   NAMESPACE: investigation-team-frontend
   ┌──────────────────────────────────────────────────────────┐
   │  ANGULAR FRONTEND (Deployment, 1 replica)                │
   │  - Angular 22 + Nginx                                    │
   │  - Puerto: 80 → Service LoadBalancer: 30081              │
   │  - Login, Dashboard CRUD, Chat con Gemini                │
   ├──────────────────────────────────────────────────────────┤
   │  .NET CHAT BACKEND (Deployment, 1 replica)               │
   │  - .NET 10 + Gemini SDK                                  │
   │  - Puerto: 8000 → Service LoadBalancer: 32445            │
   │  - JWT Auth, Chat con AI, Proxy a InvestigationTeam API  │
   ├──────────────────────────────────────────────────────────┤
   │  PostgreSQL Chat DB (Deployment, 1 replica)              │
   │  - Puerto: 5432                                          │
   │  - Usuarios, sesiones, historial                         │
   └──────────────────────────────────────────────────────────┘
```

Add to the stack table:
| InvestigationTeam Frontend | Angular 22 + Nginx | Deployment (1) | LoadBalancer | 80 |
| InvestigationTeam Chat Backend | .NET 10 + Gemini | Deployment (1) | LoadBalancer | 8000 |
| InvestigationTeam Chat DB | PostgreSQL 16 | Deployment (1) | ClusterIP | 5432 |

- [ ] **Step 2: Commit**

```bash
cd /home/roman/k8s-projects
git add CONTEXT.md
git commit -m "docs: update CONTEXT.md with investigation team frontend"
```
