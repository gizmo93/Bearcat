namespace Bearcat.Domain.UseCases.ManageAdditionalArchiveContents.Validation;

public enum AdditionalArchiveContentValidationError
{
    NameRequired = 1,
    NameTooLong = 2,
    NameAlreadyExists = 3,
    SourcePathRequired = 4,
    SourcePathTooLong = 5,
    SourcePathNotFound = 6,
    FileNameRequired = 7,
    FileNameTooLong = 8,
    FileNameInvalid = 9,
    FileNameReserved = 10,
    TextContentRequired = 11,
}
