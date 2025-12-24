using Microsoft.AspNetCore.Mvc;
using VisualSoft.Biomatric.Identification.Domain.Models;
using VisualSoft.Biomatric.Identification.Services;

namespace VisualSoft.Biomatric.Identification.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class IdentificationController : ControllerBase
    {
        private readonly IBiometricService _biometricService;
        private readonly ILogger<IdentificationController> _logger;

        public IdentificationController(
            IBiometricService biometricService,
            ILogger<IdentificationController> logger)
        {
            _biometricService = biometricService;
            _logger = logger;
        }

        /// <summary>
        /// Identifies a fingerprint from uploaded WSQ file
        /// </summary>
        [HttpPost("identify")]
        public async Task<ActionResult<IdentificationResult>> Identify(IFormFile file)
        {
           
            if (file == null || file.Length == 0)
            {
                return BadRequest(new { success = false, error = "No file uploaded" });
            }

            var extension = Path.GetExtension(file.FileName)?.ToLowerInvariant();
            if (extension != ".wsq")
            {
                return BadRequest(new { success = false, error = "Only .wsq files are supported" });
            }

            string tempPath = null;
            try
            {
                tempPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.wsq");

                using (var stream = System.IO.File.Create(tempPath))
                {
                    await file.CopyToAsync(stream);
                }

                var result = await _biometricService.IdentifyAsync(tempPath);

                if (result.Success && !string.IsNullOrEmpty(result.MatchedSubjectId))
                {
                    _logger.LogInformation("[{RequestId}] Match Found - Subject: {SubjectId}, Score: {Score}",result.MatchingScore);
                }
                else
                {
                    _logger.LogInformation("[{RequestId}] No Match - Status: {Status}", result.Status);
                }

                return Ok(new
                {
                    success = result.Success,
                    data = result,
                   
                    timestamp = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[{RequestId}] Error during identification");
                return StatusCode(500, new
                {
                    success = false,
                    error = ex.Message,
                    
                });
            }
           
        }

    }
}