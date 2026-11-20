using StudentWebPortal.Model.Entity.Enum;

namespace StudentWebPortal.Model.Dto;

public sealed class AttendanceDto
{
    public int Id { get; set; }
    public int StudentId { get; set; }
    public string? StudentName { get; set; }
    public DateTime AttendanceDate { get; set; }
    public SessionStatus? SessionStatus { get; set; }
    public string? Notes { get; set; }
    public Teachers? RecordedBy { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public byte[]? RowVersion { get; set; }
}
