namespace Pr1.MinWebService.Models;

public sealed class CreateItemRequest
{
    public string Name { get; init; } = string.Empty;
    public decimal Price { get; init; }
}