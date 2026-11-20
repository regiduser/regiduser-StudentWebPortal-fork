using StudentWebPortal.Model.Entity.Enum;
using System.ComponentModel.DataAnnotations;

namespace StudentWebPortal.Model.Dto
{
    public class AttendanceCreateDto
    {
        [Range(1, int.MaxValue, ErrorMessage = "StudentId must be a positive integer.")]
        public int StudentId { get; set; }

        /// <summary>Optional. Defaults to today (UTC). Cannot be in the future.</summary>
        [DataType(DataType.Date)]
        public DateTime? AttendanceDate { get; set; }

        [Required(ErrorMessage = "Attendance status is required.")]
        [EnumDataType(typeof(SessionStatus), ErrorMessage = "Invalid attendance status selected.")]
        [Display(Name = "Status")]
        public SessionStatus? SessionStatus { get; set; }

        [MaxLength(250)]
        [DataType(DataType.MultilineText)]
        [Display(Name = "Notes")]
        public string? Notes { get; set; }

        [Required(ErrorMessage = "Attendance RecordedBy is required.")]
        [EnumDataType(typeof(Teachers), ErrorMessage = "Invalid attendance RecordedBy selected.")]
        [Display(Name = "Recorded By")]
        public Teachers? RecordedBy { get; set; }
    }
}
