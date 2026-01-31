using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace OpenTelemetryExample.Migration.Entities;

[Table("LogsApi")]
public class ApiLog
{
    [Key]
    public int Id { get; set; }
    public string? Message { get; set; }
    public string? Level { get; set; }
    public DateTime TimeStamp { get; set; }
    public string? Exception { get; set; }
    public string? Properties { get; set; }
}
