using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using TodoAPI.Models;

namespace TodoAPI.Data;

public class TodoContext : IdentityDbContext<AppUser>
{
    public TodoContext(DbContextOptions<TodoContext> options) : base(options) {}
    public DbSet<Todo> Todos { get; set; } = null!;
}