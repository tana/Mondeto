using System.IO;
using System.Text;

namespace Mondeto.Core
{

class BlobCache
{
    string directory;

    public BlobCache(string tempDirectory)
    {
        directory = tempDirectory + Path.DirectorySeparatorChar + "blobs";
        if (!Directory.Exists(directory))
        {
            // Create blob cache directory if it does not exist
            Directory.CreateDirectory(directory);
        }
    }

    public void Add(BlobHandle handle, Blob blob)
    {
        string path = HandleToPath(handle);
        if (File.Exists(path))
        {
            Logger.Log("BlobCache", $"File {path} already exists. Ignoring");
            return;
        }

        File.WriteAllBytes(path, blob.Data);
        File.WriteAllText(path + ".info", blob.MimeType);
    }

    public Blob? Find(BlobHandle handle)
    {
        string mimeType = GetMimeType(handle);
        string path = HandleToPath(handle);
        if (mimeType != null && File.Exists(path))
        {
            return new Blob(File.ReadAllBytes(path), mimeType);
        }
        else
        {
            return null;
        }
    }

    public string GetMimeType(BlobHandle handle)
    {
        string path = HandleToPath(handle);
        string infoPath = path + ".info";
        if (File.Exists(infoPath))
        {
            return File.ReadAllText(infoPath);
        }
        else
        {
            return null;
        }
    }

    public string HandleToPath(BlobHandle handle)
    {
        StringBuilder sb = new StringBuilder();
        foreach (byte b in handle.Hash)
        {
            // Format a byte into two-digit hexadecimal
            //  https://docs.microsoft.com/ja-jp/dotnet/csharp/language-reference/tokens/interpolated
            //  https://docs.microsoft.com/ja-jp/dotnet/standard/base-types/standard-numeric-format-strings#the-hexadecimal-x-format-specifier
            sb.Append($"{b:x2}");
        }
        return directory + Path.DirectorySeparatorChar + sb.ToString();
    }
}

} // end namespace