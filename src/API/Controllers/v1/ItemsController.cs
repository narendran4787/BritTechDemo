using Application.DTOs;
using Application.Interfaces;
using Asp.Versioning;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Solution.API.Controllers.v1;

[ApiController]
[ApiVersion("1.0")]
[RequireHttps]
[Route("api/v{version:apiVersion}/products/{productId:int}/items")]
public class ItemsController : ControllerBase
{
    private readonly IItemService _service;
    private readonly IValidator<ItemUpsertDto> _upsertValidator;

    public ItemsController(
        IItemService service,
        IValidator<ItemUpsertDto> upsertValidator)
    {
        _service = service;
        _upsertValidator = upsertValidator;
    }

    [HttpGet]
    [ProducesResponseType(typeof(ItemDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetByProductId(
        [FromRoute] int productId,
        CancellationToken ct = default)
    {
        var result = await _service.GetByProductIdAsync(productId, ct);
        if (result == null) return NotFound();
        return Ok(result);
    }

    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(ItemDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(
        [FromRoute] int productId,
        [FromRoute] int id,
        CancellationToken ct = default)
    {
        var result = await _service.GetByIdAsync(productId, id, ct);
        if (result == null) return NotFound();
        return Ok(result);
    }

    [HttpPost]
    [Authorize]
    [ProducesResponseType(typeof(ItemDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Upsert(
        [FromRoute] int productId,
        [FromBody] ItemUpsertDto dto,
        CancellationToken ct = default)
    {
        return await UpsertInternal(productId, null, dto, ct);
    }

    [HttpPut("{id:int}")]
    [Authorize]
    [ProducesResponseType(typeof(ItemDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Upsert(
        [FromRoute] int productId,
        [FromRoute] int id,
        [FromBody] ItemUpsertDto dto,
        CancellationToken ct = default)
    {
        return await UpsertInternal(productId, id, dto, ct);
    }

    private async Task<IActionResult> UpsertInternal(
        int productId,
        int? itemId,
        ItemUpsertDto dto,
        CancellationToken ct)
    {
        var validationResult = await _upsertValidator.ValidateAsync(dto, ct);
        if (!validationResult.IsValid)
        {
            return ValidationProblem(CreateValidationProblemDetails(validationResult));
        }

        try
        {
            var result = await _service.UpsertAsync(productId, itemId, dto, ct);
            var version = HttpContext.GetRequestedApiVersion()?.ToString() ?? "1.0";
            
            // If itemId was provided, it's an update (200 OK)
            // If itemId was not provided, it's a create (201 Created)
            if (itemId.HasValue)
            {
                return Ok(result);
            }
            else
            {
                return CreatedAtAction(
                    nameof(GetById),
                    new { productId, id = result.Id, version },
                    result);
            }
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { error = ex.Message });
        }
    }

    private static ValidationProblemDetails CreateValidationProblemDetails(FluentValidation.Results.ValidationResult validationResult)
    {
        var errors = validationResult.Errors
            .GroupBy(x => x.PropertyName)
            .ToDictionary(
                g => g.Key,
                g => g.Select(x => x.ErrorMessage).ToArray());
        return new ValidationProblemDetails(errors);
    }
}

