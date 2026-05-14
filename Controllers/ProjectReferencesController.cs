using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AdalyaSolarAPI.Data;
using AdalyaSolarAPI.Models;

namespace AdalyaSolarAPI.Controllers;

[ApiController]
[Route("api/projects")]
public class ProjectReferencesController : ControllerBase
{
    private readonly AppDbContext _db;
    public ProjectReferencesController(AppDbContext db) => _db = db;

    // Public: published projects
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var projects = await _db.ProjectReferences
            .Where(p => p.IsPublished)
            .OrderBy(p => p.SortOrder).ThenBy(p => p.Id)
            .Select(p => new { p.Id, p.Title, p.City, p.Type, p.Capacity, p.Panels, p.Year, p.Description, p.ImageUrl, p.Savings })
            .ToListAsync();
        return Ok(projects);
    }

    // Admin: all projects
    [HttpGet("admin")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> AdminGetAll()
    {
        var projects = await _db.ProjectReferences
            .OrderBy(p => p.SortOrder).ThenBy(p => p.Id)
            .ToListAsync();
        return Ok(projects);
    }

    [HttpPost("admin")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> Create([FromBody] UpsertProjectDto dto)
    {
        var maxSort = await _db.ProjectReferences.MaxAsync(p => (int?)p.SortOrder) ?? 0;
        var p = new ProjectReference
        {
            Title = dto.Title, City = dto.City, Type = dto.Type,
            Capacity = dto.Capacity, Panels = dto.Panels, Year = dto.Year,
            Description = dto.Description, ImageUrl = dto.ImageUrl, Savings = dto.Savings,
            IsPublished = dto.IsPublished, SortOrder = maxSort + 1,
        };
        _db.ProjectReferences.Add(p);
        await _db.SaveChangesAsync();
        return Ok(p);
    }

    [HttpPut("admin/{id}")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> Update(int id, [FromBody] UpsertProjectDto dto)
    {
        var p = await _db.ProjectReferences.FindAsync(id);
        if (p == null) return NotFound();
        p.Title = dto.Title; p.City = dto.City; p.Type = dto.Type;
        p.Capacity = dto.Capacity; p.Panels = dto.Panels; p.Year = dto.Year;
        p.Description = dto.Description; p.ImageUrl = dto.ImageUrl; p.Savings = dto.Savings;
        p.IsPublished = dto.IsPublished; p.SortOrder = dto.SortOrder;
        await _db.SaveChangesAsync();
        return Ok(p);
    }

    [HttpDelete("admin/{id}")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> Delete(int id)
    {
        var p = await _db.ProjectReferences.FindAsync(id);
        if (p == null) return NotFound();
        _db.ProjectReferences.Remove(p);
        await _db.SaveChangesAsync();
        return Ok();
    }

    public record UpsertProjectDto(
        string Title, string City, string Type,
        string Capacity, int Panels, string Year,
        string Description, string ImageUrl, string Savings,
        bool IsPublished, int SortOrder);
}
