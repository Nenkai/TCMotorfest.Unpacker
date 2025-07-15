using Syroot.BinaryData;

using System;
using System.Buffers.Binary;
using System.Numerics;
using System.Text;
using System.Diagnostics;

using CommandLine;

namespace TCMotorfest.Unpacker;

public class Program
{
    static void Main(string[] args)
    {
        Console.WriteLine("-----------------------------------------");
        Console.WriteLine($"- TCMotorfestUnpack by Nenkai");
        Console.WriteLine("-----------------------------------------");
        Console.WriteLine("- https://github.com/Nenkai");
        Console.WriteLine("- https://twitter.com/Nenkaai");
        Console.WriteLine("-----------------------------------------");
        Console.WriteLine("");

        Keys.LoadKeys();

        if (args.Length == 1 && args[0].EndsWith(".cbd"))
        {
            ExtractCbd(new ExtractCbdVerbs()
            {
                InputPath = args[0]
            });
            return;
        }

        Parser.Default.ParseArguments<ExtractAllVerbs, ExtractVerbs, ListFilesVerbs, ExtractCbdVerbs>(args)
            .WithParsed<ExtractAllVerbs>(ExtractAll)
            .WithParsed<ExtractVerbs>(Extract)
            .WithParsed<ListFilesVerbs>(ListFiles)
            .WithParsed<ExtractCbdVerbs>(ExtractCbd);
    }

    public static void Extract(ExtractVerbs verbs)
    {
        if (!File.Exists(verbs.InputPath))
        {
            Console.WriteLine($"ERROR: Toc file '{verbs.InputPath}' does not exist.");
            return;
        }

        if (string.IsNullOrWhiteSpace(verbs.FileToExtract))
        {
            Console.WriteLine($"ERROR: No file to extract provided.");
            return;
        }

        string inputDir = Path.GetDirectoryName(Path.GetFullPath(verbs.InputPath))!;
        if (string.IsNullOrEmpty(verbs.OutputPath))
            verbs.OutputPath = Path.Combine(inputDir, verbs.FileToExtract);
        else
            verbs.OutputPath = Path.GetFullPath(verbs.OutputPath);

        using var bigFile = new BigFileSystem();
        try
        {
            bigFile.Init(verbs.InputPath);
            if (bigFile.ExtractFile(verbs.FileToExtract, verbs.OutputPath))
            {
                Console.WriteLine($"OK: '{verbs.FileToExtract}' => '{verbs.OutputPath}'.");
            }
            else
            {
                Console.WriteLine("ERROR: File not found.");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"ERROR: Failed to extract {verbs.FileToExtract} - {ex.Message}");
            return;
        }
    }

    public static void ExtractCbd(ExtractCbdVerbs verbs)
    {
        if (Directory.Exists(verbs.InputPath))
        {
            Console.WriteLine($"Batch processing CBD files in directory: {verbs.InputPath}");
            Console.WriteLine($"Output directory: {verbs.OutputPath}");

            string[] cbdFiles = Directory.GetFiles(verbs.InputPath, "*.cbd", SearchOption.AllDirectories);
            if (cbdFiles.Length == 0)
            {
                Console.WriteLine("No CBD files found in the specified directory.");
                return;
            }

            Console.WriteLine($"CBD files: {cbdFiles.Length}");
            foreach (var cbdFile in cbdFiles)
            {
                Console.WriteLine($"Processing: {cbdFile}");
                try
                {
                    ExtractCbd(new ExtractCbdVerbs()
                    {
                        InputPath = cbdFile,
                        OutputPath = verbs.OutputPath
                    });
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"ERROR: Failed to process {cbdFile} - {ex.Message}");
                }
            }

            Console.WriteLine("Batch extraction completed.");
        }
        else if (File.Exists(verbs.InputPath))
        {
            string inputDir = Path.GetDirectoryName(Path.GetFullPath(verbs.InputPath))!;

            if (string.IsNullOrEmpty(verbs.OutputPath))
                verbs.OutputPath = Path.Combine(inputDir, Path.GetFileNameWithoutExtension(verbs.InputPath));
            else
                verbs.OutputPath = Path.GetFullPath(verbs.OutputPath);

            using var fs = File.OpenRead(verbs.InputPath);
            var cbdFile = new CbdFile();
            cbdFile.FromStream(fs);

            Console.WriteLine($"Output directory: {verbs.OutputPath}");
            foreach (var file in cbdFile.Files)
            {
                string outputPath = Path.Combine(verbs.OutputPath, file.Key);
                Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);

                Console.WriteLine($"-> {file.Key} (0x{file.Value.Length:X} bytes)");
                File.WriteAllBytes(outputPath, file.Value);
            }
        }
        else
        {
            Console.WriteLine($"ERROR: Cbd file '{verbs.InputPath}' does not exist.");
        }
    }

    public static void ExtractAll(ExtractAllVerbs verbs)
    {
        if (!File.Exists(verbs.InputPath))
        {
            Console.WriteLine($"ERROR: Toc file '{verbs.InputPath}' does not exist.");
            return;
        }

        using var bigFile = new BigFileSystem();
        bigFile.Init(verbs.InputPath);

        string inputDir = Path.GetDirectoryName(Path.GetFullPath(verbs.InputPath))!;
        verbs.OutputPath ??= Path.Combine(inputDir, Path.GetFileNameWithoutExtension(verbs.InputPath));
        bigFile.ExtractAll(verbs.OutputPath);
    }

    public static void ListFiles(ListFilesVerbs verbs)
    {
        if (!File.Exists(verbs.InputPath))
        {
            Console.WriteLine($"ERROR: Toc file '{verbs.InputPath}' does not exist.");
            return;
        }

        using var bigFile = new BigFileSystem();
        bigFile.Init(verbs.InputPath);

        bigFile.ListFiles(Path.ChangeExtension(verbs.InputPath, ".files.txt"));
        Console.WriteLine("Listing files done.");
    }
}

[Verb("extract-file", HelpText = "Extract a specific file from a .toc archive.")]
public class ExtractVerbs
{
    [Option('i', "input", Required = true, HelpText = "Input .toc file.")]
    public required string InputPath { get; set; }

    [Option('o', "output", Required = false, HelpText = "Output directory for the file.")]
    public string? OutputPath { get; set; }

    [Option('f', "file", Required = true, HelpText = "File from the archive to extract.")]
    public string? FileToExtract { get; set; }
}

[Verb("extract-cbd", HelpText = "Extract files from .cbd files.")]
public class ExtractCbdVerbs
{
    [Option('i', "input", Required = true, HelpText = "Input .cbd file, or folder containing cbd files.")]
    public required string InputPath { get; set; }

    [Option('o', "output", Required = false, HelpText = "Output directory for the file.")]
    public string? OutputPath { get; set; }

    [Option('r', "recursive", Required = false, HelpText = "Whether to extract from folders recursively (if a foler is provided)")]
    public bool Recursive { get; set; }
}

[Verb("extract-all", HelpText = "Extract all files from a .toc archive.")]
public class ExtractAllVerbs
{
    [Option('i', "input", Required = true, HelpText = "Input .toc file.")]
    public required string InputPath { get; set; }

    [Option('o', "output", Required = false, HelpText = "Output directory for files.")]
    public string? OutputPath { get; set; }

    [Option('u', "extract-unknown", Required = false, HelpText = "Whether to extract unknown files.")]
    public bool ExtractUnknown { get; set; }
}

[Verb("list-files", HelpText = "List files from .toc big file.")]
public class ListFilesVerbs
{
    [Option('i', "input", Required = true, HelpText = "Input .toc file.")]
    public required string InputPath { get; set; }
}