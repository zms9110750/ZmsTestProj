// Serilog 最先初始化 + 全局异常兜底（写日志后继续报错，不静默吞掉）
#if USE_LOG
AppBootstrap.InitLogging();
AppBootstrap.AttachExceptionHandlers();
#endif

// DI 装配：FusionCache（开 FusionCache 时）；Http + Polly 中间件（开 FusionCache + Polly 时）
#if USE_DI && USE_FUSIONCACHE
var services = new ServiceCollection();
AppBootstrap.ConfigureFusionCache(services);
#endif

#if USE_DI && USE_FUSIONCACHE && USE_POLLY
AppBootstrap.ConfigureHttpClient(services);
#endif

// Serilog 接入 DI 容器：ConfigureTelemetry 的 ILoggerFactory 由此获得 Serilog provider（Polly 遥测真正落盘）
#if USE_LOG && USE_DI && USE_FUSIONCACHE
services.AddSerilog();
#endif

// 示例命令（hello / rand / rand janken）：文件级开关（Tree/Options/Shared 是否生成）
return CliRoot.Root.Parse(args).Invoke();
