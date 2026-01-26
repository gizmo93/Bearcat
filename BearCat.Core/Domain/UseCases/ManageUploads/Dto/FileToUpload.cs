using BearCat.Core.Domain.Abstractions.Hoster;
using BearCat.Core.Domain.Entities;

namespace BearCat.Core.Domain.UseCases.ManageUploads.Dto;

public record FileToUpload(Upload Upload, ArchiveFile ArchiveFile, IHoster Hoster, IHosterConfig HosterConfig);
