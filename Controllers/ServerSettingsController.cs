using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MusicServer.Data;
using MusicServer.Models;

namespace MusicServer.Controllers
{
    [ApiController]
    [Route("api/settings")]
    public class ServerSettingsController : ControllerBase
    {
        private readonly MusicDbContext _dbContext;

        public ServerSettingsController(MusicDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        /// <summary>
        /// GET /api/settings/server-name
        /// Returns the current server name.
        /// </summary>
        [HttpGet("server-name")]
        public async Task<IActionResult> GetServerName()
        {
            var settings = await _dbContext.ServerSettings.FirstOrDefaultAsync();
            if (settings == null)
            {
                return Ok(new { ServerName = "Audiarr" }); // Default value
            }

            return Ok(new { ServerName = settings.ServerName });
        }

        /// <summary>
        /// PUT /api/settings/server-name
        /// Updates the server name.
        /// </summary>
        [HttpPut("server-name")]
        public async Task<IActionResult> UpdateServerName([FromBody] ServerSettings updatedSettings)
        {
            if (string.IsNullOrWhiteSpace(updatedSettings.ServerName))
            {
                return BadRequest("Server name cannot be empty.");
            }

            var settings = await _dbContext.ServerSettings.FirstOrDefaultAsync();
            if (settings == null)
            {
                settings = new ServerSettings { ServerName = updatedSettings.ServerName };
                _dbContext.ServerSettings.Add(settings);
            }
            else
            {
                settings.ServerName = updatedSettings.ServerName;
            }

            await _dbContext.SaveChangesAsync();
            return Ok(new { Message = "Server name updated successfully", ServerName = settings.ServerName });
        }
    }
}
