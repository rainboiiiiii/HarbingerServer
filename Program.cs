using System.Text;
using GameBackend.Api.Auth;
using GameBackend.Api.Data;
using GameBackend.Api.Endpoints;
using GameBackend.Api.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<MongoOptions>(builder.Configuration.GetSection("Mongo"));
builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection("Jwt"));
builder.Services.Configure<ProgressionOptions>(builder.Configuration.GetSection("Progression"));
builder.Services.Configure<BattlePassOptions>(builder.Configuration.GetSection("BattlePass"));
builder.Services.Configure<MatchmakingOptions>(builder.Configuration.GetSection("Matchmaking"));
builder.Services.Configure<CorsOptions>(builder.Configuration.GetSection("Cors"));

builder.Services.Configure<JsonOptions>(options =>
{
    options.SerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
});

builder.Services.AddSingleton<MongoDbContext>();
builder.Services.AddSingleton<PasswordService>();
builder.Services.AddSingleton<JwtTokenService>();
builder.Services.AddSingleton<LoginRateLimiter>();
builder.Services.AddSingleton<ProgressionService>();
builder.Services.AddSingleton<MatchmakingService>();
builder.Services.AddSingleton<MatchReportService>();

builder.Services.AddCors(options =>
{
    var corsOptions = builder.Configuration.GetSection("Cors").Get<CorsOptions>() ?? new CorsOptions();
    var allowedOrigins = corsOptions.AllowedOrigins?.Length > 0
        ? corsOptions.AllowedOrigins
        : new[]
        {
            "http://localhost",
            "http://localhost:3000",
            "http://localhost:5173",
            "http://localhost:8080"
        };

    options.AddPolicy("Default", policy =>
    {
        policy.WithOrigins(allowedOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        var jwtSection = builder.Configuration.GetSection("Jwt").Get<JwtOptions>() ?? new JwtOptions();
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtSection.Issuer,
            ValidAudience = jwtSection.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSection.Key)),
            NameClaimType = "unique_name"
        };
    });

builder.Services.AddAuthorization();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo { Title = "Game Backend API", Version = "v1" });
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme.",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT"
    });
    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

var app = builder.Build();

var mongoContext = app.Services.GetRequiredService<MongoDbContext>();
await mongoContext.EnsureIndexesAsync();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
else
{
    // Swagger still enabled in production for debugging; protect behind network controls if needed.
    app.UseSwagger();
    app.UseSwaggerUI();
    //app.UseHttpsRedirection();
}

app.UseCors("Default");
app.UseAuthentication();
app.UseAuthorization();

app.MapAccountEndpoints();
app.MapProgressionEndpoints();
app.MapMatchmakingEndpoints();
app.MapMatchReportEndpoints();

app.MapGet("/", () => new { message = "Harbinger Server is Live!", status = "Running", documentation = "/swagger" });

app.MapGet("/health", () => Results.Json(new { status = "ok" }))
    .WithName("Health");

app.Run();
