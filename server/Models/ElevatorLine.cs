namespace Termometriya.Server.Models;

public class ElevatorLine
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int DisplayOrder { get; set; }
    public ICollection<Silo> Silos { get; set; } = new List<Silo>();
}
