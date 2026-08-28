public static class HelloCommand
{
    public static Argument<string> NameArg { get; } = new("name") { Description = "Your name" };

    public static Command Cmd { get; } = Init(new Command("hello", "输出问候语")
    {
        NameArg,
    });

    private static Command Init(Command cmd)
    {
        cmd.SetAction(Execute);
        return cmd;
    }

    private static void Execute(ParseResult ctx)
    {
        Console.WriteLine($"hello {ctx.GetValue(NameArg)}");
    }
}
