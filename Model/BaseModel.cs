
using System.ComponentModel;

namespace NTH.Model
{
    public class BaseModel
    {
        [DisplayName("CreatedDate")]
        public DateTime? CreatedDate { get; set; }

        [DisplayName("CreateUserId")]
        public string? CreateUserId { get; set; }

        [DisplayName("IsDeleted")]
        public bool? IsDeleted { get; set; } = false;
    }
}
