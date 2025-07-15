
using NTH.DefineEnum;
using System.ComponentModel;

namespace NTH.MappingSheet
{
    public class VocabModelResponse : BaseModelResponse
    {
        [DisplayName("ForeignLanguage")] // Tên cột trong Google Sheet
        public string? ForeignLanguage { get; set; }

        [DisplayName("Meaning")] // Tên cột trong Google Sheet
        public string? Meaning { get; set; }

        [DisplayName("TypeWord")] // Tên cột trong Google Sheet
        public TypeWord? TypeWord { get; set; } = DefineEnum.TypeWord.Unknow; // Sử dụng tên Enum đầy đủ

        [DisplayName("Pronounce")] // Tên cột trong Google Sheet
        public string? Pronounce { get; set; }

        [DisplayName("Description")] // Tên cột trong Google Sheet
        public string? Description { get; set; }

        [DisplayName("DoTestDate")] // Tên cột trong Google Sheet
        public DateTime? DoTestDate { get; set; }
    }
}
