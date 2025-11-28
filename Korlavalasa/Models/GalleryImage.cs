using System.ComponentModel.DataAnnotations;

namespace Korlavalasa.Models
{
    public class GalleryImage
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Title is required")]
        [StringLength(100, ErrorMessage = "Title cannot exceed 100 characters")]
        public string Title { get; set; } = string.Empty;

        [Required(ErrorMessage = "Image path is required")]
        [Display(Name = "Image Path")]
        public string ImagePath { get; set; } = string.Empty;

        [Display(Name = "Upload Date")]
       public DateTime UploadDate { get; set; } = DateTime.UtcNow;


        [Required(ErrorMessage = "Category is required")]
        public string Category { get; set; } = "General";

        [StringLength(500, ErrorMessage = "Description cannot exceed 500 characters")]
        public string? Description { get; set; }
    }
}