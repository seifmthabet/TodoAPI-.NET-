using System.ComponentModel.DataAnnotations;

namespace TodoAPI.DTOs;

public class TodoDTO
{
    [Required, MinLength(3)]
    public string Title {get; set;} = string.Empty;

    public string? Description { get; set; } = string.Empty;
    public bool IsCompleted {get; set;}
}