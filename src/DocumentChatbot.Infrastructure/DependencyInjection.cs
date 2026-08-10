using DocumentChatbot.Core.Application.Abstractions;
using DocumentChatbot.Infrastructure.Persistence;
using DocumentChatbot.Infrastructure.Rag;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace DocumentChatbot.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddSingleton<IChatSessionRepository, InMemoryChatSessionRepository>();
        services.Configure<RagServiceOptions>(configuration.GetSection(RagServiceOptions.SectionName));
        services.AddHttpClient<IRagService, HttpRagService>((serviceProvider, client) =>
        {
            var options = serviceProvider
                .GetRequiredService<IOptions<RagServiceOptions>>()
                .Value;

            client.BaseAddress = new Uri(options.BaseUrl);
            client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
        });

        return services;
    }
}
