using HotelReviews.Application.Common.Models;
using HotelReviews.Application.DTOs;
using HotelReviews.Application.Requests.Commands.CreateRequest;
using HotelReviews.Application.Requests.Commands.UpdateRequestStatus;
using HotelReviews.Application.Requests.Queries.GetPendingRequests;
using HotelReviews.Application.Requests.Queries.GetRequestById;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Bson;

namespace HotelReviews.WebApi.Controllers;
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class RequestsController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ILogger<RequestsController> _logger;

    public RequestsController(IMediator mediator, ILogger<RequestsController> logger)
    {
        _mediator = mediator;
        _logger = logger;
    }
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(RequestDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<RequestDto>> GetById(
        string id,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Запит на отримання запиту з ID: {RequestId}", id);

        if (!ObjectId.TryParse(id, out _))
        {
            return BadRequest(new { Message = $"Невалідний ID '{id}'. ID повинен бути 24-символьним hex рядком (наприклад, '507f1f77bcf86cd799439011')" });
        }

        var query = new GetRequestByIdQuery(id);
        var result = await _mediator.Send(query, cancellationToken);

        if (result == null)
        {
            return NotFound(new { Message = $"Запит з ID '{id}' не знайдено" });
        }

        return Ok(result);
    }
    [HttpGet("pending")]
    [ProducesResponseType(typeof(PagedResult<RequestDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<RequestDto>>> GetPending(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Запит pending запитів, сторінка {Page}", page);

        var query = new GetPendingRequestsQuery(page, pageSize);
        var result = await _mediator.Send(query, cancellationToken);

        return Ok(result);
    }
    [HttpPost]
    [ProducesResponseType(typeof(RequestDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<RequestDto>> Create(
        [FromBody] CreateRequestCommand command,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Створення нового запиту для кімнати {RoomId} від клієнта {ClientId}",
            command.RoomId, command.ClientId);

        var result = await _mediator.Send(command, cancellationToken);

        return CreatedAtAction(
            nameof(GetById),
            new { id = result.Id },
            result);
    }
    [HttpPut("{id}/status")]
    [ProducesResponseType(typeof(RequestDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<RequestDto>> UpdateStatus(
        string id,
        [FromBody] UpdateRequestStatusCommand command,
        CancellationToken cancellationToken)
    {
        if (!ObjectId.TryParse(id, out _))
        {
            return BadRequest(new { Message = $"Невалідний ID '{id}'. ID повинен бути 24-символьним hex рядком" });
        }

        if (id != command.Id)
        {
            return BadRequest(new { Message = "ID у URL не співпадає з ID у команді" });
        }

        _logger.LogInformation("Оновлення статусу запиту {RequestId} на '{NewStatus}'",
            id, command.NewStatus);

        var result = await _mediator.Send(command, cancellationToken);

        return Ok(result);
    }
}