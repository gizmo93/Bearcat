using Bearcat.Abstractions.Media;
using Bearcat.Abstractions.MediaMetadataDatabase;
using Bearcat.Abstractions.NfoDatabase;
using Bearcat.Domain.Shared.MediaMetadataResolution;
using Bearcat.Domain.Shared.UnmanagedReleases;
using Bearcat.Domain.UseCases.AutomateReleaseCreation.Creation;
using Bearcat.Domain.UseCases.ManageReleaseCollections;
using Bearcat.Domain.UseCases.ManageReleases;
using Bearcat.Domain.UseCases.ManageReleases.ReleaseInfoResolution;
using Bearcat.Infrastructure.Database;
using Bearcat.Infrastructure.Database.Repositories;
using Bearcat.Infrastructure.FileSystem;
using Bearcat.Infrastructure.Security;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using TimeProvider = Bearcat.Domain.Shared.TimeProvider;

namespace Bearcat.Domain.IntegrationTest.Shared.ReleaseCreation;

public static class ReleaseFromFolderCreationServiceFactory
{
    public static ReleaseFromFolderCreationService Create(
        BearcatDbContext dbContext,
        UnmanagedReleaseArchiveInitializationService unmanagedReleaseArchiveInitializationService,
        TimeProvider timeProvider
    )
    {
        var nfoDatabaseFactory = new Mock<INfoDatabaseFactory>(MockBehavior.Strict);
        nfoDatabaseFactory
            .Setup(factory => factory.GetByClassName())
            .Returns(new Dictionary<string, INfoDatabase>());
        var releaseInfoRepository = new ReleaseInfoRepository(
            dbContext,
            dbContext,
            NoOpSecretProtector.Instance
        );
        var classificationService = new ReleaseClassificationService(
            new ReleaseClassificationRepository(dbContext),
            timeProvider,
            NullLogger<ReleaseClassificationService>.Instance
        );
        var mediaMetadataExtractor = new Mock<IMediaMetadataExtractor>();
        mediaMetadataExtractor
            .Setup(extractor =>
                extractor.ExtractAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())
            )
            .ReturnsAsync((MediaProbeResult?)null);

        return new ReleaseFromFolderCreationService(
            releaseInfoResolutionService: new ReleaseInfoResolutionService(
                releaseInfoRepository,
                nfoDatabaseFactory.Object,
                new ReleaseNfoResolver(
                    nfoDatabaseFactory.Object,
                    NullLogger<ReleaseNfoResolver>.Instance
                ),
                new ReleaseInfoResolver(
                    releaseInfoRepository,
                    nfoDatabaseFactory.Object,
                    NullLogger<ReleaseInfoResolver>.Instance
                ),
                new ReleaseMetadataResolver(
                    new MediaMetadataResolver(
                        new MediaMetadataResolverRepository(
                            dbContext,
                            NoOpSecretProtector.Instance
                        ),
                        new Mock<IMediaMetadataDatabaseFactory>(MockBehavior.Strict).Object,
                        NullLogger<MediaMetadataResolver>.Instance
                    ),
                    nfoDatabaseFactory.Object,
                    NullLogger<ReleaseMetadataResolver>.Instance
                ),
                classificationService,
                NullLogger<ReleaseInfoResolutionService>.Instance,
                timeProvider
            ),
            mediaMetadataService: new MediaMetadataService(
                new MediaMetadataRepository(dbContext),
                mediaMetadataExtractor.Object,
                new FileSystemService(),
                classificationService,
                timeProvider,
                NullLogger<MediaMetadataService>.Instance
            ),
            unmanagedReleaseArchiveInitializationService: unmanagedReleaseArchiveInitializationService,
            releaseCollectionAssigner: new ReleaseCollectionAssignmentService(
                new ReleaseCollectionRepository(
                    dbRead: dbContext,
                    dbWrite: dbContext,
                    metadataDatabaseFactory: Mock.Of<IMediaMetadataDatabaseFactory>()
                ),
                timeProvider
            )
        );
    }
}
