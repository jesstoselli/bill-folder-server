namespace BillFolder.Domain.Entities;

public class Category
{
    public Guid Id { get; set; }
    public string Key { get; set; } = null!;
    public string NamePt { get; set; } = null!;
    public bool IsSystem { get; set; }
    public short DisplayOrder { get; set; }
    public DateTime CreatedAt { get; set; }
}
