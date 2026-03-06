using Microsoft.AspNetCore.Mvc;
using HotelCatalog.Bll.DTOs;
using HotelCatalog.Bll.Interfaces;

namespace HotelCatalog.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Produces("application/json")]
    public class ServiceController : ControllerBase
    {
        private readonly IServiceService _serviceService;
        private readonly ILogger<ServiceController> _logger;

        public ServiceController(IServiceService serviceService, ILogger<ServiceController> logger)
        {
            _serviceService = serviceService;
            _logger = logger;
        }

        [HttpGet]
        [ProducesResponseType(typeof(IEnumerable<ServiceDto>), StatusCodes.Status200OK)]
        public async Task<ActionResult<IEnumerable<ServiceDto>>> GetAllServices(CancellationToken cancellationToken)
        {
            try
            {
                var services = await _serviceService.GetAllAsync(cancellationToken);
                return Ok(services);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting all services");
                return StatusCode(StatusCodes.Status500InternalServerError, "Internal server error");
            }
        }

        [HttpGet("with-categories")]
        [ProducesResponseType(typeof(IEnumerable<ServiceDto>), StatusCodes.Status200OK)]
        public async Task<ActionResult<IEnumerable<ServiceDto>>> GetAllServicesWithCategories(CancellationToken cancellationToken)
        {
            try
            {
                var services = await _serviceService.GetAllWithRoomCategoriesAsync(cancellationToken);
                return Ok(services);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting services with categories");
                return StatusCode(StatusCodes.Status500InternalServerError, "Internal server error");
            }
        }

        [HttpGet("{id:int}")]
        [ProducesResponseType(typeof(ServiceDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<ServiceDto>> GetService(int id, CancellationToken cancellationToken)
        {
            try
            {
                var service = await _serviceService.GetByIdAsync(id, cancellationToken);

                if (service == null)
                {
                    return NotFound($"Service with ID {id} not found");
                }

                return Ok(service);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting service with ID {ServiceId}", id);
                return StatusCode(StatusCodes.Status500InternalServerError, "Internal server error");
            }
        }

        [HttpGet("{id:int}/with-categories")]
        [ProducesResponseType(typeof(ServiceDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<ServiceDto>> GetServiceWithCategories(int id, CancellationToken cancellationToken)
        {
            try
            {
                var service = await _serviceService.GetWithRoomCategoriesAsync(id, cancellationToken);

                if (service == null)
                {
                    return NotFound($"Service with ID {id} not found");
                }

                return Ok(service);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting service with categories for ID {ServiceId}", id);
                return StatusCode(StatusCodes.Status500InternalServerError, "Internal server error");
            }
        }

        [HttpGet("by-price-range")]
        [ProducesResponseType(typeof(IEnumerable<ServiceDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<IEnumerable<ServiceDto>>> GetServicesByPriceRange(
            [FromQuery] decimal minPrice,
            [FromQuery] decimal maxPrice,
            CancellationToken cancellationToken)
        {
            try
            {
                var services = await _serviceService.GetByPriceRangeAsync(minPrice, maxPrice, cancellationToken);
                return Ok(services);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting services by price range");
                return StatusCode(StatusCodes.Status500InternalServerError, "Internal server error");
            }
        }

        [HttpPost]
        [ProducesResponseType(typeof(ServiceDto), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<ServiceDto>> CreateService([FromBody] CreateServiceDto dto, CancellationToken cancellationToken)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                var service = await _serviceService.CreateAsync(dto, cancellationToken);
                return CreatedAtAction(nameof(GetService), new { id = service.ServiceId }, service);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating service");
                return StatusCode(StatusCodes.Status500InternalServerError, "Internal server error");
            }
        }
        [HttpPut("{id:int}")]
        [ProducesResponseType(typeof(ServiceDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<ServiceDto>> UpdateService(int id, [FromBody] UpdateServiceDto dto, CancellationToken cancellationToken)
        {
            if (id != dto.ServiceId)
            {
                return BadRequest("Service ID in URL doesn't match the ID in request body");
            }

            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                var service = await _serviceService.UpdateAsync(dto, cancellationToken);
                return Ok(service);
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating service with ID {ServiceId}", id);
                return StatusCode(StatusCodes.Status500InternalServerError, "Internal server error");
            }
        }

        [HttpDelete("{id:int}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> DeleteService(int id, CancellationToken cancellationToken)
        {
            try
            {
                var isDeleted = await _serviceService.DeleteAsync(id, cancellationToken);

                if (isDeleted)
                {
                    return NoContent();
                }

                return NotFound($"Service with ID {id} not found");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting service with ID {ServiceId}", id);
                return StatusCode(StatusCodes.Status500InternalServerError, "Internal server error");
            }
        }

        [HttpGet("paged-spec")]
        [ProducesResponseType(typeof(PagedResult<ServiceDto>), StatusCodes.Status200OK)]
        public async Task<ActionResult<PagedResult<ServiceDto>>> GetPagedServicesWithSpecification(
            [FromQuery] ServiceQueryParameters parameters,
            CancellationToken cancellationToken)
        {
            try
            {
                var result = await _serviceService.GetPagedWithSpecificationAsync(parameters, cancellationToken);

                Response.Headers.Append("X-Total-Count", result.TotalCount.ToString());
                Response.Headers.Append("X-Page", result.Page.ToString());
                Response.Headers.Append("X-Page-Size", result.PageSize.ToString());
                Response.Headers.Append("X-Total-Pages", result.TotalPages.ToString());

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting paged services with specification");
                return StatusCode(StatusCodes.Status500InternalServerError, "Internal server error");
            }
        }

        [HttpGet("by-price-range-spec")]
        [ProducesResponseType(typeof(IEnumerable<ServiceDto>), StatusCodes.Status200OK)]
        public async Task<ActionResult<IEnumerable<ServiceDto>>> GetServicesByPriceRangeWithSpec(
            [FromQuery] decimal minPrice,
            [FromQuery] decimal maxPrice,
            CancellationToken cancellationToken)
        {
            try
            {
                var services = await _serviceService.GetByPriceRangeWithSpecificationAsync(minPrice, maxPrice, cancellationToken);
                return Ok(services);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting services by price range with specification");
                return StatusCode(StatusCodes.Status500InternalServerError, "Internal server error");
            }
        }
    }
}