using Microsoft.EntityFrameworkCore;
using MongoDB.Driver;
using StackExchange.Redis;
using PixelHub.Api.Data;
using PixelHub.Api.Models;
using PixelHub.Api.Repositories;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<PixelHubContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Postgres")));

// Le MongoClient est thread-safe et gère son propre pool de connexions :
// on l'enregistre donc en singleton.
builder.Services.AddSingleton<IMongoClient>(_ =>
    new MongoClient(builder.Configuration.GetConnectionString("Mongo")));

builder.Services.AddSingleton<IMongoDatabase>(sp =>
    sp.GetRequiredService<IMongoClient>()
      .GetDatabase(builder.Configuration["Mongo:Database"]));

// Redis : ConnectionMultiplexer gère son propre pool de sockets et est conçu pour être un singleton
builder.Services.AddSingleton<IConnectionMultiplexer>(_ =>
    ConnectionMultiplexer.Connect(builder.Configuration.GetConnectionString("Redis")!));

// Services et dépôts Redis
builder.Services.AddSingleton<ILeaderboard, RedisLeaderboard>();
builder.Services.AddSingleton<RedisMatchmakingQueue>();
builder.Services.AddSingleton<RedisPresence>();

// Décorateur CachedGameCatalog autour de MongoGameCatalog
builder.Services.AddSingleton<MongoGameCatalog>();
builder.Services.AddSingleton<IGameCatalog>(sp =>
    new CachedGameCatalog(
        sp.GetRequiredService<MongoGameCatalog>(),
        sp.GetRequiredService<IConnectionMultiplexer>()));

var app = builder.Build();

// Crée la base et les tables au démarrage.
// Suffisant en TP ; dans un vrai projet, on utilise les migrations EF Core.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<PixelHubContext>();
    db.Database.EnsureCreated();

    if (!db.Players.Any())
    {
        db.Players.AddRange(
            new Player { Pseudo = "Nova",  Coins = 1200 },
            new Player { Pseudo = "Krayz", Coins = 350  },
            new Player { Pseudo = "Ombre", Coins = 90   }
        );
        db.SaveChanges();
    }
}

app.MapGet("/", () => "PixelHub API — séance 3 OK");

// PostgreSQL
app.MapGet("/players", async (PixelHubContext db) =>
    await db.Players.ToListAsync());

// MongoDB + Cache Redis
app.MapGet("/games", async (IGameCatalog catalog) =>
    await catalog.GetAllAsync());

app.MapGet("/games/genre/{genre}", async (string genre, IGameCatalog catalog) =>
    await catalog.GetByGenreAsync(genre));

app.MapGet("/games/top/{count:int}", async (int count, IGameCatalog catalog) =>
    await catalog.GetTopRatedAsync(count));

app.MapGet("/games/stats", async (IGameCatalog catalog) =>
    await catalog.GetStatsByGenreAsync());

app.MapGet("/games/timed", async (IGameCatalog catalog) =>
{
    var chrono = System.Diagnostics.Stopwatch.StartNew();
    var jeux = await catalog.GetAllAsync();
    chrono.Stop();
    return new { nombre = jeux.Count, millisecondes = chrono.Elapsed.TotalMilliseconds };
});

// Redis — Classement
app.MapPost("/leaderboard/{pseudo}/points/{points:double}",
    async (string pseudo, double points, ILeaderboard lb) =>
    { await lb.AddPointsAsync(pseudo, points); return Results.Ok(); });

app.MapGet("/leaderboard/top/{count:int}",
    async (int count, ILeaderboard lb) => await lb.GetTopAsync(count));

app.MapGet("/leaderboard/rank/{pseudo}",
    async (string pseudo, ILeaderboard lb) => await lb.GetRankAsync(pseudo));

// Redis — Matchmaking (bonus)
app.MapPost("/matchmaking/{pseudo}", async (string pseudo, RedisMatchmakingQueue file) =>
{
    await file.EnqueueAsync(pseudo);
    return Results.Ok(new { enAttente = await file.LengthAsync() });
});

app.MapPost("/matchmaking/match", async (RedisMatchmakingQueue file) =>
    await file.TryMatchAsync() is (string joueur1, string joueur2)
        ? Results.Ok(new { joueur1, joueur2 })
        : Results.NoContent());

// Redis — Présence en ligne (recommandé)
app.MapPost("/online/{pseudo}", async (string pseudo, RedisPresence p) =>
{ await p.TouchAsync(pseudo); return Results.Ok(); });

app.MapGet("/online/count", async (RedisPresence p) => await p.CountOnlineAsync());

app.Run();
