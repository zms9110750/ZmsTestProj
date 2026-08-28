public static class RandCommand
{
    public static Option<int> MinOpt { get; } = new("--min", "Minimum value") { DefaultValueFactory = _ => 0 };
    public static Option<int> MaxOpt { get; } = new("--max", "Maximum value") { DefaultValueFactory = _ => 100 };
    public static Option<int> CountOpt { get; } = new Option<int>("--count", "Count of numbers (max 100)")
    {
        DefaultValueFactory = _ => 1,
        Validators =
        {
            result =>
            {
                if (result.GetValueOrDefault<int>() > 100)
                    result.AddError("--count cannot exceed 100");
            }
        }
    };

    public static Command Cmd { get; } = Init(new Command("rand", "生成随机数")
    {
        MinOpt,
        MaxOpt,
        CountOpt,
        JankenCommand.Cmd,
    });

    private static Command Init(Command cmd)
    {
        cmd.SetAction(Execute);
        return cmd;
    }

    private static void Execute(ParseResult ctx)
    {
        var min = ctx.GetValue(MinOpt);
        var max = ctx.GetValue(MaxOpt);
        var count = ctx.GetValue(CountOpt);
        var rng = Random.Shared;

        for (int i = 0; i < count; i++)
        {
            Console.WriteLine(rng.Next(min, max + 1));
        }
    }
}
