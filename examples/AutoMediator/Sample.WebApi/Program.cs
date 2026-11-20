using CSteroid.AutoMediator;
using Sample.Application;
using Sample.WebApi;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddScoped<RequestTrace>();
builder.Services.AddScoped<SampleWorkflow>();
builder.Services.AddAutoMediator();

var app = builder.Build();

app.MapGet("/ping", async (SampleWorkflow workflow, CancellationToken cancellationToken) =>
{
    var response = await workflow.Ping(cancellationToken);
    return Results.Ok(new { response, trace = workflow.Trace.Entries });
});

app.MapPost("/record", async (SampleWorkflow workflow, CancellationToken cancellationToken) =>
{
    await workflow.Record(cancellationToken);
    return Results.Ok(new { trace = workflow.Trace.Entries });
});

app.MapPost("/publish", async (SampleWorkflow workflow, CancellationToken cancellationToken) =>
{
    await workflow.Publish(cancellationToken);
    return Results.Ok(new { trace = workflow.Trace.Entries });
});

app.Run();
