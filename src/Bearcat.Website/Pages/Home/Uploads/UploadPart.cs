using Bearcat.Domain.Shared.Transfers;
using Bearcat.Domain.ValueObjects;

namespace Bearcat.Website.Pages.Home.Uploads;

public sealed record UploadPart(
    string FileName,
    UploadPartState State,
    OnlineState? OnlineState,
    TransferFileProgressSnapshot? Progress
);
