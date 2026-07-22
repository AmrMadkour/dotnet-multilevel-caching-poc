using CachingPoc.Api.Data;
using CachingPoc.Api.Models;
using Microsoft.Extensions.Caching.Memory;
using StackExchange.Redis;
using System.Text.Json;

namespace CachingPoc.Api.Endpoints;

public static class ProductEndpoints
{
    private static readonly TimeSpan MemoryTtl = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan RedisTtl = TimeSpan.FromMinutes(2);

    public static void MapProductEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/products/{id}", GetProduct)
            .WithName("GetProduct");

        app.MapPut("/products/{id}", UpdateProductPrice)
            .WithName("UpdateProductPrice");

        app.MapDelete("/cache/{id}", ClearProductCache)
            .WithName("ClearProductCache");
    }

    private static async Task<IResult> GetProduct(int id, AppDbContext db, IMemoryCache cache, IConnectionMultiplexer redis)
    {
        var cacheKey = $"product:{id}";
        if (cache.TryGetValue(cacheKey, out Product? cachedProduct))
        {
            return Results.Ok(new { cachedProduct!.Id, cachedProduct.Name, cachedProduct.Price, source = "memory" });
        }

        var redisDb = redis.GetDatabase();
        var redisValue = await redisDb.StringGetAsync(cacheKey);

        if (redisValue.HasValue)
        {
            var redisProduct = JsonSerializer.Deserialize<Product>((string)redisValue!);
            cache.Set(cacheKey, redisProduct, MemoryTtl);
            return Results.Ok(new { redisProduct!.Id, redisProduct.Name, redisProduct.Price, source = "redis" });
        }

        await Task.Delay(1000);

        var product = await db.Products.FindAsync(id);
        if (product is null)
        {
            return Results.NotFound();
        }

        cache.Set(cacheKey, product, MemoryTtl);
        await redisDb.StringSetAsync(cacheKey, JsonSerializer.Serialize(product), RedisTtl);
        return Results.Ok(new { product.Id, product.Name, product.Price, source = "db" });
    }

    private static async Task<IResult> UpdateProductPrice(int id, UpdateProductPriceRequest request, AppDbContext db, IMemoryCache cache, IConnectionMultiplexer redis)
    {
        var product = await db.Products.FindAsync(id);
        if (product is null)
        {
            return Results.NotFound();
        }

        product.Price = request.Price;
        await db.SaveChangesAsync();

        var cacheKey = $"product:{id}";
        var redisDb = redis.GetDatabase();
        cache.Set(cacheKey, product, MemoryTtl);
        await redisDb.StringSetAsync(cacheKey, JsonSerializer.Serialize(product), RedisTtl);

        return Results.Ok(new { product.Id, product.Name, product.Price });
    }

    private static async Task<IResult> ClearProductCache(int id, IMemoryCache cache, IConnectionMultiplexer redis)
    {
        var cacheKey = $"product:{id}";
        var redisDb = redis.GetDatabase();

        var existedInMemory = cache.TryGetValue(cacheKey, out _);
        var existedInRedis = await redisDb.KeyExistsAsync(cacheKey);

        if (!existedInMemory && !existedInRedis)
        {
            return Results.NotFound();
        }

        cache.Remove(cacheKey);
        await redisDb.KeyDeleteAsync(cacheKey);
        return Results.NoContent();
    }
}

public record UpdateProductPriceRequest(decimal Price);
