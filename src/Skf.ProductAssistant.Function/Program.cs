using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Skf.ProductAssistant.Application.Agents;
using Skf.ProductAssistant.Application.Guards;
using Skf.ProductAssistant.Application.Orchestration;
using Skf.ProductAssistant.Application.Plugins;
using Skf.ProductAssistant.Application.Services;
using Skf.ProductAssistant.Domain.Interfaces;
using Skf.ProductAssistant.Infrastructure.Configuration;
using Skf.ProductAssistant.Infrastructure.Normalization;
using Skf.ProductAssistant.Infrastructure.Repositories;
using StackExchange.Redis;

var host = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults()
    .ConfigureAppConfiguration((context, config) =>
    {
        config
            .SetBasePath(context.HostingEnvironment.ContentRootPath)
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .AddJsonFile($"appsettings.{context.HostingEnvironment.EnvironmentName}.json", optional: true)
            .AddEnvironmentVariables();
    })
    .ConfigureServices((context, services) =>
    {
        var configuration = context.Configuration;

        services.AddOptions<AzureOpenAIOptions>()
            .Bind(configuration.GetSection(AzureOpenAIOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddOptions<RedisOptions>()
            .Bind(configuration.GetSection(RedisOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddOptions<DatasheetOptions>()
            .Bind(configuration.GetSection(DatasheetOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddOptions<FeatureFlags>()
            .Bind(configuration.GetSection(FeatureFlags.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddOptions<PromptOptions>()
            .Bind(configuration.GetSection(PromptOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddSingleton<AttributeLexicon>();

        var featureFlags = configuration.GetSection(FeatureFlags.SectionName).Get<FeatureFlags>();

        if (featureFlags?.UseRedis == true)
        {
            services.AddSingleton<IConnectionMultiplexer>(sp =>
            {
                var redisOptions = sp.GetRequiredService<IOptions<RedisOptions>>().Value;
                return ConnectionMultiplexer.Connect(redisOptions.ConnectionString);
            });

            services.AddSingleton<ICacheRepository, RedisCacheRepository>();
            services.AddSingleton<IFeedbackRepository, RedisFeedbackRepository>();
            services.AddSingleton<IConversationStateRepository, RedisConversationStateRepository>();
        }
        else
        {
            services.AddSingleton<ICacheRepository, InMemoryCacheRepository>();
            services.AddSingleton<IFeedbackRepository, InMemoryFeedbackRepository>();
            services.AddSingleton<IConversationStateRepository, InMemoryConversationStateRepository>();
        }

        services.AddSingleton<IDatasheetRepository, JsonDatasheetRepository>();

        services.AddSingleton<DatasheetPlugin>();
        services.AddSingleton<CachePlugin>();
        services.AddSingleton<StatePlugin>();
        services.AddSingleton<FeedbackPlugin>();

        services.AddSingleton<IntentClassifierService>();
        services.AddSingleton<ProductNormalizationService>();
        services.AddSingleton<ConversationStateManager>();
        services.AddSingleton<HallucinationGuard>();

        services.AddSingleton<QnaAgent>();
        services.AddSingleton<FeedbackAgent>();

        services.AddSingleton<OrchestratorService>();
    })
    .Build();

host.Run();
