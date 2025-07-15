

using System.ComponentModel;

namespace NTH.Model
{
    public class UserModel : BaseModel
    {
        [DisplayName("UserId")]
        public string? UserId { get; set; } // Discord User ID

        [DisplayName("UserName")]
        public string? UserName { get; set; } // Discord Username

        [DisplayName("RemindTime")]
        public TimeOnly? RemindTime { get; set; } // Thời gian nhắc nhở hàng ngày

        [DisplayName("DoTestTime")]
        public TimeOnly? DoTestTime { get; set; } // Thời gian làm bài hàng ngày
    }
}
