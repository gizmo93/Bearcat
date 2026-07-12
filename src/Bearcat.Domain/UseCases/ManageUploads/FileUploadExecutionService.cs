using System.Threading.Channels;
using Bearcat.Abstractions.Hoster.Dto;
using Bearcat.Abstractions.Hoster.Exceptions;
using Bearcat.Domain.Shared;
using Bearcat.Domain.UseCases.ManageUploads.Dto;
using Bearcat.Domain.UseCases.ManageUploads.Progress;
using Microsoft.Extensions.Logging;

namespace Bearcat.Domain.UseCases.ManageUploads;

public class FileUploadExecutionService(
    ILogger<FileUploadExecutionService> logger,
    HosterCaptchaVerificationService captchaVerificationService,
    IUploadProgressTracker progressTracker
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
                Id: fileToUpload.ArchiveFileId,
                FullFileName: fileToUpload.FullFileName,
                UploadId: fileToUpload.UploadId,
                FolderId: fileToUpload.FolderId,
                PremiumOnlyDownload: context.Upload.PremiumOnlyDownload,
                Md5Hash: fileToUpload.Md5Hash
            );

            var result = await fileToUpload.Hoster.UploadFileAsync(
                fileDto: fileDto,
                hosterConfig: fileToUpload.HosterConfig,
                progress: new UploadProgressReporter(
                    tracker: progressTracker,
                    uploadId: fileToUpload.UploadId,
                    fileId: fileToUpload.ArchiveFileId
                ),
                cancellationToken: fileUploadCancellationTokenSource.Token
            );

            await resultWriter.WriteAsync(
                new FileUploadCompleted(
                    UploadId: fileToUpload.UploadId,
                    ArchiveFileId: fileToUpload.ArchiveFileId,
                    FullFileName: fileToUpload.FullFileName,
                    FileUrl: result.FileUrl,
                    ExternalId: result.ExternalId,
                    IsSuccess: result.IsSuccess,
                    Errors: result.ErrorMessages
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
                fileToUpload.FullFileName,
                fileToUpload.UploadId,
                FileUploadTimeout.Seconds
            );

            await resultWriter.WriteAsync(
                new FileUploadCompleted(
                    UploadId: fileToUpload.UploadId,
                    ArchiveFileId: fileToUpload.ArchiveFileId,
                    FullFileName: fileToUpload.FullFileName,
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
                    ArchiveFileId: fileToUpload.ArchiveFileId,
                    FullFileName: fileToUpload.FullFileName,
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
                    ArchiveFileId: fileToUpload.ArchiveFileId,
                    FullFileName: fileToUpload.FullFileName,
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
            await resultWriter.WriteAsync(
                new FileUploadCompleted(
                    UploadId: fileToUpload.UploadId,
                    ArchiveFileId: fileToUpload.ArchiveFileId,
                    FullFileName: fileToUpload.FullFileName,
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
