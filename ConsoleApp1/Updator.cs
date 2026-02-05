using System.Diagnostics;
using System.IO.Compression;

namespace TrackPostExtUpdator;

internal static class Updator
{
    const string OWNER = "shawnpark9494";
    const string REPO_NAME = "TrackPostExtZip";
    const string UPDATOR_REPO_NAME = "TrackPostExtUpdator";

    const string LOCAL_FOLDER_NAME = "dist";

    const string UPDATE_LOGS_URL = "https://github.com/shawnpark9494/TrackPostExtZip/commits/main/";

    public static async Task DisposeTempFile(int pid, string path)
    {
        Process.GetProcessById(pid).Kill();
        while (IsFileInUse(path))
        {
            await Task.Delay(100);
        }
        File.Delete(path);
        Console.Clear();
    }

    public static async Task OverwriteAndWait(int pid, string source, string destination)
    {
        Invisibler.MakeInvisible();
        Process.GetProcessById(pid).Kill();
        File.SetLastWriteTime(source, DateTime.Now);

        while (IsFileInUse(destination))
        {
            await Task.Delay(100);
        }
        File.Copy(source, destination, true);
        Process.Start(
            new ProcessStartInfo
            {
                FileName = destination,
                Arguments = $"--updated {Environment.ProcessId} \"{source}\"",
                UseShellExecute = true
            }
        );
        Console.ReadLine();
    }

    public static async Task UpdateUpdator(GithubService client)
    {
        var current_file_path = Environment.ProcessPath;
        var local_last_updated = new DateTimeOffset(File.GetLastWriteTime(current_file_path!));

        var repo = await client.GetRepositoryAsync(OWNER, UPDATOR_REPO_NAME);
        bool IsRemoteNewer = repo.UpdatedAt > local_last_updated;

        if (IsRemoteNewer)
        {
            Invisibler.MakeVisible();
            Console.WriteLine($"TrackPost 확장프로그램 업데이터에 패치가 있습니다.");
            var temp_folder = Path.Combine(Path.GetTempPath(), "IMIC-" + Path.GetRandomFileName());
            var file_name = Path.GetFileNameWithoutExtension(current_file_path);
            var temp_path = Path.Combine(temp_folder, file_name + ".zip");
            var zip_path = await DownloadZipFile(client, repo, temp_path);

            if (string.IsNullOrEmpty(zip_path))
                throw new FileNotFoundException("Download failed.");

            ZipFile.ExtractToDirectory(zip_path, temp_folder);
            var extracted_file_path = Path.Combine(temp_folder, file_name + ".exe");
            Process.Start(
                new ProcessStartInfo
                {
                    FileName = extracted_file_path,
                    Arguments = $"--updator {Environment.ProcessId} \"{current_file_path}\"",
                    UseShellExecute = true,
                    CreateNoWindow = false,
                }
            );
            Console.ReadLine();
        }
    }

    public static async Task UpdateTrackPost(GithubService client, bool forceUpdate = false)
    {
        Console.WriteLine($"TrackPost 확장프로그램 업데이트를 확인합니다.");

        var local_folder = FindLocalFolder(LOCAL_FOLDER_NAME);
        if (local_folder is null)
            return;

        var local_last_updated = new DateTimeOffset(Directory.GetLastWriteTime(local_folder));
        Console.WriteLine($"로컬 TrackPost 마지막 업데이트: {local_last_updated.LocalDateTime}");

        var repo = await client.GetRepositoryAsync(OWNER, REPO_NAME);
        if (repo is null)
            return;
        Console.WriteLine($"공식 TrackPost 마지막 업데이트: {repo.UpdatedAt.LocalDateTime}");

        const string local_new_string = "현재 최신 버전이 설치 되어 있습니다.";
        const string remote_new_string = "현재 업데이트가 있습니다.";
        bool IsRemoteNewer = repo.UpdatedAt > local_last_updated;
        Console.WriteLine($"{(IsRemoteNewer ? remote_new_string : local_new_string)}");

        if (IsRemoteNewer || forceUpdate)
        {
            Invisibler.MakeVisible();
            var temp_folder = Path.Combine(Path.GetTempPath(), "IMIC-" + Path.GetRandomFileName());
            var temp_path = Path.Combine(temp_folder, Path.GetFileName(local_folder) + ".zip");
            var zip_path = await DownloadZipFile(client, repo, temp_path);

            if (string.IsNullOrEmpty(zip_path))
                throw new FileNotFoundException("Download failed.");

            DeleteAllFilesIn(local_folder);
            Console.WriteLine("\n업데이트 파일 압축 푸는 중..");
            ZipFile.ExtractToDirectory(zip_path, local_folder);
            Console.WriteLine("\n업데이트 파일 압축 해제 완료!");
            DeleteAllFilesIn(Path.GetDirectoryName(temp_path)!);
            Directory.SetLastWriteTime(local_folder, DateTime.Now);
            Invisibler.MakeInvisible();
            //throw new Exception("TEST EXCEPTION FOR DEBUGGING PURPOSES.");
            OpenNewTab(UPDATE_LOGS_URL);
            await Task.Delay(10000);
            //Environment.Exit(0);
        }

        var rate_limit = await client.GetRateLimitsAsync();
        Console.WriteLine(
            $"\ngithub rate limit: {rate_limit.Remaining} / {rate_limit.Limit} requests left."
        );
    }

    static string? FindLocalFolder(string folder_name)
    {
        var local_folder = FindTrackPostFolder(folder_name);
        if (local_folder is null)
        {
            Console.WriteLine($"\n프로그램이 저장된 폴더({folder_name})를 찾을 수 없습니다.");
            Console.WriteLine("dist 폴더가 설치된 곳으로 옮겨 주세요.");
            Console.ReadKey();
            return null;
        }
        return local_folder;
    }

    static bool IsFileInUse(string filePath)
    {
        try
        {
            // Try to open the file with read-write access
            using var stream = new FileStream(
                filePath,
                System.IO.FileMode.Open,
                FileAccess.ReadWrite
            );
            // If successful, the file is not in use.
            return false;
        }
        catch (IOException)
        {
            // If an IOException is caught, the file is in use.
            return true;
        }
        catch (Exception ex)
        {
            // Handle other potential exceptions (e.g., file not found).
            Console.WriteLine($"An error occurred: {ex.Message}");
            return true;
        }
    }

    internal static void OpenNewTab(string url)
    {
        var browser_ars = BrowserChecker.GetDefaultBrowser();
        var browser_path = BrowserChecker.ExtractExecutablePath(browser_ars);
        Process.Start(
            new ProcessStartInfo
            {
                FileName = browser_path,
                Arguments = $"--new-tab {url}",
                UseShellExecute = true
            }
        );
    }

    static string? FindTrackPostFolder(string candidate)
    {
        var current_folder = Directory.GetCurrentDirectory();
        var target_folder = Directory
            .EnumerateDirectories(current_folder)
            .FirstOrDefault((path) => (Path.GetFileName(path) == candidate));

        return target_folder;
    }

    static async Task<string> DownloadZipFile(
        GithubService client,
        Repository repository,
        string path
    )
    {
        var contents = await client.GetAllContentsAsync(
            repository.Owner.Login,
            repository.Name,
            Path.GetFileName(path)!
        );

        if (contents.Count < 1)
            throw new FileNotFoundException("Github에서 dist.zip을 찾을 수 없습니다.");

        var download_url = contents[0].DownloadUrl;

        //var pagination = new ApiOptions
        //{
        //  PageCount = 1,
        //  PageSize = 1,
        //  StartPage = 1
        //};
        //var patch_note = (await client.Repository.Commit.GetAll(repository.Id, pagination))[0].Commit.Message;

        using var http_client = new HttpClient();

        Console.WriteLine("최신 버전의 업데이트 파일을 다운로드를 시작합니다.\n");

        using var response = await http_client.GetAsync(
            download_url,
            HttpCompletionOption.ResponseHeadersRead
        );
        response.EnsureSuccessStatusCode();

        var total_bytes = response.Content.Headers.ContentLength ?? -1L;
        var can_report_progress = total_bytes != -1;

        if (!Path.Exists(path))
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        using var remote_stream = await response.Content.ReadAsStreamAsync();
        using var local_stream = new FileStream(
            path,
            System.IO.FileMode.Create,
            FileAccess.Write,
            FileShare.None,
            8192,
            true
        );

        var buffer = new byte[8192];
        long total_bytes_read = 0;
        int bytes_read;
        double lastProgress = 0;

        while ((bytes_read = await remote_stream.ReadAsync(buffer)) > 0)
        {
            await local_stream.WriteAsync(buffer.AsMemory(0, bytes_read));
            total_bytes_read += bytes_read;

            if (can_report_progress)
            {
                var progress = ((double)total_bytes_read / total_bytes * 100);
                if (lastProgress > 0 && Console.CursorTop > 0)
                {
                    Console.SetCursorPosition(0, Console.CursorTop - 1);
                    Console.Write(new string(' ', Console.WindowWidth));
                    Console.WriteLine($"다운로드 중: {progress:F2}%");
                    Console.SetCursorPosition(0, Console.CursorTop - 1);
                }
                lastProgress = progress;
                if (lastProgress >= 100)
                    Console.WriteLine("");
            }
        }

        Console.WriteLine("\n다운로드 완료!");

        return path;
    }

    static void DeleteAllFilesIn(string folder)
    {
        if (!Directory.Exists(folder))
            throw new DirectoryNotFoundException($"폴더 '{folder}' 찾을 수 없음.");

        // Get all file system entries and count them for progress tracking
        var entries = Directory.EnumerateFileSystemEntries(folder).ToList();
        int totalEntries = entries.Count;
        int deletedEntries = 0;
        double lastProgress = -1;

        var folder_info = folder.Split("\\");
        Console.WriteLine(
            $"\n{folder_info[^2]}\\{folder_info[^1]}\\ 폴더 내부를 삭제합니다. ({totalEntries}개 파일)\n"
        );

        foreach (var item in entries)
        {
            try
            {
                if (File.Exists(item))
                {
                    File.Delete(item);
                }
                else if (Directory.Exists(item))
                {
                    Directory.Delete(item, true);
                }

                // Increment the count of deleted entries
                deletedEntries++;

                // Calculate progress
                double progress = ((double)deletedEntries / totalEntries) * 100;

                Console.SetCursorPosition(0, Console.CursorTop - 1);
                Console.Write(new string(' ', Console.WindowWidth));
                Console.WriteLine($"삭제 중: {progress:F2}%");
                Console.SetCursorPosition(0, Console.CursorTop - 1);

                lastProgress = progress;
                // Ensure the final message appears on a new line after completion
                if (lastProgress >= 100)
                {
                    Console.WriteLine("");
                }
            }
            catch (Exception ex)
            {
                throw new IOException($"파일 '{item}'을(를) 삭제할 수 없음: {ex.Message}");
            }
        }

        Console.WriteLine("\n삭제 완료!");
    }
}
