using System.IO.Compression;
using SharpCompress.Readers;

namespace Bearcat.Media.Ffmpeg;

public static class FfmpegArchiveExtractor
{
    public static void ExtractExecutable(
        string archiveFilePath,
        FfmpegArchiveFormat archiveFormat,
        string executableFileName,
        string destinationFilePath
    )
    {
        switch (archiveFormat)
        {
            case FfmpegArchiveFormat.Zip:
                ExtractFromZip(archiveFilePath, executableFileName, destinationFilePath);
                break;
            case FfmpegArchiveFormat.TarXz:
                ExtractFromTarXz(archiveFilePath, executableFileName, destinationFilePath);
                break;
        }
    }

    private static void ExtractFromZip(
        string archiveFilePath,
        string executableFileName,
        string destinationFilePath
    )
    {
        using var archive = ZipFile.OpenRead(archiveFilePath);

        var entry =
            archive.Entries.FirstOrDefault(archiveEntry => archiveEntry.Name == executableFileName)
            ?? throw new InvalidOperationException(
                $"{Path.GetFileName(archiveFilePath)} does not contain {executableFileName}."
            );

        entry.ExtractToFile(destinationFilePath, overwrite: true);
    }

    private static void ExtractFromTarXz(
        string archiveFilePath,
        string executableFileName,
        string destinationFilePath
    )
    {
        using var archiveStream = File.OpenRead(archiveFilePath);
        using var reader = ReaderFactory.OpenReader(archiveStream);

        while (reader.MoveToNextEntry())
        {
            if (
                reader.Entry.IsDirectory
                || Path.GetFileName(reader.Entry.Key) != executableFileName
            )
            {
                continue;
            }

            reader.WriteEntryTo(destinationFilePath);
            return;
        }

        throw new InvalidOperationException(
            $"{Path.GetFileName(archiveFilePath)} does not contain {executableFileName}."
        );
    }
}
