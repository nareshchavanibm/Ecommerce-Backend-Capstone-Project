namespace ECommerce.Domain.Entities;

public class ImportJob
{
    public int Id { get; set; }

    public string JobId { get; set; } = string.Empty;

    public int UploadedBy { get; set; }

    public string Status { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }

    public DateTime? CompletedAt { get; set; }

    public int TotalRows { get; set; }

    public int Inserted { get; set; }

    public int Errors { get; set; }

    public string? ErrorReportPath { get; set; }

    // JSON mapping of OriginalCategoryId -> FinalCategoryId assigned during import
    // Stored as JSON string for simplicity; small size expected per import
    public string? CategoryIdMappingJson { get; set; }
}
