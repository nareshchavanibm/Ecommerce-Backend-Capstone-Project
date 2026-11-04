using System.Net;

namespace Services.IntegrationTests;

public class HealthEndpointTests :
    IClassFixture<UserApiFactory>,
    IClassFixture<ProductApiFactory>,
    IClassFixture<OrderApiFactory>,
    IClassFixture<PaymentApiFactory>
{
    private readonly Dictionary<string, HttpClient> _clients;

    public HealthEndpointTests(
        UserApiFactory user,
        ProductApiFactory product,
        OrderApiFactory order,
        PaymentApiFactory payment)
    {
        _clients = new()
        {
            ["user"] = user.CreateClient(),
            ["product"] = product.CreateClient(),
            ["order"] = order.CreateClient(),
            ["payment"] = payment.CreateClient()
        };
    }

    public static TheoryData<string, string> Endpoints()
    {
        var data = new TheoryData<string, string>();

        foreach (var service in new[] { "user", "product", "order", "payment" })
        {
            data.Add(service, "/health/live");
            data.Add(service, "/health/ready");
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(Endpoints))]
    public async Task Health_IsPublicAndHealthy(string service, string path)
    {
        // No token: orchestrators probe these without credentials.
        var response = await _clients[service].GetAsync(path);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Healthy", await response.Content.ReadAsStringAsync());
    }
}
