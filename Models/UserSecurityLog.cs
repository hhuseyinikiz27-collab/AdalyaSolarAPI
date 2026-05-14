namespace AdalyaSolarAPI.Models;

public class UserSecurityLog
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public User User { get; set; } = null!;
    // "login" | "password_changed" | "locked_out" | "admin_unlocked"
    public string Action { get; set; } = string.Empty;
    public string Details { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
