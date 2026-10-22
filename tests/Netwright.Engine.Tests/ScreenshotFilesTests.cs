using System.Diagnostics;
using Netwright.Engine.Capture;

namespace Netwright.Engine.Tests;

public sealed class ScreenshotFilesTests : IDisposable
{
    private readonly string _directory = Directory.CreateTempSubdirectory("netwright-shots-").FullName;
    private readonly string _outside = Directory.CreateTempSubdirectory("netwright-outside-").FullName;

    public void Dispose()
    {
        Directory.Delete(_directory, recursive: true);
        Directory.Delete(_outside, recursive: true);
    }

    [Theory]
    [InlineData("step1.png", "step1.png")]
    [InlineData("case 3/step1", "case 3\\step1.png")]
    [InlineData("STEP.PNG", "STEP.PNG")]
    [InlineData("shot.jpg", "shot.jpg.png")]
    [InlineData("console.png", "console.png")]
    public void Resolves_paths_inside_the_directory(string path, string expected)
    {
        Assert.Equal(Path.Combine(_directory, expected), ScreenshotFiles.Resolve(_directory, path));
    }

    [Theory]
    [InlineData("..\\outside.png")]
    [InlineData("C:\\Windows\\evil.png")]
    [InlineData("sub\\..\\..\\outside.png")]
    [InlineData(".")]
    [InlineData("shot.png:stream")]
    [InlineData("shot.png::$DATA")]
    [InlineData("NUL")]
    [InlineData("con.png")]
    [InlineData("sub\\COM1.png")]
    [InlineData("aux\\shot.png")]
    public void Refuses_paths_outside_the_directory_or_that_are_no_plain_file(string path)
    {
        Assert.Equal(ErrorCodes.InvalidArgument, Assert.Throws<NetwrightException>(() => ScreenshotFiles.Resolve(_directory, path)).Code);
    }

    [Fact]
    public void Resolves_inside_a_drive_root()
    {
        var root = Path.GetPathRoot(_directory)!;

        Assert.Equal(Path.Combine(root, "shots", "a.png"), ScreenshotFiles.Resolve(root, "shots/a.png"));
    }

    [Fact]
    public void Refuses_to_save_without_a_configured_directory()
    {
        var error = Assert.Throws<NetwrightException>(() => ScreenshotFiles.Resolve(null, "a.png"));

        Assert.Contains("--screenshot-dir", error.Hint, StringComparison.Ordinal);
    }

    [Fact]
    public void Saves_into_new_subdirectories_and_replaces_an_existing_file()
    {
        ScreenshotFiles.Save(_directory, "run/1.png", [9]);
        var saved = ScreenshotFiles.Save(_directory, "run/1.png", [1, 2, 3]);

        Assert.Equal([1, 2, 3], File.ReadAllBytes(saved));
    }

    [Fact]
    public void Refuses_to_save_through_a_junction_that_leaves_the_directory()
    {
        var link = Path.Combine(_directory, "link");
        using (var mklink = Process.Start(new ProcessStartInfo("cmd.exe", $"/c mklink /J \"{link}\" \"{_outside}\"") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true })!)
        {
            mklink.WaitForExit();
            Assert.Equal(0, mklink.ExitCode);
        }

        var error = Assert.Throws<NetwrightException>(() => ScreenshotFiles.Save(_directory, "link/escaped.png", [1]));

        Assert.Equal(ErrorCodes.InvalidArgument, error.Code);
        Assert.Empty(Directory.EnumerateFileSystemEntries(_outside));
        Directory.Delete(link);
    }
}
