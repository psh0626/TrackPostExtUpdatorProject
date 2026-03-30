// See https://aka.ms/new-console-template for more information

namespace TrackPostExtUpdator;

public static partial class Program
{
    public static async Task Main(string[] args)
    {
        Invisibler.MakeInvisible();
        const string USER_AGENT = "TrackPostUpdater";
        const string TOKEN =
            "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";
        using var githubClient = new GithubService(USER_AGENT, TOKEN);

        try
        {
            Console.BackgroundColor = ConsoleColor.White;
            Console.ForegroundColor = ConsoleColor.Black;
            Console.Clear();

            if (args.Length == 3)
            {
                switch (args[0])
                {
                    case "--updator":
                        var local_pid = int.Parse(args[1]);
                        var local_path = args[2];
                        var temp_path = Environment.ProcessPath!;
                        await Updator.OverwriteAndWait(local_pid, temp_path, local_path);
                        break;
                    case "--updated":
                        var temp_pid = int.Parse(args[1]);
                        var temp_file_path = args[2];
                        await Updator.DisposeTempFile(temp_pid, temp_file_path);
                        break;
                }
            }
            else
            {
                await Updator.UpdateUpdator(githubClient);
            }

            var currnet_directory = Path.GetDirectoryName(Environment.ProcessPath);
            Directory.SetCurrentDirectory(currnet_directory!);

            Invisibler.MakeVisible();
            Console.WriteLine("업데이트를 강제하려면 SHIFT키를 1초간 누르세요.");
            bool shouldForce = await ShiftDetector.CheckShiftHeld(2000, 800);
            if (shouldForce)
                Console.WriteLine("\n--업데이트를 강제합니다.\n\n");
            await Updator.UpdateTrackPost(githubClient, shouldForce);
            //Console.ReadKey(true);
        }
        catch (Exception e)
        {
            if (args.Length > 0 && args[0] == "--updator")
                Environment.Exit(1);

            while (true)
            {
                for (var i = 0; i < 10; i++)
                {
                    Console.WriteLine("");
                }
                Console.WriteLine("에러 발생: 일단 다시 시도해보세요.\n\n");
                Console.WriteLine(e);
                Console.WriteLine("다시 시도 하시겠습니까? ");

                string[] noWords = ["n", "x", "ㄴ"];
                if (noWords.Contains(Console.ReadLine() ?? "".ToLower()))
                    break;

                try
                {
                    await Updator.UpdateTrackPost(githubClient, true);
                    break;
                }
                catch (Exception ex)
                {
                    Console.WriteLine("에러 발생: 일단 다시 시도해보세요.\n\n");
                    Console.WriteLine(ex);
                    Console.WriteLine("다시 시도 하시겠습니까? ");
                    if (noWords.Contains(Console.ReadLine() ?? "".ToLower()))
                        break;
                    continue;
                }
            }
        }
    }
}
