using System.Text.Json;
using PixelVoxel.Export;
using PixelVoxel.Imaging;

return await RunAsync(args);

static async Task<int> RunAsync(string[] args)
{
    if (args.Length == 0 || args[0] is "-h" or "--help" or "help")
    {
        PrintUsage();
        return 0;
    }

    if (args.Length != 2 || args[0] is not ("inspect" or "validate"))
    {
        Console.Error.WriteLine("Invalid arguments.");
        PrintUsage();
        return 2;
    }

    try
    {
        string path = Path.GetFullPath(args[1]);
        PixelVoxelProject project = await new PxvProjectSerializer(
            new PngPixelWriter(), new PngPixelReader()).LoadAsync(path);
        if (args[0] == "validate")
        {
            Console.WriteLine($"Valid Pixel2Voxel project: {path}");
            Console.WriteLine($"Frames: {project.Frames.Count}; current: {project.CurrentFrameIndex + 1}");
            return 0;
        }

        object report = new
        {
            path,
            frameCount = project.Frames.Count,
            currentFrameIndex = project.CurrentFrameIndex,
            paletteColors = project.Palette.Count,
            frames = project.Frames.Select((frame, index) => new
            {
                index,
                frame.Name,
                frame.DurationMilliseconds,
                dimensions = new
                {
                    width = frame.Document.Storage.Dimensions.Width,
                    height = frame.Document.Storage.Dimensions.Height,
                    depth = frame.Document.Storage.Dimensions.Depth,
                },
                occupiedVoxels = frame.Document.Storage.OccupiedCount,
                sourceFaces = frame.SourceViews?.Faces.OrderBy(face => face).Select(face => face.ToString()) ?? [],
            }),
        };
        Console.WriteLine(JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
        return 0;
    }
    catch (Exception exception)
    {
        Console.Error.WriteLine($"Pixel2Voxel project validation failed: {exception.Message}");
        return 1;
    }
}

static void PrintUsage()
{
    Console.WriteLine("Pixel2Voxel CLI");
    Console.WriteLine("  PixelVoxel.Cli inspect <project.pxv>   Print project metadata as JSON");
    Console.WriteLine("  PixelVoxel.Cli validate <project.pxv>  Validate the complete project container");
}
