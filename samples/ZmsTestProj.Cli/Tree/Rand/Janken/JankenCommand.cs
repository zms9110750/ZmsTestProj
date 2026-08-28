using ZmsTestProj.Cli.Options;

public static class JankenCommand
{
    private static readonly Random _rng = Random.Shared;

    public static Option<Janken> HandOpt { get; } = new("--hand", "Your hand") { DefaultValueFactory = _ => Janken.Rock };

    public static Command Cmd { get; } = Init(new Command("janken", "Rock-Paper-Scissors vs computer")
    {
        HandOpt,
    });

    private static Command Init(Command cmd)
    {
        cmd.SetAction(Execute);
        return cmd;
    }

    private static void Execute(ParseResult ctx)
    {
        var player = ctx.GetValue(HandOpt);
        var cpu = (Janken)_rng.Next(3);
        var playerIdx = (int)player;
        var cpuIdx = (int)cpu;
        string[] emoji = ["👊", "✂️", "✋"];

        Console.WriteLine($"  You: {emoji[playerIdx]} {player}");
        Console.WriteLine($"  CPU: {emoji[cpuIdx]} {cpu}");

        if (playerIdx == cpuIdx)
        {
            Console.WriteLine("  Result: Draw 😐");
        }
        else if ((playerIdx + 1) % 3 == cpuIdx)
        {
            Console.WriteLine("  Result: You win 🎉");
        }
        else
        {
            Console.WriteLine("  Result: You lose 😞");
        }
    }
}
