using System.ComponentModel.DataAnnotations;

namespace Application.DTOs;

public record ProductCreateDto(
    [property: Required, MaxLength(255)] string ProductName
);

public record ProductUpdateDto(
    [property: Required, MaxLength(255)] string ProductName
);

public record ProductDto(
    int Id,
    string ProductName,
    string CreatedBy,
    DateTime CreatedOn,
    string? ModifiedBy,
    DateTime? ModifiedOn
);

public record ItemDto(
    int Id,
    int ProductId,
    int Quantity
);

public record ItemUpsertDto(
    [property: Required, Range(0, int.MaxValue)] int Quantity
);
