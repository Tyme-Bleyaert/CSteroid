using CSteroid.AutoMediator;
using Microsoft.Extensions.DependencyInjection;
using Migration.After;

var services = new ServiceCollection();
services.AddScoped<SliceState>();
services.AddScoped<OrderWorkflow>();
services.AddAutoMediator();

using var provider = services.BuildServiceProvider(
    new ServiceProviderOptions { ValidateScopes = true });
using var scope = provider.CreateScope();
var workflow = scope.ServiceProvider.GetRequiredService<OrderWorkflow>();

var receipt = await workflow.Create("SKU-123", CancellationToken.None);
await workflow.Record(receipt.OrderId, CancellationToken.None);
await workflow.Publish(receipt.OrderId, CancellationToken.None);

Console.WriteLine($"Created order {receipt.OrderId} for {receipt.Sku}");
Console.WriteLine("Calls:");
foreach (var call in workflow.State.Calls)
    Console.WriteLine($"- {call}");
