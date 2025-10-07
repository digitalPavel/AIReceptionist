using Microsoft.AspNetCore.HttpOverrides;
using Demo1.Services;

namespace Demo1;
public class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        // Add services to the container.

        builder.Services.AddControllers();
        // Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
        builder.Services.AddOpenApi();

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
}
