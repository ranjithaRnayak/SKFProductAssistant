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

        // ============================================
        // Configuration Options (IOptions<T> pattern)
        // ============================================

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

        // ============================================
        // Infrastructure Services
        // ============================================

        // Attribute normalization lexicon
        services.AddSingleton<AttributeLexicon>();

        // Conditional Redis registration based on feature flag
        var featureFlags = configuration.GetSection(FeatureFlags.SectionName).Get<FeatureFlags>();

        if (featureFlags?.UseRedis == true)
        {
            // Redis connection
            services.AddSingleton<IConnectionMultiplexer>(sp =>
            {
                var redisOptions = sp.GetRequiredService<IOptions<RedisOptions>>().Value;
                return ConnectionMultiplexer.Connect(redisOptions.ConnectionString);
            });

            // Redis-backed repositories
            services.AddSingleton<ICacheRepository, RedisCacheRepository>();
            services.AddSingleton<IFeedbackRepository, RedisFeedbackRepository>();
            services.AddSingleton<IConversationStateRepository, RedisConversationStateRepository>();
        }
        else
        {
            // In-memory repositories (default)
            services.AddSingleton<ICacheRepository, InMemoryCacheRepository>();
            services.AddSingleton<IFeedbackRepository, InMemoryFeedbackRepository>();
            services.AddSingleton<IConversationStateRepository, InMemoryConversationStateRepository>();
        }

        // Datasheet repository (always JSON-based)
        services.AddSingleton<IDatasheetRepository, JsonDatasheetRepository>();

        // ============================================
        // Application Plugins (for SK function calling)
        // ============================================

        services.AddSingleton<DatasheetPlugin>();
        services.AddSingleton<CachePlugin>();
        services.AddSingleton<StatePlugin>();
        services.AddSingleton<FeedbackPlugin>();

        // ============================================
        // Application Services
        // ============================================

        services.AddSingleton<IntentClassifierService>();
        services.AddSingleton<ProductNormalizationService>();
        services.AddSingleton<ConversationStateManager>();
        services.AddSingleton<HallucinationGuard>();

        // ============================================
        // Agents
        // ============================================

        services.AddSingleton<QnaAgent>();
        services.AddSingleton<FeedbackAgent>();

        // ============================================
        // Orchestrator (main entry point)
        // ============================================

        services.AddSingleton<OrchestratorService>();
    })
    .Build();

host.Run();
