using Microsoft.AspNetCore.Identity;

namespace TodoAPI.Models;

public class AppUser : IdentityUser
{
    public ICollection<Todo> Todos { get; set; } = new List<Todo>();
}