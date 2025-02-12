using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MusicServer.Data;
using MusicServer.Models;

namespace MusicServer.Controllers
{
    /// <summary>
    /// Controller for retrieving and updating server settings.
    /// </summary>
    [ApiController]
    [Route("api/settings")]
    public class ServerSettingsController : ControllerBase
    {
        #region Fields

        /// <summary>
        /// The database context used for accessing server settings.
        /// </summary>
        private readonly MusicDbContext _dbContext;

        #endregion

        #region Constructor

        /// <summary>
        /// Initializes a new instance of the <see cref="ServerSettingsController"/> class.
        /// </summary>
        /// <param name="dbContext">The music database context.</param>
        public ServerSettingsController(MusicDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        #endregion

        #region API Endpoints

        /// <summary>
        /// GET /api/settings/server-name
        /// Retrieves the current server name.
        /// </summary>
        /// <returns>An object containing the server name.</returns>
        [HttpGet("server-name")]
        public async Task<IActionResult> GetServerName()
        {
            var settings = await _dbContext.ServerSettings.FirstOrDefaultAsync();

            // Return the default server name if no settings exist.
            if (settings == null)
            {
                return Ok(new { ServerName = "Audiarr" });
            }

            return Ok(new { ServerName = settings.ServerName });
        }

        /// <summary>
        /// PUT /api/settings/server-name
        /// Updates the server name.
        /// </summary>
        /// <param name="updatedSettings">The updated server settings.</param>
        /// <returns>A confirmation message and the updated server name.</returns>
        [HttpPut("server-name")]
        public async Task<IActionResult> UpdateServerName([FromBody] ServerSettings updatedSettings)
        {
            if (string.IsNullOrWhiteSpace(updatedSettings.ServerName))
            {
                return BadRequest("Server name cannot be empty.");
            }

            // Retrieve existing settings.
            var settings = await _dbContext.ServerSettings.FirstOrDefaultAsync();
            if (settings == null)
            {
                // Create new settings if none exist.
                settings = new ServerSettings { ServerName = updatedSettings.ServerName };
                _dbContext.ServerSettings.Add(settings);
            }
            else
            {
                // Update the existing server name.
                settings.ServerName = updatedSettings.ServerName;
            }

            // Persist changes to the database.
            await _dbContext.SaveChangesAsync();
            return Ok(new { Message = "Server name updated successfully", ServerName = settings.ServerName });
        }

        #endregion
    }
}
