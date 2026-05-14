using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AdalyaSolarAPI.Data;
using AdalyaSolarAPI.Models;

namespace AdalyaSolarAPI.Controllers;

[ApiController]
[Route("api/blogs")]
public class BlogsController : ControllerBase
{
    private readonly AppDbContext _db;
    public BlogsController(AppDbContext db) => _db = db;

    // ── Public ────────────────────────────────────────────────────────────────

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] string? category)
    {
        var query = _db.BlogPosts.Where(b => b.IsPublished);
        if (!string.IsNullOrEmpty(category))
            query = query.Where(b => b.Category == category);

        var posts = await query
            .OrderByDescending(b => b.Date)
            .Select(b => new {
                b.Id, b.Slug, b.Title, b.Excerpt, b.Category,
                b.Author, b.AuthorTitle, b.Date, b.ReadTime, b.ImageUrl, b.Tags
            })
            .ToListAsync();
        return Ok(posts);
    }

    [HttpGet("{slug}")]
    public async Task<IActionResult> GetBySlug(string slug)
    {
        var post = await _db.BlogPosts
            .Where(b => b.Slug == slug && b.IsPublished)
            .Select(b => new {
                b.Id, b.Slug, b.Title, b.Excerpt, b.Content, b.Category,
                b.Author, b.AuthorTitle, b.Date, b.ReadTime, b.ImageUrl, b.Tags
            })
            .FirstOrDefaultAsync();
        if (post == null) return NotFound();
        return Ok(post);
    }

    // ── Admin ─────────────────────────────────────────────────────────────────

    [HttpGet("admin/all")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> AdminGetAll()
    {
        var posts = await _db.BlogPosts
            .OrderByDescending(b => b.CreatedAt)
            .Select(b => new {
                b.Id, b.Slug, b.Title, b.Excerpt, b.Category,
                b.Author, b.Date, b.ReadTime, b.ImageUrl, b.IsPublished, b.CreatedAt
            })
            .ToListAsync();
        return Ok(posts);
    }

    [HttpPost]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> Create([FromBody] BlogPostDto dto)
    {
        var slug = string.IsNullOrWhiteSpace(dto.Slug) ? GenerateSlug(dto.Title) : dto.Slug.Trim();
        if (await _db.BlogPosts.AnyAsync(b => b.Slug == slug))
            return BadRequest(new { message = "Bu slug zaten kullanımda." });

        var post = new BlogPost
        {
            Slug = slug,
            Title = dto.Title,
            Excerpt = dto.Excerpt,
            Content = dto.Content,
            Category = dto.Category,
            Author = dto.Author,
            AuthorTitle = dto.AuthorTitle,
            Date = dto.Date.HasValue ? DateTime.SpecifyKind(dto.Date.Value, DateTimeKind.Utc) : DateTime.UtcNow,
            ReadTime = dto.ReadTime,
            ImageUrl = dto.ImageUrl,
            Tags = dto.Tags,
            IsPublished = dto.IsPublished,
        };
        _db.BlogPosts.Add(post);
        await _db.SaveChangesAsync();
        return Ok(post);
    }

    [HttpPut("{id}")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> Update(int id, [FromBody] BlogPostDto dto)
    {
        var post = await _db.BlogPosts.FindAsync(id);
        if (post == null) return NotFound();

        var slug = string.IsNullOrWhiteSpace(dto.Slug) ? GenerateSlug(dto.Title) : dto.Slug.Trim();
        if (slug != post.Slug && await _db.BlogPosts.AnyAsync(b => b.Slug == slug && b.Id != id))
            return BadRequest(new { message = "Bu slug zaten kullanımda." });

        post.Slug = slug;
        post.Title = dto.Title;
        post.Excerpt = dto.Excerpt;
        post.Content = dto.Content;
        post.Category = dto.Category;
        post.Author = dto.Author;
        post.AuthorTitle = dto.AuthorTitle;
        if (dto.Date.HasValue) post.Date = DateTime.SpecifyKind(dto.Date.Value, DateTimeKind.Utc);
        post.ReadTime = dto.ReadTime;
        post.ImageUrl = dto.ImageUrl;
        post.Tags = dto.Tags;
        post.IsPublished = dto.IsPublished;

        await _db.SaveChangesAsync();
        return Ok(post);
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> Delete(int id)
    {
        var post = await _db.BlogPosts.FindAsync(id);
        if (post == null) return NotFound();
        _db.BlogPosts.Remove(post);
        await _db.SaveChangesAsync();
        return Ok();
    }

    private static string GenerateSlug(string title)
    {
        var slug = title.ToLowerInvariant()
            .Replace('ğ', 'g').Replace('ü', 'u').Replace('ş', 's')
            .Replace('ı', 'i').Replace('ö', 'o').Replace('ç', 'c');
        slug = System.Text.RegularExpressions.Regex.Replace(slug, @"[^a-z0-9\s-]", "");
        slug = System.Text.RegularExpressions.Regex.Replace(slug, @"\s+", "-");
        return slug.Trim('-');
    }
}

public class BlogPostDto
{
    public string Slug { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Excerpt { get; set; } = string.Empty;
    public string Content { get; set; } = "[]";
    public string Category { get; set; } = string.Empty;
    public string Author { get; set; } = string.Empty;
    public string AuthorTitle { get; set; } = string.Empty;
    public DateTime? Date { get; set; }
    public int ReadTime { get; set; } = 5;
    public string ImageUrl { get; set; } = string.Empty;
    public string Tags { get; set; } = "[]";
    public bool IsPublished { get; set; } = true;
}
