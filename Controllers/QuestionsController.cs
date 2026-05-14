using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AdalyaSolarAPI.Data;
using AdalyaSolarAPI.Models;
using System.Security.Claims;

namespace AdalyaSolarAPI.Controllers;

[ApiController]
[Route("api/questions")]
public class QuestionsController : ControllerBase
{
    private readonly AppDbContext _db;
    public QuestionsController(AppDbContext db) => _db = db;

    private int? TryGetUserId()
    {
        var claim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return claim != null ? int.Parse(claim) : null;
    }

    // ── Public ────────────────────────────────────────────────────────────────

    [HttpGet("product/{productId}")]
    public async Task<IActionResult> GetByProduct(int productId)
    {
        var questions = await _db.ProductQuestions
            .Where(q => q.ProductId == productId && q.IsVisible)
            .OrderByDescending(q => q.CreatedAt)
            .Select(q => new
            {
                q.Id, q.UserName, q.Question,
                q.Answer, q.CreatedAt, q.AnsweredAt,
            })
            .ToListAsync();
        return Ok(questions);
    }

    // ── Authorized ────────────────────────────────────────────────────────────

    [HttpPost]
    [Authorize]
    public async Task<IActionResult> Ask([FromBody] AskQuestionDto dto)
    {
        var uid = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var user = await _db.Users.FindAsync(uid);
        if (user == null) return Unauthorized();

        var product = await _db.Products.FindAsync(dto.ProductId);
        if (product == null) return NotFound(new { message = "Ürün bulunamadı." });

        var q = new ProductQuestion
        {
            ProductId = dto.ProductId,
            UserId = uid,
            UserName = user.Name,
            Question = dto.Question.Trim(),
        };
        _db.ProductQuestions.Add(q);
        await _db.SaveChangesAsync();

        return Ok(new
        {
            q.Id, q.UserName, q.Question,
            q.Answer, q.CreatedAt, q.AnsweredAt,
        });
    }

    // ── Admin ─────────────────────────────────────────────────────────────────

    [HttpGet("admin/pending-count")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> PendingCount()
    {
        var count = await _db.ProductQuestions.CountAsync(q => q.Answer == null);
        return Ok(new { count });
    }

    [HttpGet("admin/all")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> AdminGetAll()
    {
        var list = await _db.ProductQuestions
            .Include(q => q.Product)
            .OrderByDescending(q => q.CreatedAt)
            .Select(q => new
            {
                q.Id, q.UserName, q.Question, q.Answer,
                q.IsVisible, q.CreatedAt, q.AnsweredAt,
                q.ProductId,
                ProductName = q.Product.Name,
            })
            .ToListAsync();
        return Ok(list);
    }

    [HttpPut("admin/{id}/answer")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> Answer(int id, [FromBody] AnswerDto dto)
    {
        var q = await _db.ProductQuestions.FindAsync(id);
        if (q == null) return NotFound();
        q.Answer = dto.Answer.Trim();
        q.AnsweredAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        // Kullanıcıya bildirim gönder
        _db.Notifications.Add(new Notification
        {
            UserId = q.UserId,
            Title = "Sorunuz Yanıtlandı",
            Message = $"Ürüne yönelik sorunuz yanıtlandı: \"{q.Question.Substring(0, Math.Min(60, q.Question.Length))}...\"",
            Type = "info",
        });
        await _db.SaveChangesAsync();

        return Ok(new { q.Id, q.Answer, q.AnsweredAt });
    }

    [HttpPut("admin/{id}/visibility")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> SetVisibility(int id, [FromBody] VisibilityDto dto)
    {
        var q = await _db.ProductQuestions.FindAsync(id);
        if (q == null) return NotFound();
        q.IsVisible = dto.IsVisible;
        await _db.SaveChangesAsync();
        return Ok();
    }

    [HttpDelete("admin/{id}")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> Delete(int id)
    {
        var q = await _db.ProductQuestions.FindAsync(id);
        if (q == null) return NotFound();
        _db.ProductQuestions.Remove(q);
        await _db.SaveChangesAsync();
        return Ok();
    }
}

public class AskQuestionDto
{
    public int ProductId { get; set; }
    public string Question { get; set; } = string.Empty;
}

public class AnswerDto
{
    public string Answer { get; set; } = string.Empty;
}

public class VisibilityDto
{
    public bool IsVisible { get; set; }
}
