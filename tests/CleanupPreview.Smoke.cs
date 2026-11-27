using WinTweaker.Services;

var fileName = $"WinTweaker-CleanupPreview-{Guid.NewGuid():N}.tmp";
var filePath = Path.Combine(Path.GetTempPath(), fileName);
var expectedBytes = 1234L;

try
{
    await File.WriteAllBytesAsync(filePath, new byte[expectedBytes]);
    var originalBytes = await File.ReadAllBytesAsync(filePath);
    var originalWriteTime = File.GetLastWriteTimeUtc(filePath);

    var previews = CleanupService.PreviewSafeCleanup();
    var userTemp = previews.Single(x => x.Name == "Временные файлы пользователя");

    if (userTemp.EstimatedBytes < expectedBytes)
        throw new InvalidOperationException($"Expected at least {expectedBytes} bytes, got {userTemp.EstimatedBytes}.");

    if (!File.Exists(filePath))
        throw new InvalidOperationException("Preview deleted the temporary test file.");

    var previewBytes = await File.ReadAllBytesAsync(filePath);
    if (!originalBytes.SequenceEqual(previewBytes))
        throw new InvalidOperationException("Preview changed the temporary test file.");

    if (File.GetLastWriteTimeUtc(filePath) != originalWriteTime)
        throw new InvalidOperationException("Preview changed the temporary test file timestamp.");

    if (previews.Count != 7)
        throw new InvalidOperationException($"Expected seven batch categories, got {previews.Count}.");

    var delivery = previews.Single(x => x.Name == "Delivery Optimization");
    if (delivery.EstimateAvailable || string.IsNullOrWhiteSpace(delivery.Message))
        throw new InvalidOperationException("Delivery Optimization must be explicitly marked as not estimated.");

    Console.WriteLine("PASS cleanup preview reports size without deleting test file");
}
finally
{
    if (File.Exists(filePath))
        File.Delete(filePath);
}
