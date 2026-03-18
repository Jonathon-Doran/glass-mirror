namespace Glass.Data.Models;

public class Command
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public List<CommandStep> Steps { get; set; } = new();
}