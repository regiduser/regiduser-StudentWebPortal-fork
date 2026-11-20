namespace StudentWebPortal.Model.Dto;

/// <summary>Progress snapshot for one student, combining attendance, Hifz and revision activity.</summary>
public sealed class StudentSummaryDto
{
    public int StudentId { get; set; }
    public required string StudentName { get; set; }
    public bool IsActive { get; set; }
    public required AttendanceSummary Attendance { get; set; }
    public required SessionSummary Hifz { get; set; }
    public required SessionSummary Revision { get; set; }
}

/// <summary>
/// AttendanceRatePercent counts Present and Late as attended; it is null when no attendance has been recorded.
/// </summary>
public sealed record AttendanceSummary(int Total, int Present, int Late, int Absent, double? AttendanceRatePercent);

public sealed record SessionSummary(
    int TotalSessions,
    int TotalMinutes,
    DateTime? LastSessionDate,
    string? LastSurah,
    int? LastVerse);
