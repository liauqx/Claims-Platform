namespace backend.Services;

public sealed class ClaimDocumentStorage(IWebHostEnvironment environment)
{
    public const long MaximumFileSize = 10 * 1024 * 1024;
    private readonly string _root = Path.Combine(environment.ContentRootPath, "App_Data", "claim-documents");

    public async Task SaveAsync(
        Guid claimId,
        Guid documentId,
        IFormFile file,
        CancellationToken cancellationToken)
    {
        var extension = Path.GetExtension(file.FileName.Replace('\\', '/')).ToLowerInvariant();
        if (file.Length is <= 0 or > MaximumFileSize ||
            extension is not (".pdf" or ".png" or ".jpg" or ".jpeg") ||
            !await HasValidSignatureAsync(file, extension, cancellationToken))
        {
            throw new InvalidDataException("Upload a PDF, PNG, or JPEG file no larger than 10 MB.");
        }

        var directory = Path.Combine(_root, claimId.ToString("N"));
        Directory.CreateDirectory(directory);
        var path = GetPath(claimId, documentId);
        var fileCreated = false;
        var saved = false;
        try
        {
            await using (var output = new FileStream(
                path,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 81920,
                FileOptions.Asynchronous))
            {
                fileCreated = true;
                await using var input = file.OpenReadStream();
                await input.CopyToAsync(output, cancellationToken);
            }

            saved = true;
        }
        finally
        {
            if (fileCreated && !saved)
            {
                File.Delete(path);
            }
        }
    }

    public string GetPath(Guid claimId, Guid documentId) =>
        Path.Combine(_root, claimId.ToString("N"), documentId.ToString("N"));

    public void Delete(Guid claimId, Guid documentId) =>
        File.Delete(GetPath(claimId, documentId));

    private static async Task<bool> HasValidSignatureAsync(
        IFormFile file,
        string extension,
        CancellationToken cancellationToken)
    {
        var signature = new byte[8];
        await using var stream = file.OpenReadStream();
        var bytesRead = await stream.ReadAsync(signature, cancellationToken);

        return extension switch
        {
            ".pdf" => bytesRead >= 5 &&
                signature.AsSpan(0, 5).SequenceEqual("%PDF-"u8),
            ".png" => bytesRead >= 8 &&
                signature.AsSpan().SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }),
            ".jpg" or ".jpeg" => bytesRead >= 3 &&
                signature[0] == 0xFF && signature[1] == 0xD8 && signature[2] == 0xFF,
            _ => false
        };
    }
}
