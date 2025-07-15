
namespace NTH.Common
{
    public class Constant
    {
        public static readonly List<string> HEADER_USER = new List<string>
        {
            "UserId",
            "UserName",
            "RemindTime",
            "DoTestTime",
            "DoTestTime",
            "CreatedDate",
            "CreateUserId",
            "IsDeleted"
        };

        public const string NAME_SHEET_USER = "user";
        public const string RANGE_SHEET_USER = "!A1:G";

        public static readonly List<string> HEADER_VOCAB = new List<string>
        {
            "ForeignLanguage",
            "Meaning",
            "TypeWord",
            "Pronounce",
            "Description",
            "DoTestDate",
            "CreatedDate",
            "CreateUserId",
            "IsDeleted"
        };

        public static readonly List<string> HEADER_RESULT = new List<string>
        {
            "DoTestDate",
            "IsPassed",
            "CorrectAnswers",
            "TotalVocab",
            "CreatedDate"
        };

        public const string NAME_SHEET_RESULT = "result";
        public const string RANGE_SHEET_RESULT = "!A1:E";

        public const int REMINDER_INTERVAL_MINUTES = 1;
    }
}
