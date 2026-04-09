using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace WinSpotlight;

public sealed class SearchEngine
{
    private readonly ClipboardManager _clipboard;
    private List<AppEntry> _appCache = [];
    private volatile bool  _cacheReady;

    public SearchEngine(ClipboardManager clipboard)
    {
        _clipboard = clipboard;
        Task.Run(BuildAppCache); // pre-warm cache in background
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  Public API
    // ═══════════════════════════════════════════════════════════════════════

    public async Task<List<SearchResult>> SearchAsync(string query, CancellationToken ct = default)
    {
        // Empty query → show upcoming events + clipboard history
        if (string.IsNullOrWhiteSpace(query))
        {
            var emptyResults = new List<SearchResult>();
            emptyResults.AddRange(await GetCalendarEventsAsync());
            emptyResults.AddRange(ClipboardResults("").Take(4));
            return emptyResults;
        }

        var results = new List<SearchResult>();

        // 0. Timer / Calendar specific triggers
        var timer = TryProcessTimer(query);
        if (timer != null) results.Add(timer);
        var math = TryCalculate(query);
        if (math != null) results.Add(math);

        // 2. Apps
        var apps = await SearchAppsAsync(query, ct);
        results.AddRange(apps.Take(5));

        // 3. Files
        var files = await SearchFilesAsync(query, ct);
        results.AddRange(files.Take(4));

        // 4. Clipboard history
        results.AddRange(ClipboardResults(query).Take(2));

        // 5. Web search – always last
        results.Add(WebResult(query));

        return results;
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  Calculator
    // ═══════════════════════════════════════════════════════════════════════

    // Allow digits, spaces, operators, parentheses, dots
    private static readonly Regex MathRx =
        new(@"^[\d\s\+\-\*\/\(\)\.\,\%]+$", RegexOptions.Compiled);

    private static SearchResult? TryCalculate(string q)
    {
        var expr = q.Trim().TrimEnd('=');
        if (expr.Length < 2) return null;
        if (!MathRx.IsMatch(expr)) return null;

        try
        {
            var raw    = new DataTable().Compute(expr, null);
            var value  = Convert.ToDouble(raw);
            var display = value % 1 == 0 ? ((long)value).ToString()
                                         : value.ToString("G12");
            return new SearchResult
            {
                Title      = display,
                Subtitle   = $"{expr.Trim()} = {display}",
                Category   = ResultCategory.Math,
                ActionPath = display    // copied to clipboard on launch
            };
        }
        catch { return null; }
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  App search  (Start Menu .lnk files)
    // ═══════════════════════════════════════════════════════════════════════

    private record AppEntry(string Name, string LnkPath, string ExePath);

    private void BuildAppCache()
    {
        var folders = new[]
        {
            Environment.GetFolderPath(Environment.SpecialFolder.StartMenu),
            Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu)
        };

        var list = new List<AppEntry>();
        var opts = new EnumerationOptions { IgnoreInaccessible = true, RecurseSubdirectories = true };
        foreach (var folder in folders)
        {
            if (!Directory.Exists(folder)) continue;
            foreach (var lnk in Directory.EnumerateFiles(folder, "*.lnk", opts))
            {
                try
                {
                    var name   = Path.GetFileNameWithoutExtension(lnk);
                    var target = ResolveShortcut(lnk) ?? lnk;
                    list.Add(new AppEntry(name, lnk, target));
                }
                catch { /* skip broken .lnk */ }
            }
        }
        _appCache   = list;
        _cacheReady = true;
    }

    /// <summary>Resolve a .lnk file to its target using WScript.Shell via reflection.</summary>
    private static string? ResolveShortcut(string lnkPath)
    {
        try
        {
            var type = Type.GetTypeFromProgID("WScript.Shell");
            if (type is null) return null;
            var shell    = Activator.CreateInstance(type)!;
            var shortcut = type.InvokeMember(
                "CreateShortcut", System.Reflection.BindingFlags.InvokeMethod,
                null, shell, new object[] { lnkPath });
            return shortcut?.GetType().InvokeMember(
                "TargetPath", System.Reflection.BindingFlags.GetProperty,
                null, shortcut, null) as string;
        }
        catch { return null; }
    }

    private async Task<List<SearchResult>> SearchAppsAsync(string query, CancellationToken ct)
    {
        // Wait briefly for the background cache if not ready yet
        for (int i = 0; i < 10 && !_cacheReady; i++)
            await Task.Delay(50, ct);

        return await Task.Run(() =>
        {
            var q = query.ToLowerInvariant();
            return _appCache
                .Where(a => a.Name.ToLowerInvariant().Contains(q))
                .OrderBy(a =>
                {
                    var n = a.Name.ToLowerInvariant();
                    if (n == q)           return 0;
                    if (n.StartsWith(q))  return 1;
                    return 2;
                })
                .Select(a => new SearchResult
                {
                    Title      = a.Name,
                    Subtitle   = a.ExePath,
                    Category   = ResultCategory.App,
                    ActionPath = a.LnkPath
                })
                .ToList();
        }, ct);
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  File search  (common user folders)
    // ═══════════════════════════════════════════════════════════════════════

    private static readonly string[] SearchRoots =
    [
        Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads"),
        Environment.GetFolderPath(Environment.SpecialFolder.MyPictures),
        Environment.GetFolderPath(Environment.SpecialFolder.MyMusic),
        Environment.GetFolderPath(Environment.SpecialFolder.MyVideos),
    ];

    private static readonly HashSet<string> SkipExt =
        new([".lnk", ".tmp", ".ini", ".db", ".sys", ".log", ".bak"], StringComparer.OrdinalIgnoreCase);

    private static async Task<List<SearchResult>> SearchFilesAsync(string query, CancellationToken ct)
    {
        return await Task.Run(() =>
        {
            var q       = query.ToLowerInvariant();
            var results = new List<SearchResult>();
            var opts    = new EnumerationOptions { IgnoreInaccessible = true, RecurseSubdirectories = true };

            foreach (var root in SearchRoots)
            {
                if (!Directory.Exists(root)) continue;
                try
                {
                    foreach (var entry in Directory.EnumerateFileSystemEntries(root, "*", opts))
                    {
                        ct.ThrowIfCancellationRequested();

                        var ext  = Path.GetExtension(entry);
                        if (ext.Length > 0 && SkipExt.Contains(ext)) continue;

                        var name = Path.GetFileName(entry);
                        if (!name.ToLowerInvariant().Contains(q)) continue;

                        bool isDir = Directory.Exists(entry);

                        results.Add(new SearchResult
                        {
                            Title      = name,
                            Subtitle   = entry,
                            Category   = isDir ? ResultCategory.Folder : ResultCategory.File,
                            ActionPath = entry
                        });
                        if (results.Count >= 15) return results;
                    }
                }
                catch (OperationCanceledException) { throw; }
                catch { /* skip inaccessible directories */ }
            }
            return results;
        }, ct);
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  Clipboard history
    // ═══════════════════════════════════════════════════════════════════════

    private IEnumerable<SearchResult> ClipboardResults(string query)
    {
        var q = query.ToLowerInvariant();
        return _clipboard.History
            .Where(t => string.IsNullOrEmpty(q) || t.ToLowerInvariant().Contains(q))
            .Select(t => new SearchResult
            {
                Title      = t.Length > 100 ? t[..100] + "…" : t,
                Subtitle   = "Clipboard history — Enter to copy again",
                Category   = ResultCategory.Clipboard,
                ActionPath = t
            });
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  Web search (always available)
    // ═══════════════════════════════════════════════════════════════════════

    private static SearchResult WebResult(string query) => new()
    {
        Title      = $"Search \"{(query.Length > 60 ? query[..60] + "…" : query)}\" on Google",
        Subtitle   = $"https://www.google.com/search?q={Uri.EscapeDataString(query)}",
        Category   = ResultCategory.Web,
        ActionPath = $"https://www.google.com/search?q={Uri.EscapeDataString(query)}"
    };

    // ═══════════════════════════════════════════════════════════════════════
    //  Timer / Calendar Implementation
    // ═══════════════════════════════════════════════════════════════════════

    private static readonly Regex TimerRx = new(@"^(?:timer\s+)?(\d+)([smh]?)$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static SearchResult? TryProcessTimer(string query)
    {
        var match = TimerRx.Match(query.Trim());
        if (!match.Success) return null;

        int value = int.Parse(match.Groups[1].Value);
        string unit = match.Groups[2].Value.ToLowerInvariant();
        
        string label = unit switch {
            "s" => "seconds",
            "h" => "hours",
            _   => "minutes"
        };

        // Convert to seconds for action path
        int seconds = unit switch {
            "s" => value,
            "h" => value * 3600,
            _   => value * 60
        };

        // Generate ISO 8601 duration string (e.g., PT5M, PT30S)
        string isoDuration = unit switch {
            "s" => $"PT{value}S",
            "h" => $"PT{value}H",
            _   => $"PT{value}M"
        };

        return new SearchResult {
            Title = $"Set timer for {value} {label}",
            Subtitle = "Open Windows Clock with this duration",
            Category = ResultCategory.Timer,
            ActionPath = isoDuration
        };
    }

    private async Task<List<SearchResult>> GetCalendarEventsAsync()
    {
        try 
        {
            var results = new List<SearchResult>();
            
            // WinRT Appointments API
            var store = await Windows.ApplicationModel.Appointments.AppointmentManager.RequestStoreAsync(
                Windows.ApplicationModel.Appointments.AppointmentStoreAccessType.AllCalendarsReadOnly);
            
            if (store == null) return results;

            var options = new Windows.ApplicationModel.Appointments.FindAppointmentsOptions();
            options.MaxCount = 3;
            options.FetchProperties.Add(Windows.ApplicationModel.Appointments.AppointmentProperties.Subject);
            options.FetchProperties.Add(Windows.ApplicationModel.Appointments.AppointmentProperties.StartTime);
            options.FetchProperties.Add(Windows.ApplicationModel.Appointments.AppointmentProperties.Location);

            var now = DateTimeOffset.Now;
            var appointments = await store.FindAppointmentsAsync(now, TimeSpan.FromDays(7), options);

            foreach (var appt in appointments)
            {
                results.Add(new SearchResult {
                    Title = appt.Subject,
                    Subtitle = $"{appt.StartTime:MMM dd, HH:mm} — {appt.Location}",
                    Category = ResultCategory.Calendar,
                    ActionPath = "" // No specific action yet
                });
            }

            return results;
        }
        catch 
        { 
            // Return empty list if access denied or unsupported
            return []; 
        }
    }
}
