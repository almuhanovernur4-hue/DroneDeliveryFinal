using System;
using System.IO;
using System.Text.Json;
using System.Collections.Generic;

public static class ResourceStreamHandler
{
    public static Result<List<BatteryItem>, string> LoadDataSafely(string filePath)
    {
        try
        {
            if (!File.Exists(filePath))
                return new Result<List<BatteryItem>, string>.Fail("File not found.");

            using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read);
            using var reader = new StreamReader(fileStream);

            string jsonString = reader.ReadToEnd();
            if (string.IsNullOrWhiteSpace(jsonString))
                return new Result<List<BatteryItem>, string>.Fail("Malformed or empty file content.");

            var items = JsonSerializer.Deserialize<List<BatteryItem>>(jsonString);
            return items != null
                ? new Result<List<BatteryItem>, string>.Ok(items)
                : new Result<List<BatteryItem>, string>.Fail("Deserialization returned null.");
        }
        jsonException =>
        catch (Exception ex)
        {
            return new Result<List<BatteryItem>, string>.Fail($"Resource read error: {ex.Message}");
        }
    }
}