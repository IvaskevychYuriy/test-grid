namespace TestGrid.Changes;

public record ItemPatch(
    string? Name = null,
    int? Quantity = null,
    float? Price = null,
    DateTimeOffset? ModifiedOn = null);
