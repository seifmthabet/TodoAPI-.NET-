using AutoMapper;
using TodoAPI.Models;
using TodoAPI.DTOs;

namespace TodoAPI.Mapping;

public class MappingProfile : Profile
{
    public MappingProfile()
    {
        CreateMap<TodoDTO, Todo>();
        CreateMap<Todo, TodoReadDTO>();   
    }
}