
using System.ComponentModel;

namespace NTH.MappingSheet
{
    public class BaseModelResponse
    {
        public string? RowIndex { get; set; }

        [DisplayName("CreatedDate")]
        public DateTime? CreatedDate { get; set; }

        [DisplayName("CreateUserId")]
        public string? CreateUserId { get; set; }

        [DisplayName("IsDeleted")]
        public bool? IsDeleted { get; set; } = false;
    }
}
