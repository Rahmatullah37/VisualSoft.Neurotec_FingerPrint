using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VisualSoft.Biomatric.Identification.Domain.Models
{
    public class IdentificationResult
    {
        public bool Success { get; set; }
        public string Status { get; set; }
        public string MatchedSubjectId { get; set; }
        public int MatchingScore { get; set; }
        public int MatchingThreshold { get; set; }
        public string Message { get; set; }
        // NEW: List of all matches
        public List<MatchInfo> AllMatches { get; set; }
        public int TotalMatches { get; set; }
    }
    public class MatchInfo
    {
        public string SubjectId { get; set; }
        public int Score { get; set; }
    }

}
