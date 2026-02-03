using Bearcat.Abstractions.Hoster;
using Bearcat.Domain.Entities;

namespace Bearcat.Domain.UseCases.ManageUploads.Dto;

public record FileToUpload(Upload Upload, ArchiveFile ArchiveFile, IHoster Hoster, IHosterConfig HosterConfig);
