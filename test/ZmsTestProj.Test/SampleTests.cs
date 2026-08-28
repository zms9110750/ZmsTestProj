using System.Diagnostics;
using Xunit;

// v3 新增：把 Console.WriteLine / Debug / Trace 输出重定向到测试输出（等价于经 ITestOutputHelper 输出）
[assembly: CaptureConsole]
[assembly: CaptureTrace]

/// <summary>测试夹具：在本测试类的所有测试方法间共享的状态/初始化入口。</summary>
public sealed class SampleFixture
{
    /// <summary>记录本夹具被测试方法使用的次数。</summary>
    public int Uses { get; set; }
}

/// <summary>
/// 示例测试（xUnit v3）。
///
/// <para>如何写测试：</para>
/// <list type="bullet">
/// <item>单个测试：用 <see cref="FactAttribute"/> 标记无参方法；</item>
/// <item>把输出送到测试输出：构造函数注入 <see cref="ITestOutputHelper"/> 调 <c>output.WriteLine</c>；
/// 或加 <c>[assembly: CaptureConsole]</c> / <c>[assembly: CaptureTrace]</c> 把
/// <c>Console.WriteLine</c> / <c>Debug</c> / <c>Trace</c> 一并重定向到测试输出；</item>
/// <item>参数化测试：<see cref="TheoryAttribute"/> + <see cref="InlineDataAttribute"/>（每行一组参数）；
/// 需要更强的参数化（每行跳过/显示名/元数据）时用 <c>MemberData</c> + <see cref="TheoryDataRow{T1,T2,T3}"/>，
/// 组合多组数据用 <c>MatrixTheoryData</c>；</item>
/// <item>多个测试方法共用同一个初始化/状态：实现 <see cref="IClassFixture{TFixture}"/>，构造函数注入 fixture
/// 实例（fixture 在本测试类的所有方法间共享，类结束后由框架自动释放；fixture 定义为独立顶层类）；</item>
/// <item>取消令牌：测试期间可通过 <c>TestContext.Current.CancellationToken</c> 感知超时/用户取消，
/// 传给被测代码或异步等待（如 <c>await Task.Delay(ms, token)</c>）；</item>
/// <item>动态跳过：<c>Assert.SkipWhen(condition, reason)</c> 在运行时按环境跳过
/// （如 <c>!OperatingSystem.IsWindows()</c>、CI 环境变量）——比 <c>Skip = "..."</c> 的静态跳过更灵活；</item>
/// <item>显式测试：<c>[Fact(Explicit = true)]</c> 默认不运行，需在运行器/IDE 中显式请求才执行
/// （适合耗时或需特殊环境的用例）；</item>
/// <item>网络资源：放入 <c>test/Resources/</c>（经 Directory.Build.props 外引用复制到测试输出目录），
/// 避免测试直接访问网络。</item>
/// </list>
///
/// 运行方式：<c>dotnet test</c>（或 IDE 的测试资源管理器）。
/// </summary>
public class SampleTests(ITestOutputHelper output, SampleFixture fixture) : IClassFixture<SampleFixture>
{
    /// <summary>参数化数据：每行可携带跳过/显示名等元数据（v3 的 TheoryDataRow）。</summary>
    public static TheoryDataRow<int, int, int>[] SumData =>
    [
        new TheoryDataRow<int, int, int>(1, 2, 3),
        new TheoryDataRow<int, int, int>(2, 3, 5).WithSkip("演示：跳过这一行数据"),
        new TheoryDataRow<int, int, int>(10, 20, 30) { TestDisplayName = "10 + 20 = 30" },
    ];

    /// <summary>Hello World：把输出写到测试输出面板。</summary>
    [Fact]
    public void HelloWorld_ShouldWriteToTestOutput()
    {
        fixture.Uses++;
        output.WriteLine($"Hello from xUnit v3! (fixture used {fixture.Uses} time(s))");
        Assert.True(true);
    }

    /// <summary>参数化测试（InlineData）：同一方法多组参数。</summary>
    [Theory]
    [InlineData(1, 2, 3)]
    [InlineData(2, 3, 5)]
    [InlineData(10, 20, 30)]
    public void Add_ShouldReturnSum(int a, int b, int expected)
    {
        output.WriteLine($"{a} + {b} = {expected}");
        Assert.Equal(expected, a + b);
    }

    /// <summary>更强大的参数化（TheoryDataRow + MemberData）：支持每行跳过/显示名/超时。</summary>
    [Theory]
    [MemberData(nameof(SumData))]
    public void Add_WithTheoryDataRow(int a, int b, int expected)
    {
        output.WriteLine($"{a} + {b} = {expected}");
        Assert.Equal(expected, a + b);
    }

    /// <summary>控制台/调试输出重定向：被 [assembly: CaptureConsole]/[assembly: CaptureTrace] 捕获。</summary>
    [Fact]
    public void ConsoleOutput_ShouldBeCaptured()
    {
        Console.WriteLine("Console.WriteLine 的输出（被 CaptureConsole 重定向到测试输出）");
        Debug.WriteLine("Debug 的输出（被 CaptureTrace 重定向，仅 Debug 构建）");
        Trace.WriteLine("Trace 的输出（被 CaptureTrace 重定向）");
        Assert.True(true);
    }

    /// <summary>取消令牌：测试超时/用户取消时可感知并传给被测代码。</summary>
    [Fact]
    public async Task Cancellation_ShouldBeObservable()
    {
        var token = TestContext.Current.CancellationToken;
        await Task.Delay(50, token); // 超时/取消时抛 TaskCanceledException
        Assert.False(token.IsCancellationRequested);
    }

    /// <summary>动态跳过：非 Windows 环境跳过（OS 场景）。</summary>
    [Fact]
    public void SkipWhen_NotWindows()
    {
        Assert.SkipWhen(!OperatingSystem.IsWindows(), "本测试仅适用于 Windows");
        Assert.True(OperatingSystem.IsWindows());
    }

    /// <summary>显式测试：默认不运行，需在运行器/IDE 中显式请求。</summary>
    [Fact(Explicit = true)]
    public void ExplicitTest_OnlyRunsWhenRequested()
    {
        output.WriteLine("显式测试：默认不运行（报告为 not run），显式请求时才执行");
        Assert.True(true);
    }
}
