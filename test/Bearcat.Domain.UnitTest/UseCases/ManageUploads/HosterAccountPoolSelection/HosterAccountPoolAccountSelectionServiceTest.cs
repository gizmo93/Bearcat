using System.Globalization;
using System.Linq.Expressions;
using Bearcat.Abstractions.Hoster;
using Bearcat.Abstractions.Hoster.Results;
using Bearcat.Domain.Entities;
using Bearcat.Domain.Shared;
using Bearcat.Domain.UseCases.ManageUploads.HosterAccountPoolSelection;
using Bearcat.Domain.UseCases.ManageUploads.Repositories;
using Bearcat.Domain.ValueObjects;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Shouldly;
using TimeProvider = Bearcat.Domain.Shared.TimeProvider;

namespace Bearcat.Domain.UnitTest.UseCases.ManageUploads.HosterAccountPoolSelection;

public class HosterAccountPoolAccountSelectionServiceTest
{
    private const string HosterClassName = "PoolHoster";
    private static readonly DateTime Now = new(2026, 10, 14, 12, 0, 0, DateTimeKind.Utc);

    private Mock<IHosterWithStorageQuota> hosterMock = null!;
    private Mock<IUploadFilesRepository> repositoryMock = null!;
    private Mock<INotificationService> notificationServiceMock = null!;
    private Dictionary<int, IHosterConfig> hosterConfigsByRegistrationId = null!;
    private HosterAccountPoolAccountSelectionService service = null!;

    [SetUp]
    public void SetUp()
    {
        hosterMock = new Mock<IHosterWithStorageQuota>();
        hosterMock
            .Setup(hoster => hoster.DeserializeHosterConfig(It.IsAny<string>()))
            .Returns(
                (string serializedConfig) =>
                    hosterConfigsByRegistrationId[
                        int.Parse(serializedConfig, CultureInfo.InvariantCulture)
                    ]
            );

        var hosterFactoryMock = new Mock<IHosterFactory>();
        hosterFactoryMock
            .Setup(factory => factory.GetByName(HosterClassName))
            .Returns(hosterMock.Object);

        repositoryMock = new Mock<IUploadFilesRepository>();
        notificationServiceMock = new Mock<INotificationService>();
        hosterConfigsByRegistrationId = [];

        var timeProviderMock = new Mock<TimeProvider>(new ConfigurationBuilder().Build());
        timeProviderMock.Setup(provider => provider.GetLocalNow()).Returns(Now);

        service = new HosterAccountPoolAccountSelectionService(
            repositoryMock.Object,
            hosterFactoryMock.Object,
            timeProviderMock.Object,
            notificationServiceMock.Object,
            new HosterCredentialsRejectionService(notificationServiceMock.Object),
            new HosterCaptchaVerificationService(notificationServiceMock.Object),
            new HosterStorageAvailabilityCache(),
            NullLogger<HosterAccountPoolAccountSelectionService>.Instance
        );
    }

    [Test]
    public async Task SelectHosterRegistrationAsync_FirstAccountUnusableAndSecondTooSmall_AssignsNextAccountByPriority()
    {
        // Arrange
        var accountRequiringCaptcha = CreateRegistration(1);
        accountRequiringCaptcha.RequiresCaptchaVerification = true;
        var tooSmallAccount = CreateRegistration(2);
        var fittingAccount = CreateRegistration(3);
        var upload = CreateUpload(
            minimumFreeSpaceBytes: 0,
            (fittingAccount, 30),
            (accountRequiringCaptcha, 10),
            (tooSmallAccount, 20)
        );
        SetupQuota(accountRequiringCaptcha, HosterStorageQuota.Limited(10_000, null));
        SetupQuota(tooSmallAccount, HosterStorageQuota.Limited(99, null));
        SetupQuota(fittingAccount, HosterStorageQuota.Limited(100, null));

        // Act
        var result = await SelectAsync(upload, requiredBytes: 100);

        // Assert
        result.ShouldBe(fittingAccount);
        VerifyQuotaRequested(accountRequiringCaptcha, Times.Never());
    }

    [Test]
    public async Task SelectHosterRegistrationAsync_UnlimitedAccount_AssignsAccountRegardlessOfSize()
    {
        // Arrange
        var unlimitedAccount = CreateRegistration(1);
        var upload = CreateUpload(minimumFreeSpaceBytes: 1_000, (unlimitedAccount, 1));
        SetupQuota(unlimitedAccount, HosterStorageQuota.Unlimited());

        // Act
        var result = await SelectAsync(upload, requiredBytes: long.MaxValue / 2);

        // Assert
        result.ShouldBe(unlimitedAccount);
    }

    [TestCase(500, true)]
    [TestCase(501, false)]
    public async Task SelectHosterRegistrationAsync_MinimumFreeSpaceAndReservedBytes_DecideWhetherAccountFits(
        long requiredBytes,
        bool expectedToFit
    )
    {
        // Arrange
        var account = CreateRegistration(1);
        var upload = CreateUpload(minimumFreeSpaceBytes: 200, (account, 1));
        SetupQuota(account, HosterStorageQuota.Limited(1_000, null));

        // Act
        var result = await SelectAsync(
            upload,
            requiredBytes,
            remainingBytesOfRunningUploads: new Dictionary<int, long> { [account.Id] = 300 }
        );

        // Assert
        (result == account).ShouldBe(expectedToFit);
    }

    [Test]
    public async Task SelectHosterRegistrationAsync_QuotaRequestFails_SkipsAccountWithoutRetryingWithinCacheDuration()
    {
        // Arrange
        var failingAccount = CreateRegistration(1);
        var fallbackAccount = CreateRegistration(2);
        var upload = CreateUpload(
            minimumFreeSpaceBytes: 0,
            (failingAccount, 1),
            (fallbackAccount, 2)
        );
        hosterConfigsByRegistrationId[failingAccount.Id] = Mock.Of<IHosterConfig>();
        hosterMock
            .Setup(hoster =>
                hoster.GetStorageQuotaAsync(
                    hosterConfigsByRegistrationId[failingAccount.Id],
                    It.IsAny<CancellationToken>()
                )
            )
            .ThrowsAsync(new HttpRequestException("API unavailable"));
        SetupQuota(fallbackAccount, HosterStorageQuota.Limited(1_000, null));

        // Act
        var firstResult = await SelectAsync(upload, requiredBytes: 100);
        var secondResult = await SelectAsync(upload, requiredBytes: 100);

        // Assert
        firstResult.ShouldBe(fallbackAccount);
        secondResult.ShouldBe(fallbackAccount);
        VerifyQuotaRequested(failingAccount, Times.Once());
        VerifyQuotaRequested(fallbackAccount, Times.Once());
    }

    [Test]
    public async Task SelectHosterRegistrationAsync_NoAccountFitsTwice_CreatesHosterAccountPoolFullNotificationOnce()
    {
        // Arrange
        var account = CreateRegistration(1);
        var upload = CreateUpload(minimumFreeSpaceBytes: 0, (account, 1));
        SetupQuota(account, HosterStorageQuota.Limited(50, null));
        repositoryMock
            .SetupSequence(repository =>
                repository.HasUnresolvedNotificationForUploadAsync(
                    upload.Id,
                    NotificationKind.HosterAccountPoolFull,
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(false)
            .ReturnsAsync(true);

        // Act
        var firstResult = await SelectAsync(upload, requiredBytes: 100);
        var secondResult = await SelectAsync(upload, requiredBytes: 100);

        // Assert
        firstResult.ShouldBeNull();
        secondResult.ShouldBeNull();
        notificationServiceMock.Verify(
            notificationService =>
                notificationService.Create(
                    NotificationKind.HosterAccountPoolFull,
                    It.Is<string>(message => message.Contains("'Pool'")),
                    upload,
                    It.IsAny<Expression<Func<Notification, Upload?>>>()
                ),
            Times.Once()
        );
    }

    [Test]
    public async Task SelectHosterRegistrationAsync_SecondUploadInSameCycle_AssignsNextAccountBecauseFirstAssignmentReservedCachedFreeSpace()
    {
        // Arrange
        var firstAccount = CreateRegistration(1);
        var secondAccount = CreateRegistration(2);
        var firstUpload = CreateUpload(
            minimumFreeSpaceBytes: 0,
            (firstAccount, 1),
            (secondAccount, 2)
        );
        var secondUpload = CreateUpload(
            minimumFreeSpaceBytes: 0,
            (firstAccount, 1),
            (secondAccount, 2)
        );
        SetupQuota(firstAccount, HosterStorageQuota.Limited(150, null));
        SetupQuota(secondAccount, HosterStorageQuota.Limited(1_000, null));

        // Act
        var firstResult = await SelectAsync(firstUpload, requiredBytes: 100);
        var secondResult = await SelectAsync(secondUpload, requiredBytes: 100);

        // Assert
        firstResult.ShouldBe(firstAccount);
        secondResult.ShouldBe(secondAccount);
        VerifyQuotaRequested(firstAccount, Times.Once());
    }

    [Test]
    public async Task SelectHosterRegistrationAsync_NoMemberUsable_CreatesHosterAccountPoolWithoutUsableAccountNotification()
    {
        // Arrange
        var inactiveAccount = CreateRegistration(1);
        inactiveAccount.IsActive = false;
        var accountRequiringCaptcha = CreateRegistration(2);
        accountRequiringCaptcha.RequiresCaptchaVerification = true;
        var upload = CreateUpload(
            minimumFreeSpaceBytes: 0,
            (inactiveAccount, 1),
            (accountRequiringCaptcha, 2)
        );
        SetupQuota(inactiveAccount, HosterStorageQuota.Unlimited());
        SetupQuota(accountRequiringCaptcha, HosterStorageQuota.Unlimited());

        // Act
        var result = await SelectAsync(upload, requiredBytes: 100);

        // Assert
        result.ShouldBeNull();
        notificationServiceMock.Verify(
            notificationService =>
                notificationService.Create(
                    NotificationKind.HosterAccountPoolWithoutUsableAccount,
                    It.Is<string>(message => message.Contains("'Pool'")),
                    upload,
                    It.IsAny<Expression<Func<Notification, Upload?>>>()
                ),
            Times.Once()
        );
        notificationServiceMock.Verify(
            notificationService =>
                notificationService.Create(
                    NotificationKind.HosterAccountPoolFull,
                    It.IsAny<string>(),
                    It.IsAny<Upload>(),
                    It.IsAny<Expression<Func<Notification, Upload?>>>()
                ),
            Times.Never()
        );
    }

    private async Task<HosterRegistration?> SelectAsync(
        Upload upload,
        long requiredBytes,
        IReadOnlyDictionary<int, long>? remainingBytesOfRunningUploads = null
    )
    {
        return await service.SelectHosterRegistrationAsync(
            upload: upload,
            requiredBytes: requiredBytes,
            hosterConfigsByRegistrationId: hosterConfigsByRegistrationId.ToDictionary(
                entry => entry.Key,
                entry => entry.Key.ToString(CultureInfo.InvariantCulture)
            ),
            remainingBytesOfRunningUploadsByHosterRegistrationId: remainingBytesOfRunningUploads
                ?? new Dictionary<int, long>(),
            cancellationToken: CancellationToken.None
        );
    }

    private void SetupQuota(HosterRegistration registration, HosterStorageQuota quota)
    {
        var hosterConfig = Mock.Of<IHosterConfig>();
        hosterConfigsByRegistrationId[registration.Id] = hosterConfig;
        hosterMock
            .Setup(hoster =>
                hoster.GetStorageQuotaAsync(hosterConfig, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(quota);
    }

    private void VerifyQuotaRequested(HosterRegistration registration, Times times)
    {
        hosterMock.Verify(
            hoster =>
                hoster.GetStorageQuotaAsync(
                    hosterConfigsByRegistrationId[registration.Id],
                    It.IsAny<CancellationToken>()
                ),
            times
        );
    }

    private static HosterRegistration CreateRegistration(int id) =>
        new()
        {
            Id = id,
            Name = $"Account {id}",
            SerializedConfig = id.ToString(CultureInfo.InvariantCulture),
            HosterClassName = HosterClassName,
            IsActive = true,
        };

    private static Upload CreateUpload(
        long minimumFreeSpaceBytes,
        params (HosterRegistration Registration, int Priority)[] members
    )
    {
        var hosterAccountPool = new HosterAccountPool
        {
            Id = 1,
            Name = "Pool",
            HosterClassName = HosterClassName,
            MinimumFreeSpaceBytes = minimumFreeSpaceBytes,
            Members = members
                .Select(member => new HosterAccountPoolMember
                {
                    HosterRegistrationId = member.Registration.Id,
                    HosterRegistration = member.Registration,
                    Priority = member.Priority,
                })
                .ToList(),
        };

        return new Upload
        {
            Id = 7,
            UploadConfig = new UploadConfig
            {
                UploadSource = UploadSource.ArchiveFiles,
                Name = "Upload config",
                Release = new Release { Name = "Bearcat.Release.001" },
                HosterAccountPoolId = hosterAccountPool.Id,
                HosterAccountPool = hosterAccountPool,
            },
        };
    }
}
