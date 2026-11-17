namespace NexusCommunication.Helpers;

/// <summary>
/// Saves, validates and deletes product pictures (wwwroot/uploads/products).
/// The file type is checked from the real file header (not from the file name), the size is limited to 2 MB
/// and the stored file gets a random name, so nothing the user types ends up in the file path.
/// </summary>
public static class ProductImageStorage
{
    public const long MaxBytes = 2 * 1024 * 1024;
    private const string WebFolder = "/uploads/products/";

    /// <summary>Returns an error message, or null when there is no file or the file is a valid image.</summary>
    public static async Task<string?> ValidateAsync(IFormFile? file)
    {
        if (file == null || file.Length == 0) return null;   // picture is optional
        if (file.Length > MaxBytes) return "Image must be 2 MB or smaller.";
        if (await DetectExtensionAsync(file) == null) return "Only JPG, PNG, GIF or WEBP images are allowed.";
        return null;
    }

    /// <summary>Saves the (already validated) file and returns its web path.</summary>
    public static async Task<string> SaveAsync(IFormFile file, IWebHostEnvironment env)
    {
        var ext = await DetectExtensionAsync(file) ?? throw new InvalidOperationException("Unsupported image type.");
        var dir = Path.Combine(env.WebRootPath, "uploads", "products");
        Directory.CreateDirectory(dir);
        var name = Guid.NewGuid().ToString("N") + ext;
        await using (var fs = new FileStream(Path.Combine(dir, name), FileMode.Create))
            await file.CopyToAsync(fs);
        return WebFolder + name;
    }

    /// <summary>Deletes a previously saved picture (ignored when the path is empty or not one of ours).</summary>
    public static void Delete(string? webPath, IWebHostEnvironment env)
    {
        if (string.IsNullOrEmpty(webPath) || !webPath.StartsWith(WebFolder)) return;
        var full = Path.Combine(env.WebRootPath, "uploads", "products", Path.GetFileName(webPath));
        try { if (File.Exists(full)) File.Delete(full); } catch { /* a locked file must not break the request */ }
    }

    /// <summary>Looks at the first bytes of the file: JPG, PNG, GIF or WEBP, otherwise null.</summary>
    private static async Task<string?> DetectExtensionAsync(IFormFile file)
    {
        var h = new byte[12];
        await using var s = file.OpenReadStream();
        int n = await s.ReadAtLeastAsync(h, h.Length, throwOnEndOfStream: false);
        if (n < 12) return null;
        if (h[0] == 0xFF && h[1] == 0xD8 && h[2] == 0xFF) return ".jpg";
        if (h[0] == 0x89 && h[1] == 0x50 && h[2] == 0x4E && h[3] == 0x47 && h[4] == 0x0D && h[5] == 0x0A && h[6] == 0x1A && h[7] == 0x0A) return ".png";
        if (h[0] == 'G' && h[1] == 'I' && h[2] == 'F' && h[3] == '8' && (h[4] == '7' || h[4] == '9') && h[5] == 'a') return ".gif";
        if (h[0] == 'R' && h[1] == 'I' && h[2] == 'F' && h[3] == 'F' && h[8] == 'W' && h[9] == 'E' && h[10] == 'B' && h[11] == 'P') return ".webp";
        return null;
    }
}
