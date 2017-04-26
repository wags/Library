using System.ComponentModel.DataAnnotations;

namespace Library.API.Models
{
    public abstract class BookForManipulationDto
    {
        [Required(ErrorMessage = "You should include a title.")]
        [MaxLength(100, ErrorMessage = "The title may not contain more than 100 characters.")]
        public string Title { get; set; }

        [MaxLength(500, ErrorMessage = "The description may not have more than 500 characters.")]
        public string Description { get; set; }
    }
}
