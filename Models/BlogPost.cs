namespace AdalyaSolarAPI.Models;

public class BlogPost
{
    public int Id { get; set; }
    public string Slug { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Excerpt { get; set; } = string.Empty;
    public string Content { get; set; } = "[]"; // JSON array of paragraphs
    public string Category { get; set; } = string.Empty;
    public string Author { get; set; } = string.Empty;
    public string AuthorTitle { get; set; } = string.Empty;
    public DateTime Date { get; set; } = DateTime.UtcNow;
    public int ReadTime { get; set; } = 5;
    public string ImageUrl { get; set; } = string.Empty;
    public string Tags { get; set; } = "[]"; // JSON array of tag strings
    public bool IsPublished { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
