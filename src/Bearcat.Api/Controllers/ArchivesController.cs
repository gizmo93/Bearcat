using Bearcat.Api.Contracts.Archives;
using Bearcat.Domain.UseCases.ManageArchives.Repositories;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Bearcat.Api.Controllers;

[ApiController]
[Route("api/v1/archives")]
public class ArchivesController(IArchiveReadRepository archiveReadRepository) : ControllerBase
{
    [HttpGet("{archiveId:int}")]
    [ProducesResponseType(typeof(ArchiveResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ArchiveResponse>> GetAsync(
        int archiveId,
        CancellationToken cancellationToken = default
    )
    {
        var archive = await archiveReadRepository.GetByIdAsync(archiveId, cancellationToken);

        if (archive is null)
        {
            return NotFound();
        }

        return Ok(ArchiveResponse.FromReadModel(archive));
    }
}
