using Application.Results;

namespace API.Models.Statistics
{
    public class SummaryStatisticsResponseDto
    {
        public SummaryStatisticDTO Summary { get; set; } = new();
        public IEnumerable<GradeDTO> Grades { get; set; } = new List<GradeDTO>();
        public IEnumerable<SubjectDTO> Subjects { get; set; } = new List<SubjectDTO>();
    }
}
