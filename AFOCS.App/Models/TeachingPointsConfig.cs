using AFOCS.Infrastructure;

namespace AFOCS.App.Models;
// ==================== JSON 持久化 POCO ====================

public class TeachingPointsConfig
{
    public List<TeachingPointPoco> Points { get; set; } = [];
}

public class TeachingPointPoco
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public WorkPos Station { get; set; } = WorkPos.Left;
    public List<EAxis> AxisKeys { get; set; } = [];
    public Dictionary<EAxis, double> AxisPositions { get; set; } = [];
}