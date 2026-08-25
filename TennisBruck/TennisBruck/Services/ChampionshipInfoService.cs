using System.Text.Json;
using TennisBruck.Dto;

namespace TennisBruck.Services;

public class ChampionshipInfoService
{
    private readonly IWebHostEnvironment _env;
    private readonly string _dataDir;
    private readonly string _jsonPath;

    public ChampionshipInfoService(IWebHostEnvironment env)
    {
        _env = env;
        var webRoot = _env.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
        _dataDir = Path.Combine(webRoot, "uploads", "championship");
        _jsonPath = Path.Combine(_dataDir, "championship_info.json");
    }

    public async Task<ChampionshipInfo> GetInfoAsync()
    {
        try
        {
            if (File.Exists(_jsonPath))
            {
                var json = await File.ReadAllTextAsync(_jsonPath);
                var info = JsonSerializer.Deserialize<ChampionshipInfo>(json);
                if (info != null) return info;
            }
        }
        catch (Exception)
        {
            // fallback
        }
        return new ChampionshipInfo();
    }

    public async Task SaveInfoAsync(string? text, IFormFile? pdfFile, bool deletePdf = false, IFormFile? imageFile = null, bool deleteImage = false)
    {
        if (!Directory.Exists(_dataDir))
        {
            Directory.CreateDirectory(_dataDir);
        }

        var currentInfo = await GetInfoAsync();
        currentInfo.Text = text?.Trim();
        currentInfo.LastUpdated = DateTime.Now;

        // Handle PDF removal
        if (deletePdf && !string.IsNullOrEmpty(currentInfo.PdfFileName))
        {
            var oldFilePath = Path.Combine(_dataDir, Path.GetFileName(currentInfo.PdfFileName));
            if (File.Exists(oldFilePath))
            {
                try { File.Delete(oldFilePath); } catch { /* ignore */ }
            }
            currentInfo.PdfFileName = null;
            currentInfo.PdfOriginalName = null;
        }

        // Handle PDF upload
        if (pdfFile != null && pdfFile.Length > 0)
        {
            if (!string.IsNullOrEmpty(currentInfo.PdfFileName))
            {
                var oldFilePath = Path.Combine(_dataDir, Path.GetFileName(currentInfo.PdfFileName));
                if (File.Exists(oldFilePath))
                {
                    try { File.Delete(oldFilePath); } catch { /* ignore */ }
                }
            }

            var safeFileName = $"regeln_{Guid.NewGuid():N}.pdf";
            var targetFilePath = Path.Combine(_dataDir, safeFileName);
            using (var stream = new FileStream(targetFilePath, FileMode.Create))
            {
                await pdfFile.CopyToAsync(stream);
            }

            currentInfo.PdfFileName = $"/uploads/championship/{safeFileName}";
            currentInfo.PdfOriginalName = Path.GetFileName(pdfFile.FileName);
        }

        // Handle Image removal
        if (deleteImage && !string.IsNullOrEmpty(currentInfo.ImageFileName))
        {
            var oldFilePath = Path.Combine(_dataDir, Path.GetFileName(currentInfo.ImageFileName));
            if (File.Exists(oldFilePath))
            {
                try { File.Delete(oldFilePath); } catch { /* ignore */ }
            }
            currentInfo.ImageFileName = null;
            currentInfo.ImageOriginalName = null;
        }

        // Handle Image upload
        if (imageFile != null && imageFile.Length > 0)
        {
            if (!string.IsNullOrEmpty(currentInfo.ImageFileName))
            {
                var oldFilePath = Path.Combine(_dataDir, Path.GetFileName(currentInfo.ImageFileName));
                if (File.Exists(oldFilePath))
                {
                    try { File.Delete(oldFilePath); } catch { /* ignore */ }
                }
            }

            var extension = Path.GetExtension(imageFile.FileName).ToLowerInvariant();
            if (string.IsNullOrEmpty(extension)) extension = ".jpg";
            var safeImageName = $"bild_{Guid.NewGuid():N}{extension}";
            var targetImagePath = Path.Combine(_dataDir, safeImageName);
            using (var stream = new FileStream(targetImagePath, FileMode.Create))
            {
                await imageFile.CopyToAsync(stream);
            }

            currentInfo.ImageFileName = $"/uploads/championship/{safeImageName}";
            currentInfo.ImageOriginalName = Path.GetFileName(imageFile.FileName);
        }

        var json = JsonSerializer.Serialize(currentInfo, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(_jsonPath, json);
    }
}
