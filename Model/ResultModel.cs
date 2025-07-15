using System.ComponentModel;

namespace NTH.Model
{
    public class ResultModel : BaseModel
    {
        [DisplayName("DoTestDate")]
        public DateTime? DoTestDate { get; set; }
        [DisplayName("IsPassed")]
        public bool? IsPassed { get; set; }
        [DisplayName("CorrectAnswers")]
        public int? CorrectAnswers { get; set; }
        [DisplayName("TotalVocab")]
        public int? TotalVocab { get; set; }
    }
}
