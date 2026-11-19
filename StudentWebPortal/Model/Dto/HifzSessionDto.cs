using StudentWebPortal.Model.Entity.Enum;

namespace StudentWebPortal.Model.Dto;

public sealed class HifzSessionDto
{
    public int Id { get; set; }
    public int StudentId { get; set; }
    public string? StudentName { get; set; }
    public DateTime SessionDate { get; set; }
    public Surahs? Surah { get; set; }
    public int VerseStart { get; set; }
    public SessionStatus? Status { get; set; }
    public Ranks? Rank { get; set; }
    public int DurationMinutes { get; set; }
    public string? Notes { get; set; }
    public Teachers? RecordedBy { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public byte[]? RowVersion { get; set; }
}
