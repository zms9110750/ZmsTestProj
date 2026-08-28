public static class CliRoot
{
    private static Argument<string[]> Items { get; } = new("items");

    public static RootCommand Root { get; } = Init(new RootCommand("CLI application")
    {
        Items,
        HelloCommand.Cmd,
        RandCommand.Cmd,
    });

    private static RootCommand Init(RootCommand root)
    {
        root.SetAction(Execute);
        return root;
    }

    private static void Execute(ParseResult ctx)
    {
        foreach (var item in ctx.GetValue(Items) ?? [])
        {
            Console.WriteLine(item);
        }
    }
}
