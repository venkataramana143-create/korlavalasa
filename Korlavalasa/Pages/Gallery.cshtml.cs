using Korlavalasa.Data;
using Korlavalasa.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Korlavalasa.Pages
{
    public class GalleryModel : PageModel
    {
        private readonly AppDbContext _context;
        public List<GalleryImage> GalleryImages { get; set; } = new List<GalleryImage>();

        [BindProperty(SupportsGet = true)]
        public string Category { get; set; } = "Temple"; // Changed from "All" to "Temple"

        public GalleryModel(AppDbContext context)
        {
            _context = context;
        }

        public async Task OnGetAsync()
        {
            var query = _context.Gallery.AsQueryable();

            // If no category is specified in URL, use default "Temple"
            if (string.IsNullOrEmpty(Request.Query["category"]))
            {
                Category = "Temple";
            }

            if (Category != "All")
            {
                query = query.Where(g => g.Category == Category);
            }

            GalleryImages = await query
                .OrderByDescending(g => g.UploadDate)
                .ToListAsync();
        }
    }
}