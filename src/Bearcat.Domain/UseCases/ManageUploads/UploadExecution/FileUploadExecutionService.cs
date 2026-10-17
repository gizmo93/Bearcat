using System.Threading.Channels;
using Bearcat.Abstractions.Hoster.Dto;
using Bearcat.Abstractions.Hoster.Exceptions;
using Bearcat.Domain.Shared;
using Bearcat.Domain.Shared.Proxies;
using Bearcat.Domain.Shared.Transfers;
using Microsoft.Extensions.Logging;

namespace Bearcat.Domain.UseCases.ManageUploads.UploadExecution;

public class FileUploadExecutionService(
    ILogger<FileUploadExecutionService> logger,
    HosterCaptchaVerificationService captchaVerificationService,
    HosterCredentialsRejectionService credentialsRejectionService,
    ITransferProgressTracker progressTracker,
    TransferSpeedLimitService transferSpeedLimitService
)
{
    public TimeSpan FileUploadTimeout { get; set; } = Timeout.InfiniteTimeSpan;

    public async Task UploadFileAsync(
        FileToUpload fileToUpload,
        UploadExecutionContext context,
        UploadConcurrencyService concurrencyService,
        SemaphoreSlim hosterSemaphore,
        ChannelWriter<FileUploadCompleted> resultWriter,
        CancellationToken processCancellationToken
    )
    {
        using var fileUploadCancellationTokenSource =
            CancellationTokenSource.CreateLinkedTokenSource(context.CancellationToken);

        if (FileUploadTimeout != Timeout.InfiniteTimeSpan)
        {
            fileUploadCancellationTokenSource.CancelAfter(FileUploadTimeout);
        }

        try
        {
            var fileDto = new FileDto(
                Id: fileToUpload.SourceFile.TransferFileId,
                FullFileName: fileToUpload.SourceFile.FullFileName,
                UploadId: fileToUpload.UploadId,
                FolderId: fileToUpload.FolderId,
                PremiumOnlyDownload: context.Upload.PremiumOnlyDownload,
                Md5Hash: fileToUpload.SourceFile.Md5Hash
            );

            using var proxyScope = HosterRegistrationProxyScope.EnterForUpload(
                context.Upload.HosterRegistration!
            );

            using var speedLimitScope = transferSpeedLimitService.EnterUploadScope(
                context.Upload.HosterRegistration!
            );

            var result = await fileToUpload.Hoster.UploadFileAsync(
                fileDto: fileDto,
                hosterConfig: fileToUpload.HosterConfig,
                progress: new TransferProgressReporter(
                    tracker: progressTracker,
                    identifier: new TransferIdentifier(TransferType.Upload, fileToUpload.UploadId),
                    fileId: fileToUpload.SourceFile.TransferFileId,
                    fileName: Path.GetFileName(fileToUpload.SourceFile.FullFileName),
                    sourceName: context.Upload.HosterRegistration!.Name
                ),
                cancellationToken: fileUploadCancellationTokenSource.Token
            );

            await resultWriter.WriteAsync(
                new FileUploadCompleted(
                    UploadId: fileToUpload.UploadId,
                    SourceFile: fileToUpload.SourceFile,
                    FileUrl: result.FileUrl,
                    ExternalId: result.ExternalId,
                    IsSuccess: result.IsSuccess,
                    Errors: result.ErrorMessages,
                    EmbedUrl: result.EmbedUrl
                ),
                processCancellationToken
            );
        }
        catch (OperationCanceledException) when (processCancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException ex)
            when (fileUploadCancellationTokenSource.IsCancellationRequested
                && !context.CancellationToken.IsCancellationRequested
                && !processCancellationToken.IsCancellationRequested
            )
        {
            var message = $"Upload timed out after {FileUploadTimeout.Seconds} seconds";

            logger.LogWarning(
                ex,
                "Upload for file {FilePath} for upload {UploadId} timed out after {Timeout} seconds",
                fileToUpload.SourceFile.FullFileName,
                fileToUpload.UploadId,
                FileUploadTimeout.Seconds
            );

            await resultWriter.WriteAsync(
                new FileUploadCompleted(
                    UploadId: fileToUpload.UploadId,
                    SourceFile: fileToUpload.SourceFile,
                    FileUrl: null,
                    ExternalId: null,
                    IsSuccess: false,
                    Errors: [message]
                ),
                processCancellationToken
            );
        }
        catch (CaptchaVerificationRequiredException ex)
        {
            captchaVerificationService.MarkRequired(context.Upload, ex.Message);
            context.RequestCancellation();

            await resultWriter.WriteAsync(
                new FileUploadCompleted(
                    UploadId: fileToUpload.UploadId,
                    SourceFile: fileToUpload.SourceFile,
                    FileUrl: null,
                    ExternalId: null,
                    IsSuccess: false,
                    Errors: [ex.Message],
                    WasCanceled: true
                ),
                processCancellationToken
            );
        }
        catch (HosterCredentialsRejectedException ex)
        {
            credentialsRejectionService.DeactivateAndNotify(context.Upload, ex.Message);
            context.RequestCancellation();

            await resultWriter.WriteAsync(
                new FileUploadCompleted(
                    UploadId: fileToUpload.UploadId,
                    SourceFile: fileToUpload.SourceFile,
                    FileUrl: null,
                    ExternalId: null,
                    IsSuccess: false,
                    Errors: [ex.Message],
                    WasCanceled: true
                ),
                processCancellationToken
            );
        }
        catch (OperationCanceledException) when (context.CancellationToken.IsCancellationRequested)
        {
            await resultWriter.WriteAsync(
                new FileUploadCompleted(
                    UploadId: fileToUpload.UploadId,
                    SourceFile: fileToUpload.SourceFile,
                    FileUrl: null,
                    ExternalId: null,
                    IsSuccess: false,
                    Errors: [],
                    WasCanceled: true
                ),
                processCancellationToken
            );
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Upload for file {FilePath} for upload {UploadId} failed unexpectedly: {Message}",
                fileToUpload.SourceFile.FullFileName,
                fileToUpload.UploadId,
                ex.Message
            );

            await resultWriter.WriteAsync(
                new FileUploadCompleted(
                    UploadId: fileToUpload.UploadId,
                    SourceFile: fileToUpload.SourceFile,
                    FileUrl: null,
                    ExternalId: null,
                    IsSuccess: false,
                    Errors: new List<string> { ex.Message }
                ),
                processCancellationToken
            );
        }
        finally
        {
            concurrencyService.ReleaseGlobalSlot();
            hosterSemaphore.Release();
        }
    }
}
