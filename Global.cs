// Global usings
global using System.Net;
global using System.Reflection;

#if USE_POLLY
global using Polly;
global using Polly.Retry;
global using Polly.RateLimiting;
global using Polly.Telemetry;
global using System.Threading.RateLimiting;
#endif
#if USE_FUSIONCACHE
global using ZiggyCreatures.Caching.Fusion;
global using ZiggyCreatures.Caching.Fusion.Serialization.SystemTextJson;
global using ZiggyCreatures.Caching.Fusion.MicrosoftHybridCache;
global using NeoSmart.Caching.Sqlite;
#endif
#if USE_DI
global using Microsoft.Extensions.DependencyInjection;
#endif
#if (USE_DI && USE_FUSIONCACHE && USE_POLLY)
global using Microsoft.Extensions.Caching.Hybrid;
global using Microsoft.Extensions.Http.Resilience;
global using Axion.Extensions.Caching.Hybrid.Serialization.Http;
global using Axion.Extensions.Http.Resilience;
#endif
#if USE_LOG
global using Serilog;
global using Microsoft.Extensions.Logging;
global using Soenneker.HttpClients.LoggingHandler;
#endif

// Project-type usings
#if IS_CLI
global using System.CommandLine;
#endif
#if IS_GUI
global using Masa.Blazor;
global using Photino.Blazor;
#endif
#if IS_TEST
global using Xunit;
#endif

/// <summary>应用引导：Serilog 初始化、全局异常兜底、DI 装配。仅按编译常量条件编译。</summary>
public static class AppBootstrap
{
#if USE_LOG
    /// <summary>最先初始化 Serilog（Console + File）；任何阶段（含启动早期异常）的日志都能落盘。</summary>
    public static void InitLogging()
    {
        Log.Logger = new LoggerConfiguration()
            .WriteTo.Console()
            .WriteTo.File(Path.Combine(AppContext.BaseDirectory, "logs", "app.log"))
            .CreateLogger();
    }

    /// <summary>全局异常兜底：写日志后继续按默认传播，不静默吞掉。</summary>
    public static void AttachExceptionHandlers()
    {
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            try { Log.Fatal(e.ExceptionObject as Exception, "未处理的 AppDomain 异常"); }
            catch { }
            // 不抑制：进程按 .NET 默认行为终止
        };
        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            try { Log.Error(e.Exception, "未观察的任务异常"); }
            catch { }
            // 不调用 e.SetObserved()：异常保持默认处理，不静默吞掉
        };
    }
#endif

#if USE_DI && USE_FUSIONCACHE
    /// <summary>FusionCache 装配：Sqlite 分布式持久化 → HybridCache 桥接。遥测由 DI 自动注入 ILogger&lt;FusionCache&gt;（若开日志）。</summary>
    public static void ConfigureFusionCache(IServiceCollection services)
    {
        services.AddSqliteCache("cache.db")
            .AddFusionCache()
            .WithRegisteredDistributedCache()
            .WithSerializer(new FusionCacheSystemTextJsonSerializer())
            .AsHybridCache();
    }
#endif

#if USE_DI && USE_FUSIONCACHE && USE_POLLY
    /// <summary>
    /// Http + Polly 中间件链装配：key = 项目名（AddHttpClient 与 AddResilienceHandler 同名）。
    /// 中间件顺序：缓存（命中跳过后续所有策略）→ 限流重试 → 并发限流 → 429 重试。
    /// </summary>
    public static void ConfigureHttpClient(IServiceCollection services)
    {
        // key 与 UA 取自已加载（入口）程序集：GUI 调用 lib 时反射到 GUI
        var asm = Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly();
        var name = asm.GetName().Name ?? "MyApp";
        var version = asm.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
                      ?? asm.GetName().Version?.ToString()
                      ?? "0.0.0";
        var author = asm.GetCustomAttributes<AssemblyMetadataAttribute>()
                          .FirstOrDefault(static a => a.Key == "Authors")?.Value;
        var ua = $"{name} v{version} @ {author}";
        services.AddHttpClient(name, client => client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", ua))
#if USE_LOG
            // HTTP 请求/响应日志（Soenneker delegating handler）：选项可调（请求/响应体、脱敏头）
            .AddHttpMessageHandler(sp => new HttpClientLoggingHandler(
                sp.GetRequiredService<ILogger<HttpClientLoggingHandler>>(),
                new HttpClientLoggingOptions
                {
                    LogRequestHeaders = true,
                    LogResponseHeaders = true,
                }))
#endif
            .AddResilienceHandler(name, (pipeline, ctx) =>
            {
#if USE_LOG
                // Polly 遥测：策略事件经 ILoggerFactory（AddSerilog 提供）写进 Serilog
                pipeline.ConfigureTelemetry(ctx.ServiceProvider.GetRequiredService<ILoggerFactory>());
#endif
                // 缓存：命中直接返回，跳过后续所有策略
                pipeline.AddCaching(new HttpCachingStrategyOptions
                {
                    HybridCache = ctx.ServiceProvider.GetRequiredService<HybridCache>(),
                });

                // 共享限流器（并发 3/s，队列 600）：限流重试读取其队列数估算等待时间
                var limiter = new SlidingWindowRateLimiter(new SlidingWindowRateLimiterOptions
                {
                    PermitLimit = 3,
                    SegmentsPerWindow = 1,
                    Window = TimeSpan.FromSeconds(1),
                    QueueLimit = 600,
                });

                // 限流重试（外层）：本地队列满（RateLimiterRejectedException）→ 重试，等队列腾出
                pipeline.AddRetry(new RetryStrategyOptions<HttpResponseMessage>
                {
                    ShouldHandle = args => ValueTask.FromResult(args.Outcome.Exception is RateLimiterRejectedException),
                    MaxRetryAttempts = 5,
                    DelayGenerator = _ => new ValueTask<TimeSpan?>(TimeSpan.FromMilliseconds(((limiter.GetStatistics()?.CurrentQueuedCount ?? 0) * 333) + 300)),
                });

                // 并发限流：放行后的请求才可能被服务器限流
                pipeline.AddRateLimiter(limiter);

                // 429 重试（最内）：服务器限流 → 内部消化
                pipeline.AddRetry(new RetryStrategyOptions<HttpResponseMessage>
                {
                    ShouldHandle = args => ValueTask.FromResult(args.Outcome.Result?.StatusCode == HttpStatusCode.TooManyRequests),
                    MaxRetryAttempts = 3,
                    DelayGenerator = _ => new ValueTask<TimeSpan?>(TimeSpan.FromSeconds(1)),
                });
            });
    }
#endif
}
