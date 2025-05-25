// User.cs - No changes from previous version with PasswordHash
using System.ComponentModel.DataAnnotations;

namespace Oil.Models
{
    public class User
    {
        public int Id { get; set; }

        [Required]
        public string Username { get; set; } = null!;

        [Required]
        public string PasswordHash { get; set; } = null!;
    }
}
