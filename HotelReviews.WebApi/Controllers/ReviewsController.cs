using HotelReviews.Application.Common.Models;
using HotelReviews.Application.DTOs;
using HotelReviews.Application.Reviews.Commands.ApproveReview;
using HotelReviews.Application.Reviews.Commands.CreateReview;
using HotelReviews.Application.Reviews.Commands.DeleteReview;
using HotelReviews.Application.Reviews.Commands.UpdateReview;
using HotelReviews.Application.Reviews.Queries.GetReviewById;
using HotelReviews.Application.Reviews.Queries.GetReviewsByRoom;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Bson;

namespace HotelReviews.WebApi.Controllers;
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class ReviewsController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ILogger<ReviewsController> _logger;

    public ReviewsController(IMediator mediator, ILogger<ReviewsController> logger)
    {
        _mediator = mediator;
        _logger = logger;
    }
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<ReviewDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<ReviewDto>>> GetAll(
    [FromQuery] int page = 1,
    [FromQuery] int pageSize = 10,
    [FromQuery] int? minRating = null,
    [FromQuery] bool? isVerified = null,
    [FromQuery] bool? isApproved = null,
    CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Запит всіх відгуків, сторінка {Page}", page);

        var query = new GetReviewsByRoomQuery(0, page, pageSize, minRating, isVerified, isApproved);
        var result = await _mediator.Send(query, cancellationToken);

        return Ok(result);
    }


    [HttpGet("{id}")]
    [ProducesResponseType(typeof(ReviewDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ReviewDto>> GetById(
        string id,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Запит на отримання відгуку з ID: {ReviewId}", id);

        if (!ObjectId.TryParse(id, out _))
        {
            return BadRequest(new { Message = $"Невалідний ID '{id}'. ID повинен бути 24-символьним hex рядком (наприклад, '507f1f77bcf86cd799439011')" });
        }

        var query = new GetReviewByIdQuery(id);
        var result = await _mediator.Send(query, cancellationToken);

        if (result == null)
        {
            return NotFound(new { Message = $"Відгук з ID '{id}' не знайдено" });
        }

        return Ok(result);
    }
    [HttpGet("room/{roomId}")]
    [ProducesResponseType(typeof(PagedResult<ReviewDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<ReviewDto>>> GetByRoom(
        int roomId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] int? minRating = null,
        [FromQuery] bool? isVerified = null,
        [FromQuery] bool? isApproved = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Запит відгуків для кімнати {RoomId}, сторінка {Page}", roomId, page);

        var query = new GetReviewsByRoomQuery(roomId, page, pageSize, minRating, isVerified, isApproved);
        var result = await _mediator.Send(query, cancellationToken);

        return Ok(result);
    }
    [HttpPost]
    [ProducesResponseType(typeof(ReviewDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ReviewDto>> Create(
        [FromBody] CreateReviewCommand command,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Створення нового відгуку для кімнати {RoomId} від клієнта {ClientId}",
            command.RoomId, command.ClientId);

        var result = await _mediator.Send(command, cancellationToken);

        return CreatedAtAction(
            nameof(GetById),
            new { id = result.Id },
            result);
    }
    [HttpPut("{id}")]
    [ProducesResponseType(typeof(ReviewDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ReviewDto>> Update(
        string id,
        [FromBody] UpdateReviewCommand command,
        CancellationToken cancellationToken)
    {
        if (!ObjectId.TryParse(id, out _))
        {
            return BadRequest(new { Message = $"Невалідний ID '{id}'. ID повинен бути 24-символьним hex рядком" });
        }

        if (id != command.Id)
        {
            return BadRequest(new { Message = "ID у URL не співпадає з ID у тілі запиту" });
        }

        _logger.LogInformation("Оновлення відгуку {ReviewId}", id);

        var result = await _mediator.Send(command, cancellationToken);

        return Ok(result);
    }
    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Delete(
        string id,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Видалення відгуку {ReviewId}", id);

        if (!ObjectId.TryParse(id, out _))
        {
            return BadRequest(new { Message = $"Невалідний ID '{id}'. ID повинен бути 24-символьним hex рядком" });
        }

        var command = new DeleteReviewCommand(id);
        await _mediator.Send(command, cancellationToken);

        return NoContent();
    }
    [HttpPost("{id}/approve")]
    [ProducesResponseType(typeof(ReviewDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ReviewDto>> Approve(
        string id,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Схвалення відгуку {ReviewId}", id);

        if (!ObjectId.TryParse(id, out _))
        {
            return BadRequest(new { Message = $"Невалідний ID '{id}'. ID повинен бути 24-символьним hex рядком" });
        }

        var command = new ApproveReviewCommand(id);
        var result = await _mediator.Send(command, cancellationToken);

        return Ok(result);
    }
}