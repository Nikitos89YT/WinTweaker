using WinTweaker.Services;

static void Require(bool condition, string message)
{
    if (!condition) throw new Exception(message);
}

static void JoinByInstanceId()
{
    var snapshot = DriverInventoryService.Load(
        () => [new(@"PCI\Example", "Matched", true, 0), new(null, "No ID", true, 0), new(@"PCI\Missing", "Missing", true, 0)],
        () => [new(@"pci\example", "Provider", "1.2", "one.inf"), new(null, "Other", "9.9", "two.inf", "Orphan")]);
    Require(snapshot.Rows.Count == 4, "Expected three devices and one unmatched driver");
    Require(snapshot.Rows.Single(r => r.Device == "Matched").Version == "1.2", "IDs must match case-insensitively");
    Require(snapshot.Rows.Single(r => r.Device == "No ID").Version is null, "Null IDs must not join");
    Require(snapshot.Rows.Single(r => r.Device == "Missing").Version is null, "Missing driver must not join");
    var orphan = snapshot.Rows.Single(r => r.Device == "Orphan");
    Require(orphan.Version == "9.9" && orphan.Status == "Статус неизвестен" && !orphan.HasProblem, "Unmatched driver must have unknown status");
    Require(snapshot.Rows.Single(r => r.Device == "Missing").Status == "Исправно", "Missing driver must not invent a problem");
}

static void ProjectWindowsStatus()
{
    (bool? present, uint? code, string status, bool problem)[] cases = [
        (true, 0, "Исправно", false), (true, 22, "Отключено", true),
        (true, 28, "Драйверы не установлены", true), (true, 99, "Код Windows: 99", true),
        (false, 28, "Отсутствует", false), (null, null, "Статус неизвестен", false)];
    foreach (var (present, code, status, problem) in cases)
    {
        var snapshot = DriverInventoryService.Load(() => [new("id", "Device", present, code)], () => []);
        Require(snapshot.Rows.Count == 1 && snapshot.Rows[0].Status == status && snapshot.Rows[0].HasProblem == problem, $"Unexpected status for {present}/{code}");
    }
}

static void KeepPartialResults()
{
    var devicesOnly = DriverInventoryService.Load(() => [new("id", "Device", true, 0)], () => throw new UnauthorizedAccessException());
    Require(devicesOnly.Warnings.Any(w => w.Contains("Win32_PnPSignedDriver")), "Driver warning missing");
    Require(devicesOnly.Rows.Count == 1 && devicesOnly.Rows[0].Device == "Device" && devicesOnly.Rows[0].Status == "Исправно", "Device results lost");
    var driversOnly = DriverInventoryService.Load(() => throw new UnauthorizedAccessException(), () => [new("id", "Provider", "1", "x.inf", "Driver")]);
    Require(driversOnly.Warnings.Any(w => w.Contains("Win32_PnPEntity")), "Device warning missing");
    Require(driversOnly.Rows.Count == 1 && driversOnly.Rows[0].Device == "Driver" && driversOnly.Rows[0].Status == "Статус неизвестен" && !driversOnly.Rows[0].HasProblem, "Driver results lost or status invented");
}

JoinByInstanceId();
ProjectWindowsStatus();
KeepPartialResults();
Console.WriteLine("PASS Driver Manager inventory");
await SharedExportSurvivesPageReentry();
Console.WriteLine("PASS Driver Manager shared export state");

static async Task SharedExportSurvivesPageReentry()
{
    var coordinator = typeof(DriverExportService).Assembly.GetType("WinTweaker.Pages.DriverExportCoordinator");
    Require(coordinator is not null, "Shared export coordinator must survive page re-entry");
    var start = coordinator!.GetMethod("TryStart", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)!;
    var current = coordinator.GetProperty("CurrentTask", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)!;
    var pending = new TaskCompletionSource<DriverExportResult>(TaskCreationOptions.RunContinuationsAsynchronously);
    int calls = 0;
    Func<Task<DriverExportResult>> runner = () => { calls++; return pending.Task; };
    var first = (Task<DriverExportResult>?)start.Invoke(null, [runner]);
    Require(first is not null && ReferenceEquals(first, current.GetValue(null)), "Re-entered page must see the running task");
    Require(start.Invoke(null, [runner]) is null && calls == 1, "A second page must not start another export");
    var result = new DriverExportResult(DriverExportStatus.Completed, "fixture path", 0, "fixture detail");
    pending.SetResult(result);
    Require(await first! == result && await (Task<DriverExportResult>)current.GetValue(null)! == result, "Re-entered page must see the completed result");
}

var root = Path.Combine(Path.GetTempPath(), "DriverManager-" + Guid.NewGuid().ToString("N"));
var parent = Path.Combine(root, "Копия & backup");
Directory.CreateDirectory(parent);
try
{
    await BuildFixedCommand(parent);
    await ClassifyResults(parent);
    await IsolateAttempts(parent);
    Console.WriteLine("PASS Driver Manager export");
}
finally
{
    Directory.Delete(root, true);
}

static async Task BuildFixedCommand(string parent)
{
    string? destination = null;
    var result = await DriverExportService.ExportAsync(parent, true, info =>
    {
        destination = info.ArgumentList[2];
        Require(info.FileName == Path.Combine(Environment.SystemDirectory, "pnputil.exe"), "Incorrect executable");
        Require(!info.UseShellExecute, "Shell execution must be disabled");
        Require(info.ArgumentList.SequenceEqual(new[] { "/export-driver", "*", info.ArgumentList[2] }), "Incorrect arguments");
        Require(Path.GetDirectoryName(info.ArgumentList[2]) == parent, "Destination must be under parent");
        File.WriteAllText(Path.Combine(info.ArgumentList[2], "driver.inf"), "fixture");
        return Task.FromResult((0, "ok"));
    });
    Require(result.Status == DriverExportStatus.Completed && result.Destination == destination, "Expected completed export at the runner destination");
}

static async Task ClassifyResults(string parent)
{
    var count = Directory.GetDirectories(parent).Length;
    var noAdmin = await DriverExportService.ExportAsync(parent, false, _ => throw new Exception("runner called"));
    Require(noAdmin.Status == DriverExportStatus.NeedsAdministrator && Directory.GetDirectories(parent).Length == count, "Admin check must precede creation and execution");
    var invalid = await DriverExportService.ExportAsync(Path.Combine(parent, "missing"), true, _ => throw new Exception("runner called"));
    Require(invalid.Status == DriverExportStatus.Failed, "Invalid parent must fail");
    var denied = await DriverExportService.ExportAsync(parent, true, _ => throw new UnauthorizedAccessException("denied"));
    Require(denied.Status == DriverExportStatus.Failed && denied.Destination is not null, "Denied launch must fail and retain destination");
    var failed = await DriverExportService.ExportAsync(parent, true, _ => Task.FromResult((1, "error")));
    Require(failed.Status == DriverExportStatus.Failed && failed.ExitCode == 1, "Nonzero empty export must fail");
    var partial = await DriverExportService.ExportAsync(parent, true, info =>
    {
        var package = Directory.CreateDirectory(Path.Combine(info.ArgumentList[2], "package"));
        File.WriteAllText(Path.Combine(package.FullName, "driver.sys"), "fixture");
        return Task.FromResult((1, "error"));
    });
    Require(partial.Status == DriverExportStatus.Partial, "Nonzero export with any file must be partial");
    var thrown = await DriverExportService.ExportAsync(parent, true, info =>
    {
        File.WriteAllText(Path.Combine(info.ArgumentList[2], "driver.sys"), "fixture");
        throw new IOException("runner failed after writing");
    });
    Require(thrown.Status == DriverExportStatus.Partial && File.Exists(Path.Combine(thrown.Destination!, "driver.sys")), "Runner exception with files must be partial and preserve files");
    var empty = await DriverExportService.ExportAsync(parent, true, _ => Task.FromResult((0, "ok")));
    Require(empty.Status == DriverExportStatus.NoFiles, "Zero exit without INF must be NoFiles");
    var nonInf = await DriverExportService.ExportAsync(parent, true, info =>
    {
        File.WriteAllText(Path.Combine(info.ArgumentList[2], "driver.sys"), "fixture");
        return Task.FromResult((0, "ok"));
    });
    Require(nonInf.Status == DriverExportStatus.NoFiles, "Zero exit with only non-INF files must be NoFiles");
    var complete = await DriverExportService.ExportAsync(parent, true, info =>
    {
        var package = Directory.CreateDirectory(Path.Combine(info.ArgumentList[2], "package"));
        File.WriteAllText(Path.Combine(package.FullName, "driver.INF"), "fixture");
        return Task.FromResult((0, "ok"));
    });
    Require(complete.Status == DriverExportStatus.Completed && complete.ExitCode == 0, "Zero exit with INF must complete");
}

static async Task IsolateAttempts(string parent)
{
    var first = await DriverExportService.ExportAsync(parent, true, info =>
    {
        File.WriteAllText(Path.Combine(info.ArgumentList[2], "retained.sys"), "fixture");
        return Task.FromResult((1, "error"));
    });
    var second = await DriverExportService.ExportAsync(parent, true, _ => Task.FromResult((0, "ok")));
    Require(first.Status == DriverExportStatus.Partial && second.Status == DriverExportStatus.NoFiles, "Attempts must classify independently");
    Require(first.Destination != second.Destination && File.Exists(Path.Combine(first.Destination!, "retained.sys")), "Attempts must preserve separate files");
}
