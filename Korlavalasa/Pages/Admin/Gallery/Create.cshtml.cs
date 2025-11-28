using Korlavalasa.Data;
using Korlavalasa.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Korlavalasa.Pages.Admin.Gallery
{
    [Authorize(Roles = "Admin")]
    public class CreateModel : PageModel
    {
        private readonly AppDbContext _context;
        private readonly IWebHostEnvironment _environment;
        private readonly ILogger<CreateModel> _logger;

        [BindProperty]
        public GalleryImage GalleryImage { get; set; } = new GalleryImage();

        // Remove the List<IFormFile> binding and use IFormCollection instead
        // [BindProperty] - REMOVE THIS LINE
        // public List<IFormFile> ImageFiles { get; set; } = new List<IFormFile>();

        public CreateModel(AppDbContext context, IWebHostEnvironment environment, ILogger<CreateModel> logger)
        {
            _context = context;
            _environment = environment;
            _logger = logger;
        }

        public IActionResult OnGet()
        {
            GalleryImage.Category = "General";
            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            try
            {
                _logger.LogInformation("=== STARTING MULTIPLE IMAGE UPLOAD PROCESS ===");

                // Clear existing model state
                ModelState.Clear();

                // Get files from Form collection directly (more reliable for multiple files)
                var formFiles = Request.Form.Files;
                _logger.LogInformation($"Found {formFiles.Count} files in form collection");

                // Basic validation
                if (formFiles.Count == 0)
                {
                    ModelState.AddModelError("", "Please select at least one image file.");
                    ViewData["Error"] = "Please select at least one image file.";
                    return Page();
                }

                if (string.IsNullOrWhiteSpace(GalleryImage.Category))
                {
                    ModelState.AddModelError("GalleryImage.Category", "Category is required.");
                    ViewData["Error"] = "Category is required.";
                    return Page();
                }

                // Filter out null files and files with zero length
                var validFiles = formFiles.Where(f => f != null && f.Length > 0).ToList();

                if (!validFiles.Any())
                {
                    ModelState.AddModelError("", "No valid image files were selected.");
                    ViewData["Error"] = "No valid image files were selected.";
                    return Page();
                }

                _logger.LogInformation($"Processing {validFiles.Count} valid file(s)");

                // Validate each file
                var validExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
                var maxFileSize = 5 * 1024 * 1024; // 5MB
                var validatedFiles = new List<(IFormFile file, string extension)>();

                foreach (var imageFile in validFiles)
                {
                    try
                    {
                        var fileName = imageFile.FileName;
                        var fileExtension = Path.GetExtension(fileName)?.ToLower();

                        _logger.LogInformation($"Validating file: {fileName}, Size: {imageFile.Length} bytes");

                        if (string.IsNullOrEmpty(fileExtension) || !validExtensions.Contains(fileExtension))
                        {
                            _logger.LogWarning($"Invalid file extension: {fileExtension} for file: {fileName}");
                            ModelState.AddModelError("", $"File '{fileName}' is not a supported image format.");
                            continue;
                        }

                        if (imageFile.Length > maxFileSize)
                        {
                            _logger.LogWarning($"File too large: {fileName} - {imageFile.Length} bytes");
                            ModelState.AddModelError("", $"File '{fileName}' exceeds the 5MB size limit.");
                            continue;
                        }

                        if (imageFile.Length == 0)
                        {
                            _logger.LogWarning($"Empty file: {fileName}");
                            ModelState.AddModelError("", $"File '{fileName}' is empty.");
                            continue;
                        }

                        validatedFiles.Add((imageFile, fileExtension));
                        _logger.LogInformation($"File validated: {fileName}");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, $"Error validating file: {imageFile.FileName}");
                        ModelState.AddModelError("", $"Error validating file '{imageFile.FileName}': {ex.Message}");
                    }
                }

                if (!validatedFiles.Any())
                {
                    _logger.LogWarning("No files passed validation");
                    ViewData["Error"] = "No valid image files were selected. Please check file formats and sizes.";
                    return Page();
                }

                // Create uploads directory
                var uploadsFolder = Path.Combine(_environment.WebRootPath, "uploads", "gallery");
                _logger.LogInformation($"Uploads folder path: {uploadsFolder}");

                try
                {
                    if (!Directory.Exists(uploadsFolder))
                    {
                        _logger.LogInformation("Creating uploads directory...");
                        Directory.CreateDirectory(uploadsFolder);
                    }

                    // Test directory permissions
                    var testFilePath = Path.Combine(uploadsFolder, $"test_{Guid.NewGuid()}.txt");
                    await System.IO.File.WriteAllTextAsync(testFilePath, "test");
                    if (System.IO.File.Exists(testFilePath))
                    {
                        System.IO.File.Delete(testFilePath);
                        _logger.LogInformation("Directory permissions test passed");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Directory permission test failed");
                    ViewData["Error"] = "Upload directory is not accessible. Please check permissions.";
                    return Page();
                }

                var uploadedImages = new List<GalleryImage>();
                var uploadErrors = new List<string>();
                var successCount = 0;

                // Process each validated file
                foreach (var (imageFile, fileExtension) in validatedFiles)
                {
                    string uniqueFileName = null;
                    string filePath = null;

                    try
                    {
                        _logger.LogInformation($"Processing file: {imageFile.FileName}");

                        // Generate unique filename
                        uniqueFileName = $"{Guid.NewGuid()}{fileExtension}";
                        filePath = Path.Combine(uploadsFolder, uniqueFileName);

                        _logger.LogInformation($"Saving file to: {filePath}");

                        // Save file to disk
                        using (var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None))
                        {
                            await imageFile.CopyToAsync(fileStream);
                        }

                        // Verify file was created and has content
                        if (!System.IO.File.Exists(filePath))
                        {
                            throw new Exception("File was not created on disk");
                        }

                        var fileInfo = new FileInfo(filePath);
                        if (fileInfo.Length == 0)
                        {
                            throw new Exception("File was created but is empty");
                        }

                        _logger.LogInformation($"File saved successfully: {filePath}, Size: {fileInfo.Length} bytes");

                        // Create gallery image entity
                        var galleryImage = new GalleryImage
                        {
                            Title = SanitizeFileName(Path.GetFileNameWithoutExtension(imageFile.FileName)),
                            Description = string.Empty,
                            Category = GalleryImage.Category?.Trim() ?? "General",
                            ImagePath = $"/uploads/gallery/{uniqueFileName}",
                            UploadDate = DateTime.Now
                        };

                        uploadedImages.Add(galleryImage);
                        successCount++;

                        _logger.LogInformation($"Successfully processed: {imageFile.FileName} -> {galleryImage.Title}");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, $"Error processing file: {imageFile.FileName}");

                        // Clean up failed file
                        if (filePath != null && System.IO.File.Exists(filePath))
                        {
                            try
                            {
                                System.IO.File.Delete(filePath);
                                _logger.LogInformation($"Cleaned up failed file: {filePath}");
                            }
                            catch (Exception cleanupEx)
                            {
                                _logger.LogError(cleanupEx, $"Failed to clean up file: {filePath}");
                            }
                        }

                        uploadErrors.Add($"'{imageFile.FileName}': {ex.Message}");
                    }
                }

                // Save to database if we have successful uploads
                if (uploadedImages.Any())
                {
                    try
                    {
                        _logger.LogInformation($"Saving {uploadedImages.Count} images to database...");

                        // Save in batches to avoid large transactions
                        _context.Gallery.AddRange(uploadedImages);
                        await _context.SaveChangesAsync();

                        _logger.LogInformation($"Successfully saved {uploadedImages.Count} images to database");

                        // Prepare success message
                        var successMessage = $"Successfully uploaded {successCount} image(s) to the gallery!";
                        if (uploadErrors.Any())
                        {
                            successMessage += $"\n\nHowever, {uploadErrors.Count} file(s) failed:\n• " + string.Join("\n• ", uploadErrors.Take(5));
                            if (uploadErrors.Count > 5)
                            {
                                successMessage += $"\n• ... and {uploadErrors.Count - 5} more";
                            }
                        }

                        TempData["SuccessMessage"] = successMessage;
                        _logger.LogInformation("=== UPLOAD PROCESS COMPLETED SUCCESSFULLY ===");
                    }
                    catch (DbUpdateException dbEx)
                    {
                        _logger.LogError(dbEx, "Database error while saving images");

                        var realError =
                            dbEx.InnerException?.Message       // actual PostgreSQL error
                            ?? dbEx.Message;                   // fallback

                        ViewData["Error"] = realError;
                        return Page();
                    }

                }
                else
                {
                    _logger.LogWarning("No images were successfully processed");
                    ViewData["Error"] = "No images were successfully uploaded. Please try again.";
                    return Page();
                }

                return RedirectToPage("./Index");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "=== CRITICAL ERROR IN UPLOAD PROCESS ===");
                ViewData["Error"] = $"A critical error occurred: {ex.Message}. Please try again or contact support.";
                return Page();
            }
        }

        private string SanitizeFileName(string fileName)
        {
            if (string.IsNullOrEmpty(fileName))
                return "Untitled";

            // Remove or replace invalid characters
            var invalidChars = Path.GetInvalidFileNameChars();
            var sanitized = new string(fileName.Select(ch => invalidChars.Contains(ch) ? '_' : ch).ToArray());

            // Limit length
            return sanitized.Length > 90 ? sanitized.Substring(0, 90) : sanitized;
        }
    }
}