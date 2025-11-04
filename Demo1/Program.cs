using Microsoft.AspNetCore.HttpOverrides;
using Demo1.Services;
using Demo1.Services.Brain;

namespace Demo1;
public class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        // Add services to the container.

        builder.Services.AddControllers();

        builder.Services.AddOpenApi();

        // Configure Azure Speech options from configuration(Added region and key to secrets)
        builder.Services.Configure<AzureSpeechOptions>(
            builder.Configuration.GetSection("AzureSpeech"));

        // Register state store for dialog memory
        builder.Services.AddSingleton<IStateStore, InMemoryStateStore>();

        // Register NLU service for intent recognition
        builder.Services.AddSingleton<INluService, RulesNluService>();

        // Alternatively, use an LLM-based NLU service
        //builder.Services.AddSingleton<ILlmNluService>

        // Registrer decision policy for dialog management
        builder.Services.AddSingleton<IDecisionPolicy, DefaultDecisionPolicy>();


        var app = builder.Build();

        // Configure Forwarded Headers Middleware to process X-Forwarded-* headers
        app.UseForwardedHeaders(new ForwardedHeadersOptions
        {
            ForwardedHeaders =   ForwardedHeaders.XForwardedFor 
                               | ForwardedHeaders.XForwardedProto
                               | ForwardedHeaders.XForwardedHost
        });

        // Configure the HTTP request pipeline.
        if (app.Environment.IsDevelopment())
        {
            app.MapOpenApi();
        }

        app.UseWebSockets();

        app.Map("/ws/stream", MediaStreamHandler.HandleAsync);

        app.UseHttpsRedirection();

        app.UseAuthorization();

        app.MapControllers();

        app.Run();
    }

    // Options for Azure Speech service(Where will put Key and Region)
    public record AzureSpeechOptions
    {
        public string Region { get; init; } = String.Empty;
        public string Key { get; init; } = String.Empty;
    }
}