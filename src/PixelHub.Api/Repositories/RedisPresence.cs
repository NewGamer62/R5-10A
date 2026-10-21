using StackExchange.Redis;

namespace PixelHub.Api.Repositories;

public class RedisPresence
{
    private const string Key = "enligne:joueurs";
    private const int TimeoutSeconds = 60;
    private readonly IDatabase _db;

    public RedisPresence(IConnectionMultiplexer redis) => _db = redis.GetDatabase();

    public async Task TouchAsync(string pseudo)
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        await _db.SortedSetAddAsync(Key, pseudo, now);
    }

    public async Task<long> CountOnlineAsync()
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        await _db.SortedSetRemoveRangeByScoreAsync(Key, 0, now - TimeoutSeconds);
        return await _db.SortedSetLengthAsync(Key);
    }
}
