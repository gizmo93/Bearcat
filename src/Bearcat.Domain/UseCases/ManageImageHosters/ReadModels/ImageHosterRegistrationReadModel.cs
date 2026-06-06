namespace Bearcat.Domain.UseCases.ManageImageHosters.ReadModels;

public record ImageHosterRegistrationReadModel(
    int ImageHosterRegistrationId,
    string Name,
    string ImageHosterClassName,
    string ImageHosterName,
    bool IsActive
);
