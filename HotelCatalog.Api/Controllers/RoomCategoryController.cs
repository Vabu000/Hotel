using Microsoft.AspNetCore.Mvc;
using HotelCatalog.Bll.DTOs;
using HotelCatalog.Bll.Interfaces;

namespace HotelCatalog.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Produces("application/json")]
    public class RoomCategoryController : ControllerBase
    {
        private readonly IRoomCategoryService _roomCategoryService;
        private readonly ILogger<RoomCategoryController> _logger;

        public RoomCategoryController(IRoomCategoryService roomCategoryService, ILogger<RoomCategoryController> logger)
        {
            _roomCategoryService = roomCategoryService;
            _logger = logger;
        }

        [HttpGet]
        [ProducesResponseType(typeof(IEnumerable<RoomCategoryDto>), StatusCodes.Status200OK)]
        public async Task<ActionResult<IEnumerable<RoomCategoryDto>>> GetAllCategories(CancellationToken cancellationToken)
        {
            try
            {
                var categories = await _roomCategoryService.GetAllAsync(cancellationToken);
                return Ok(categories);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting all room categories");
                return StatusCode(StatusCodes.Status500InternalServerError, "Internal server error");
            }
        }

        [HttpGet("with-service-count")]
        [ProducesResponseType(typeof(IEnumerable<RoomCategoryDto>), StatusCodes.Status200OK)]
        public async Task<ActionResult<IEnumerable<RoomCategoryDto>>> GetCategoriesWithServiceCount(CancellationToken cancellationToken)
        {
            try
            {
                var categories = await _roomCategoryService.GetCategoriesWithServiceCountAsync(cancellationToken);
                return Ok(categories);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting categories with service count");
                return StatusCode(StatusCodes.Status500InternalServerError, "Internal server error");
            }
        }
        [HttpGet("{id:int}")]
        [ProducesResponseType(typeof(RoomCategoryDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<RoomCategoryDto>> GetCategory(int id, CancellationToken cancellationToken)
        {
            try
            {
                var category = await _roomCategoryService.GetByIdAsync(id, cancellationToken);

                if (category == null)
                {
                    return NotFound($"Room category with ID {id} not found");
                }

                return Ok(category);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting room category with ID {CategoryId}", id);
                return StatusCode(StatusCodes.Status500InternalServerError, "Internal server error");
            }
        }

        [HttpGet("{id:int}/with-services")]
        [ProducesResponseType(typeof(RoomCategoryDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<RoomCategoryDto>> GetCategoryWithServices(int id, CancellationToken cancellationToken)
        {
            try
            {
                var category = await _roomCategoryService.GetByIdWithServicesAsync(id, cancellationToken);

                if (category == null)
                {
                    return NotFound($"Room category with ID {id} not found");
                }

                return Ok(category);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting room category with services for ID {CategoryId}", id);
                return StatusCode(StatusCodes.Status500InternalServerError, "Internal server error");
            }
        }

        [HttpGet("{id:int}/services")]
        [ProducesResponseType(typeof(IEnumerable<ServiceDto>), StatusCodes.Status200OK)]
        public async Task<ActionResult<IEnumerable<ServiceDto>>> GetServicesByCategory(int id, CancellationToken cancellationToken)
        {
            try
            {
                var services = await _roomCategoryService.GetServicesByCategoryAsync(id, cancellationToken);
                return Ok(services);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting services for category {CategoryId}", id);
                return StatusCode(StatusCodes.Status500InternalServerError, "Internal server error");
            }
        }

        [HttpPost]
        [ProducesResponseType(typeof(RoomCategoryDto), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<RoomCategoryDto>> CreateCategory([FromBody] CreateRoomCategoryDto dto, CancellationToken cancellationToken)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                var category = await _roomCategoryService.CreateAsync(dto, cancellationToken);
                return CreatedAtAction(nameof(GetCategory), new { id = category.CategoryId }, category);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating room category");
                return StatusCode(StatusCodes.Status500InternalServerError, "Internal server error");
            }
        }

        [HttpPut("{id:int}")]
        [ProducesResponseType(typeof(RoomCategoryDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<RoomCategoryDto>> UpdateCategory(int id, [FromBody] UpdateRoomCategoryDto dto, CancellationToken cancellationToken)
        {
            if (id != dto.CategoryId)
            {
                return BadRequest("Category ID in URL doesn't match the ID in request body");
            }

            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                var category = await _roomCategoryService.UpdateAsync(dto, cancellationToken);
                return Ok(category);
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating room category with ID {CategoryId}", id);
                return StatusCode(StatusCodes.Status500InternalServerError, "Internal server error");
            }
        }

        [HttpDelete("{id:int}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> DeleteCategory(int id, CancellationToken cancellationToken)
        {
            try
            {
                var isDeleted = await _roomCategoryService.DeleteAsync(id, cancellationToken);

                if (isDeleted)
                {
                    return NoContent();
                }

                return NotFound($"Room category with ID {id} not found");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting room category with ID {CategoryId}", id);
                return StatusCode(StatusCodes.Status500InternalServerError, "Internal server error");
            }
        }

        [HttpPost("assign-service")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> AssignServiceToCategory([FromBody] AssignServiceToCategoryDto dto, CancellationToken cancellationToken)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                var result = await _roomCategoryService.AssignServiceToCategoryAsync(dto, cancellationToken);

                if (result)
                {
                    return Ok("Service assigned to category successfully");
                }

                return NotFound("Category or service not found");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error assigning service to category");
                return StatusCode(StatusCodes.Status500InternalServerError, "Internal server error");
            }
        }

        [HttpDelete("{categoryId:int}/services/{serviceId:int}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> RemoveServiceFromCategory(int categoryId, int serviceId, CancellationToken cancellationToken)
        {
            try
            {
                var result = await _roomCategoryService.RemoveServiceFromCategoryAsync(categoryId, serviceId, cancellationToken);

                if (result)
                {
                    return NoContent();
                }

                return NotFound("Service-category relationship not found");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing service from category");
                return StatusCode(StatusCodes.Status500InternalServerError, "Internal server error");
            }
        }

        [HttpGet("paged")]
        [ProducesResponseType(typeof(PagedResult<RoomCategoryDto>), StatusCodes.Status200OK)]
        public async Task<ActionResult<PagedResult<RoomCategoryDto>>> GetPagedCategories(
            [FromQuery] RoomCategoryQueryParameters parameters,
            CancellationToken cancellationToken)
        {
            try
            {
                var result = await _roomCategoryService.GetPagedWithSpecificationAsync(parameters, cancellationToken);

                Response.Headers.Append("X-Total-Count", result.TotalCount.ToString());
                Response.Headers.Append("X-Page", result.Page.ToString());
                Response.Headers.Append("X-Page-Size", result.PageSize.ToString());
                Response.Headers.Append("X-Total-Pages", result.TotalPages.ToString());

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting paged room categories");
                return StatusCode(StatusCodes.Status500InternalServerError, "Internal server error");
            }
        }
    }
}