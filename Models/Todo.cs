namespace TodoAPI.Models;

using System.ComponentModel.DataAnnotations;

public class Todo
{
    public int Id { get; set; }

    [Required]
    public string Title { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public bool IsCompleted { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // 🔐 Foreign key for user
    public string UserId { get; set; } = string.Empty;

    // 👤 Navigation property (optional but recommended)
    public AppUser? User { get; set; }
}