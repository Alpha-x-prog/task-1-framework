namespace Pr1.MinWebService.Models;

public sealed class Item
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public decimal Price { get; init; }
}