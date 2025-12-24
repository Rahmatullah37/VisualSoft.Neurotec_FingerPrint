using Serilog.Filters;

namespace VisualSoft.Biomatric.Identification.API.Models
{
    public class IdentifyResponse
    {
        public bool Success { get; set; }
        public string MatchedSubjectId { get; set; }
        public int Best_MatchingScore { get; set; }
        public string Status { get; set; }
        public string Message { get; set; }

    }
    
}
