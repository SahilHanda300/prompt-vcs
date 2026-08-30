using System.Text;
using PromptVcs.Cli;

Console.OutputEncoding = Encoding.UTF8;
// Force CRLF regardless of OS default (bare LF on Linux). Real terminals get
// LF-to-CRLF translated for free by the TTY driver; this CLI's output can
// also be piped through a non-TTY bridge (the web terminal), which performs
// no such translation — without it, xterm.js moves down a row on each "\n"
// without returning to column 0, staircasing every line to the right.
Console.Out.NewLine = "\r\n";

var client = new ServerClient();

var commandUsage = new Dictionary<string, string>
{
    ["init"] = "init",
    ["create"] = "create <name> [--content <text> | --file <path>]",
    ["edit"] = "edit <name> [--content <text> | --file <path>]",
    ["list"] = "list",
    ["show"] = "show <name> [--version <n>]",
    ["diff"] = "diff <name> <v1> <v2>",
    ["reset"] = "reset",
};
var isInteractive = false;

if (args.Length == 0)
{
    isInteractive = true;
    return await RunInteractiveAsync();
}

return await RunCommandAsync(args);

async Task<int> RunInteractiveAsync()
{
    PrintWelcome();
    while (true)
    {
        Console.Write("pvcs> ");
        var line = Console.ReadLine();
        if (line == null) return 0; // EOF (e.g. redirected/closed input)

        line = line.Trim();
        if (line.Length == 0) continue;
        if (line is "exit" or "quit") return 0;

        await RunCommandAsync(Tokenize(line));
    }
}

void PrintWelcome()
{
    WriteLineColor("PromptVCS Terminal Version 1.0", ConsoleColor.Cyan);
    Console.Write("If you want to get started, try: ");
    WriteLineColor("init", ConsoleColor.Green);
    Console.Write("Type ");
    WriteColor("help", ConsoleColor.Yellow);
    Console.Write(" to see all commands, or ");
    WriteColor("exit", ConsoleColor.Yellow);
    Console.WriteLine(" to quit.");
}

async Task<int> RunCommandAsync(string[] cmdArgs)
{
    var command = cmdArgs[0];
    try
    {
        switch (command)
        {
            case "init":
                await RunInitAsync();
                break;
            case "create":
                await RunCreateAsync(RequireArg(cmdArgs, 1, "name"), ReadContent(cmdArgs, isInteractive));
                break;
            case "edit":
                await RunEditAsync(RequireArg(cmdArgs, 1, "name"), ReadContent(cmdArgs, isInteractive));
                break;
            case "list":
                await RunListAsync();
                break;
            case "show":
                await RunShowAsync(RequireArg(cmdArgs, 1, "name"), ParseIntOption(cmdArgs, "--version", "-v"));
                break;
            case "diff":
                await RunDiffAsync(RequireArg(cmdArgs, 1, "name"), RequireArg(cmdArgs, 2, "v1"), RequireArg(cmdArgs, 3, "v2"));
                break;
            case "reset":
                await RunResetAsync();
                break;
            case "-h":
            case "--help":
            case "help":
                PrintUsage();
                break;
            default:
                WriteLineColor($"Unknown command: {command}", ConsoleColor.Red);
                var suggestion = SuggestCommand(command, commandUsage.Keys);
                if (suggestion != null)
                {
                    Console.Write("Did you mean ");
                    WriteColor(suggestion, ConsoleColor.Yellow);
                    Console.WriteLine("?");
                    WriteLineColor($"  Usage: pvcs {commandUsage[suggestion]}", ConsoleColor.Yellow);
                }
                else
                {
                    PrintUsage();
                }
                return 1;
        }
    }
    catch (CliUsageException ex)
    {
        // Only argument-shape problems (missing name, no content, etc.) get
        // the usage hint — it would be misleading noise on domain errors
        // like "prompt already exists", which already say what to do instead.
        WriteLineColor($"Error: {ex.Message}", ConsoleColor.Red);
        if (commandUsage.TryGetValue(command, out var usage))
        {
            WriteLineColor($"  Usage: pvcs {usage}", ConsoleColor.Yellow);
        }
        return 1;
    }
    catch (Exception ex)
    {
        WriteLineColor($"Error: {ex.Message}", ConsoleColor.Red);
        return 1;
    }

    return 0;
}

/// Finds the closest known command to a mistyped one (e.g. "int" -> "init"),
/// so a typo gets a helpful hint instead of just "unknown command".
static string? SuggestCommand(string input, IEnumerable<string> knownCommands)
{
    string? best = null;
    var bestDistance = int.MaxValue;
    foreach (var candidate in knownCommands)
    {
        var distance = LevenshteinDistance(input, candidate);
        if (distance < bestDistance)
        {
            bestDistance = distance;
            best = candidate;
        }
    }
    return bestDistance <= 2 ? best : null;
}

static int LevenshteinDistance(string a, string b)
{
    var d = new int[a.Length + 1, b.Length + 1];
    for (var i = 0; i <= a.Length; i++) d[i, 0] = i;
    for (var j = 0; j <= b.Length; j++) d[0, j] = j;

    for (var i = 1; i <= a.Length; i++)
    {
        for (var j = 1; j <= b.Length; j++)
        {
            var cost = a[i - 1] == b[j - 1] ? 0 : 1;
            d[i, j] = Math.Min(Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1), d[i - 1, j - 1] + cost);
        }
    }
    return d[a.Length, b.Length];
}

async Task RunInitAsync()
{
    var result = await client.InitAsync();
    if (result.AlreadyInitialized)
    {
        WriteLineColor("Store already initialized.", ConsoleColor.Yellow);
    }
    else
    {
        WriteLineColor("Initialized PromptVCS store on the server.", ConsoleColor.Green);
    }
}

async Task RunResetAsync()
{
    await client.ResetAsync();
    WriteLineColor("Store reset. All prompts and generated sites deleted.", ConsoleColor.Yellow);
}

async Task RunCreateAsync(string name, string content)
{
    var result = await client.CreateAsync(name, content);
    PrintPipelineResult(result);
}

async Task RunEditAsync(string name, string content)
{
    var result = await client.EditAsync(name, content);
    PrintPipelineResult(result);
}

async Task RunListAsync()
{
    var items = await client.ListAsync();
    if (items.Count == 0)
    {
        Console.WriteLine("No prompts yet. Use `pvcs create <name>` to add one.");
        return;
    }
    Console.WriteLine(string.Join("\t", "name", "dev", "qa", "prod", "latest build"));
    foreach (var r in items)
    {
        Console.Write(string.Join("\t",
            r.Name,
            r.Dev?.ToString() ?? "-",
            r.Qa?.ToString() ?? "-",
            r.Prod?.ToString() ?? "-") + "\t");
        if (r.LatestBuild != null)
        {
            WriteColor($"{r.LatestBuild.Status} (v{r.LatestBuild.BuildVersion})", BuildStatusColor(r.LatestBuild.Status));
        }
        else
        {
            Console.Write("-");
        }
        Console.WriteLine();
    }
}

static ConsoleColor? BuildStatusColor(string status) => status switch
{
    "success" => ConsoleColor.Green,
    "failed" => ConsoleColor.Red,
    "skipped" => ConsoleColor.Yellow,
    _ => null,
};

async Task RunShowAsync(string name, int? version)
{
    var result = await client.ShowAsync(name, version);
    var record = result.Record;
    Console.WriteLine($"# {record.Name} (v{result.Version})");
    Console.WriteLine();
    Console.WriteLine(result.Content);
    Console.WriteLine();
    Console.WriteLine($"environments: dev={record.Environments.Dev?.ToString() ?? "-"} qa={record.Environments.Qa?.ToString() ?? "-"} prod={record.Environments.Prod?.ToString() ?? "-"}");
    Console.WriteLine($"versions: {string.Join(", ", record.History.Select(h => h.Version))}");
    if (record.QaCheckpoints.Count > 0)
    {
        Console.WriteLine();
        Console.WriteLine("qa checkpoints:");
        foreach (var cp in record.QaCheckpoints)
        {
            Console.Write($"  v{cp.Version} — ");
            WriteColor(cp.Passed ? "passed" : "failed", cp.Passed ? ConsoleColor.Green : ConsoleColor.Red);
            Console.WriteLine($" ({cp.Timestamp:O})");
        }
    }
    if (record.Builds.Count > 0)
    {
        Console.WriteLine();
        Console.WriteLine("builds:");
        foreach (var b in record.Builds)
        {
            var suffix = b.ArtifactUrl != null ? $" — {ResolveArtifactUrl(b.ArtifactUrl)}" : "";
            Console.Write($"  build v{b.BuildVersion} (prompt v{b.PromptVersion}) — ");
            WriteColor(b.Status, BuildStatusColor(b.Status));
            Console.WriteLine(suffix);
        }
    }
}

async Task RunDiffAsync(string name, string v1Raw, string v2Raw)
{
    var v1 = int.Parse(v1Raw);
    var v2 = int.Parse(v2Raw);
    var lines = await client.DiffAsync(name, v1, v2);
    foreach (var line in lines)
    {
        var (prefix, color) = line.Type switch
        {
            "inserted" => ("+", (ConsoleColor?)ConsoleColor.Green),
            "deleted" => ("-", (ConsoleColor?)ConsoleColor.Red),
            _ => (" ", (ConsoleColor?)null),
        };
        WriteLineColor($"{prefix} {line.Text}", color);
    }
}

void PrintPipelineResult(PipelineResultDto result)
{
    var cp = result.Checkpoint;
    Console.WriteLine();

    if (cp.Passed)
    {
        Console.Write($"v{result.Version} ");
        WriteLineColor("QA passed", ConsoleColor.Green);
    }
    else
    {
        var (label, check) = FindFailedCheck(cp.Checks);
        Console.Write($"v{result.Version} ");
        WriteColor("QA failed", ConsoleColor.Red);
        Console.WriteLine($" — {label}:");
        if (check.Detail != null)
        {
            WriteLineColor($"  {check.Detail}", ConsoleColor.Red);
        }
    }

    switch (result.Stage)
    {
        case "publishSkipped":
            WriteLineColor($"⚠ {result.Message}", ConsoleColor.Yellow);
            break;
        case "publishFailed":
            WriteLineColor($"✗ {result.Message}", ConsoleColor.Red);
            break;
        case "published":
            WriteLineColor($"✓ Site deployed: {ResolveArtifactUrl(result.Build?.ArtifactUrl)}", ConsoleColor.Green);
            break;
        case "qaFailed":
            break; // already shown above, nothing more to add
    }
}

static (string Label, QaCheckResultDto Check) FindFailedCheck(QaChecksDto checks)
{
    if (!checks.Validation.Passed) return ("validation", checks.Validation);
    if (!checks.ContentSafety.Passed) return ("content safety", checks.ContentSafety);
    return ("trial generation", checks.TrialGeneration);
}

/// The server stores/returns artifact paths as relative (e.g. "/site/demo/")
/// rather than baking in its own externally-visible hostname/port — the CLI
/// already knows the base it used to reach the server, so it resolves the
/// clickable link itself at display time.
static string? ResolveArtifactUrl(string? path)
{
    if (path == null) return null;
    if (path.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || path.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
    {
        return path;
    }
    var mcpUrl = Environment.GetEnvironmentVariable("PROMPTVCS_MCP_URL") ?? "http://localhost:5279/mcp";
    var baseUrl = mcpUrl.EndsWith("/mcp", StringComparison.OrdinalIgnoreCase) ? mcpUrl[..^4] : mcpUrl;
    return baseUrl.TrimEnd('/') + path;
}

// Raw ANSI SGR codes rather than Console.ForegroundColor: this CLI runs in
// three contexts now (a local Windows console, a local Linux console, and
// piped through a WebSocket to a browser-based xterm.js terminal), and
// .NET's Console color API may silently no-op when it detects stdout isn't
// a real attached terminal — exactly the case once output is redirected to
// be pumped elsewhere. Raw codes work identically in all three.
static void WriteColor(string text, ConsoleColor? color)
{
    if (color == null) { Console.Write(text); return; }
    Console.Write($"{AnsiCode(color.Value)}{text}\x1b[0m");
}

static void WriteLineColor(string text, ConsoleColor? color)
{
    if (color == null) { Console.WriteLine(text); return; }
    Console.WriteLine($"{AnsiCode(color.Value)}{text}\x1b[0m");
}

static string AnsiCode(ConsoleColor color) => color switch
{
    ConsoleColor.Green => "\x1b[32m",
    ConsoleColor.Red => "\x1b[31m",
    ConsoleColor.Yellow => "\x1b[33m",
    ConsoleColor.Cyan => "\x1b[36m",
    ConsoleColor.Magenta => "\x1b[35m",
    _ => "",
};

static string RequireArg(string[] args, int index, string label)
{
    if (index >= args.Length || args[index].StartsWith('-'))
    {
        throw new CliUsageException($"Missing required argument: {label}");
    }
    return args[index];
}

static string ReadContent(string[] args, bool isInteractive)
{
    var content = GetOption(args, "--content", "-c");
    if (content != null) return content;

    var file = GetOption(args, "--file", "-f");
    if (file != null) return File.ReadAllText(file);

    // Reading the rest of stdin only makes sense for one-shot invocation
    // (e.g. `echo "..." | pvcs create x`) — inside the REPL, stdin is the
    // same stream driving the command loop, so this would swallow every
    // command typed afterward instead of just failing clearly.
    if (!isInteractive && Console.IsInputRedirected)
    {
        return Console.In.ReadToEnd();
    }

    throw new CliUsageException("No prompt content given. Use --content or --file.");
}

static string? GetOption(string[] args, string longName, string shortName)
{
    for (var i = 0; i < args.Length - 1; i++)
    {
        if (args[i] == longName || args[i] == shortName) return args[i + 1];
    }
    return null;
}

static int? ParseIntOption(string[] args, string longName, string shortName)
{
    var value = GetOption(args, longName, shortName);
    return value != null ? int.Parse(value) : null;
}

/// Splits a typed line into argv-style tokens, honoring "quoted spans" so
/// --content "Build a calculator" works when typed directly at the prompt.
static string[] Tokenize(string line)
{
    var tokens = new List<string>();
    var current = new StringBuilder();
    var inQuotes = false;
    var hasToken = false;

    foreach (var c in line)
    {
        if (c == '"')
        {
            inQuotes = !inQuotes;
            hasToken = true;
            continue;
        }
        if (char.IsWhiteSpace(c) && !inQuotes)
        {
            if (hasToken)
            {
                tokens.Add(current.ToString());
                current.Clear();
                hasToken = false;
            }
            continue;
        }
        current.Append(c);
        hasToken = true;
    }
    if (hasToken) tokens.Add(current.ToString());

    return tokens.ToArray();
}

static void PrintUsage()
{
    // Written as individual WriteLine calls, not one big raw-string literal —
    // a raw string's embedded newlines are baked into the string itself and
    // bypass Console.Out.NewLine entirely, reintroducing the same bare-LF
    // staircasing in the web terminal that the CRLF fix elsewhere solved.
    const string text = """
    pvcs — version control for prompts with an automatic dev/qa/prod pipeline

    Commands:
      init
      create <name> [--content <text> | --file <path>]
      edit <name> [--content <text> | --file <path>]
      list
      show <name> [--version <n>]
      diff <name> <v1> <v2>
      reset
    """;
    foreach (var line in text.Split('\n'))
    {
        Console.WriteLine(line.TrimEnd('\r'));
    }
}

class CliUsageException(string message) : Exception(message);
