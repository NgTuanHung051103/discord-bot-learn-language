using NTH.MappingSheet;

namespace NTH.Model
{
    public class UserTestSessionModel
    {
        public ulong UserId { get; set; }
        public List<VocabModelResponse> VocabListForTest { get; set; } = new List<VocabModelResponse>(); 
        public int CurrentQuestionIndex { get; set; } = 0; // Chỉ số của câu hỏi hiện tại
        public int CorrectAnswersCount { get; set; } = 0; // Số câu trả lời đúng
        public DateTime SessionStartTime { get; set; } // Thời gian bắt đầu phiên
        public string QuestionType { get; set; } = "MeaningToForeignLanguage"; // Loại câu hỏi (ForeignLanguageToMeaning/MeaningToForeignLanguage)
        public ulong ChannelId { get; set; } // Kênh mà bài kiểm tra đang diễn ra
        public ulong MessageId { get; set; } // ID của tin nhắn câu hỏi cuối cùng (để sửa nếu cần)
        public bool IsActive { get; set; } = true; // Cờ cho biết phiên đang hoạt động
        public DateTime? DoTestDate { get; set; }
    }
}
