public class CampaignJoin
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public int CampaignId { get; set; }
    public DateTime JoinedAt { get; set; } = DateTime.UtcNow;
}
