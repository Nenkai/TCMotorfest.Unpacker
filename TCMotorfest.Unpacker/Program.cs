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
        Console.WriteLine("TCMotorfestUnpack by Nenkai");
        Console.WriteLine("- https://github.com/Nenkai");
        Console.WriteLine("- https://twitter.com/Nenkaai");
        Console.WriteLine("-----------------------------");

        Keys.LoadKeys();

        if (args.Length == 1 && args[0].EndsWith(".cbd"))
        {
            ExtractCbd(new ExtractCbdVerbs()
            {
                InputPath = args[0]
            });
            return;
        }

        Parser.Default.ParseArguments<ExtractVerbs, ExtractAllVerbs, ExtractCbdVerbs, ListFilesVerbs>(args)
            .WithParsed<ExtractVerbs>(Extract)
            .WithParsed<ExtractAllVerbs>(ExtractAll)
             .WithParsed<ExtractCbdVerbs>(ExtractCbd)
            .WithParsed<ListFilesVerbs>(ListFiles);
    }

    public static void Extract(ExtractVerbs verbs)
    {
        if (!File.Exists(verbs.InputPath))
        {
            Console.WriteLine($"ERROR: Toc file '{verbs.InputPath}' does not exist.");
            return;
        }

        string inputDir = Path.GetDirectoryName(Path.GetFullPath(verbs.InputPath));
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
        if (!File.Exists(verbs.InputPath))
        {
            Console.WriteLine($"ERROR: Cbd file '{verbs.InputPath}' does not exist.");
            return;
        }

        string inputDir = Path.GetDirectoryName(Path.GetFullPath(verbs.InputPath));

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
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath));

            Console.WriteLine($"-> {file.Key} (0x{file.Value.Length:X} bytes)");
            File.WriteAllBytes(outputPath, file.Value);
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

        string inputDir = Path.GetDirectoryName(Path.GetFullPath(verbs.InputPath));

        if (string.IsNullOrEmpty(verbs.OutputPath))
            verbs.OutputPath = Path.Combine(inputDir, Path.GetFileNameWithoutExtension(verbs.InputPath));
        else
            verbs.OutputPath = Path.GetFullPath(verbs.OutputPath);

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

[Verb("extract-file", HelpText = "Extract files from a .toc archive.")]
public class ExtractVerbs
{
    [Option('i', "input", Required = true, HelpText = "Input data.i file.")]
    public string InputPath { get; set; }

    [Option('o', "output", Required = false, HelpText = "Output directory for the file. Defaults to data folder next to data.i.")]
    public string OutputPath { get; set; }

    [Option('f', "file", Required = true, HelpText = "File from the archive to extract.")]
    public string FileToExtract { get; set; }
}

[Verb("extract-cbd", HelpText = "Extract files from a .toc archive.")]
public class ExtractCbdVerbs
{
    [Option('i', "input", Required = true, HelpText = "Input .cbd file.")]
    public string InputPath { get; set; }

    [Option('o', "output", Required = false, HelpText = "Output directory for the file.")]
    public string OutputPath { get; set; }
}

[Verb("extract-all", HelpText = "Extract all files from a .toc archive.")]
public class ExtractAllVerbs
{
    [Option('i', "input", Required = true, HelpText = "Input data.i file.")]
    public string InputPath { get; set; }

    [Option('o', "output", Required = false, HelpText = "Output directory for files.")]
    public string OutputPath { get; set; }

    [Option('u', "extract-unknown", Required = false, HelpText = "Whether to extract unknown files.")]
    public bool ExtractUnknown { get; set; }
}

[Verb("list-files", HelpText = "List files from .toc big file.")]
public class ListFilesVerbs
{
    [Option('i', "input", Required = true, HelpText = "Input .toc file.")]
    public string InputPath { get; set; }
}