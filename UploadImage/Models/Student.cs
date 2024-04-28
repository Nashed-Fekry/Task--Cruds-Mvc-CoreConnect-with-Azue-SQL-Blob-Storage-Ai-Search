using Azure.Search.Documents.Indexes;

namespace UploadImage.Models
{
    public class Student
    {
        public Guid Id { get; set; }
        [SearchableField(IsFilterable = true, IsSortable = true, IsFacetable = true)]
        public string? Name { get; set; }
        public string? Image { get; set; }
        public int Age { get; set; }
        [SearchableField(IsFilterable = true, IsSortable = true, IsFacetable = true)]
        public string? Address { get; set; }
        public string? PhoneNumber { get; set; }
        [SearchableField(IsFilterable = true, IsSortable = true, IsFacetable = true)]
        public string? Course { get; set; }
    }
}
