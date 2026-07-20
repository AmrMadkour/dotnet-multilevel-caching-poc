using CachingPoc.Api.Data;
using Microsoft.Extensions.Caching.Memory;

namespace CachingPoc.Api.Endpoints;

public static class ProductEndpoints
{
    public static void MapProductEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/products/{id}", async (int id, AppDbContext db, IMemoryCache cache) =>
        {
            await Task.Delay(500);

            var product = await db.Products.FindAsync(id);

            return product is null
                ? Results.NotFound()
                : Results.Ok(new { product.Id, product.Name, product.Price, source = "db" });
        })
        .WithName("GetProduct");
    }
}
