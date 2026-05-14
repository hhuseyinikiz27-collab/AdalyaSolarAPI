using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AdalyaSolarAPI.Data;
using AdalyaSolarAPI.Models;
using System.Security.Claims;
using System.Text.Json;

namespace AdalyaSolarAPI.Controllers;

[ApiController]
[Route("api/reviews")]
public class ReviewsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IWebHostEnvironment _env;

    public ReviewsController(AppDbContext db, IWebHostEnvironment env)
    {
        _db = db;
        _env = env;
    }

    private int? GetUserId()
    {
        var claim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return claim != null ? int.Parse(claim) : null;
    }

    [HttpGet("product/{productId}")]
    public async Task<IActionResult> GetByProduct(int productId)
    {
        var uid = GetUserId();
        var reviews = await _db.Reviews
            .Where(r => r.ProductId == productId)
            .OrderByDescending(r => r.CreatedAt)
            .Select(r => new
            {
                r.Id,
                r.ProductId,
                r.UserId,
                r.UserName,
                r.Rating,
                r.Comment,
                r.AdminReply,
                r.PhotosJson,
                r.LikeCount,
                r.CreatedAt,
                LikedByMe = uid != null && r.Likes.Any(l => l.UserId == uid),
            })
            .ToListAsync();

        return Ok(reviews);
    }

    [HttpPost]
    [Authorize]
    public async Task<IActionResult> Create([FromBody] ReviewCreateDto dto, [FromQuery] int productId)
    {
        var uid = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var user = await _db.Users.FindAsync(uid);
        if (user == null) return Unauthorized();

        var product = await _db.Products.FindAsync(productId);
        if (product == null) return NotFound();

        var already = await _db.Reviews.AnyAsync(r => r.ProductId == productId && r.UserId == uid);
        if (already) return BadRequest(new { message = "Bu ürüne zaten yorum yaptınız." });

        var review = new Review
        {
            ProductId = productId,
            UserId = uid,
            UserName = user.Name,
            UserEmail = user.Email,
            Rating = Math.Clamp(dto.Rating, 1, 5),
            Comment = dto.Comment,
        };

        _db.Reviews.Add(review);
        await _db.SaveChangesAsync();
        return Ok(new { review.Id, review.ProductId, review.UserId, review.UserName, review.Rating, review.Comment, review.AdminReply, review.LikeCount, review.CreatedAt, LikedByMe = false });
    }

    [HttpPost("{id}/photos")]
    [Authorize]
    public async Task<IActionResult> UploadPhotos(int id, [FromForm] List<IFormFile> files)
    {
        var uid = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var review = await _db.Reviews.FindAsync(id);
        if (review == null) return NotFound();
        if (review.UserId != uid) return Forbid();

        if (files == null || files.Count == 0)
            return BadRequest(new { message = "Dosya seçilmedi." });

        var existing = string.IsNullOrEmpty(review.PhotosJson)
            ? new List<string>()
            : JsonSerializer.Deserialize<List<string>>(review.PhotosJson) ?? new List<string>();

        if (existing.Count + files.Count > 5)
            return BadRequest(new { message = "En fazla 5 fotoğraf yükleyebilirsiniz." });

        var uploadsPath = Path.Combine(_env.WebRootPath ?? "wwwroot", "uploads", "reviews");
        Directory.CreateDirectory(uploadsPath);

        var allowed = new[] { ".jpg", ".jpeg", ".png", ".webp" };
        foreach (var file in files)
        {
            var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (!allowed.Contains(ext)) continue;
            var fileName = $"{Guid.NewGuid()}{ext}";
            var filePath = Path.Combine(uploadsPath, fileName);
            using var stream = new FileStream(filePath, FileMode.Create);
            await file.CopyToAsync(stream);
            existing.Add($"/uploads/reviews/{fileName}");
        }

        review.PhotosJson = JsonSerializer.Serialize(existing);
        await _db.SaveChangesAsync();
        return Ok(new { photos = existing });
    }

    [HttpPost("{id}/like")]
    [Authorize]
    public async Task<IActionResult> Like(int id)
    {
        var uid = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var review = await _db.Reviews.FindAsync(id);
        if (review == null) return NotFound();

        var existing = await _db.ReviewLikes.FirstOrDefaultAsync(l => l.ReviewId == id && l.UserId == uid);
        if (existing != null)
        {
            _db.ReviewLikes.Remove(existing);
            review.LikeCount = Math.Max(0, review.LikeCount - 1);
            await _db.SaveChangesAsync();
            return Ok(new { liked = false, likeCount = review.LikeCount });
        }

        _db.ReviewLikes.Add(new ReviewLike { ReviewId = id, UserId = uid });
        review.LikeCount++;
        await _db.SaveChangesAsync();
        return Ok(new { liked = true, likeCount = review.LikeCount });
    }
}

public class ReviewCreateDto
{
    public int Rating { get; set; }
    public string Comment { get; set; } = string.Empty;
}
