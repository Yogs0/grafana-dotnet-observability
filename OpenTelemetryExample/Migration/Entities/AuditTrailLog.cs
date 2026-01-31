using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace OpenTelemetryExample.Migration.Entities;

[Table("Log_AuditTrail_T")]
public class AuditTrailLog
{
    [Key]
    public int Id { get; set; }
    public string? Message { get; set; }
    public string? Level { get; set; }
    public DateTime TimeStamp { get; set; }
    public string? Exception { get; set; }
    public string? UserId { get; set; }
    public string? UserIp { get; set; }
    public string? Device { get; set; }
    public string? Browser { get; set; }
    public string? Platform { get; set; }
    public string? Crawler { get; set; }
    public string? Application { get; set; }
    public string? ApplicationVersion { get; set; }
    public string? UserAgent { get; set; }
}
