using System.Security.Cryptography;

public struct Blob
{
    public string MimeType { get; private set; }
    public byte[] Data { get; private set; }

    byte[] hash;

    public Blob(byte[] data, string mimeType)
    {
        Data = data;
        MimeType = mimeType;
        hash = null;
    }

    public BlobHandle GenerateHandle()
    {
        if (hash == null)
        {
            // SHA-256 cryptographic hash function
            //    https://docs.microsoft.com/ja-jp/dotnet/api/system.security.cryptography.sha256?view=netcore-3.1
            using (var sha = SHA256.Create())
            {
                hash = sha.ComputeHash(Data);
            }
        }

        return new BlobHandle { Hash = hash };
    }
}

