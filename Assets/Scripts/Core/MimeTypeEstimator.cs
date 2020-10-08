using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text;

// Class for estimating MIME type from file extension
public class MimeTypeEstimator
{
    Dictionary<string, string> extensionToMimeType = new Dictionary<string, string>();

    public MimeTypeEstimator(string database)
    {
        // Load Apache's MIME type database file
        using (var reader = new StreamReader(database))
        {
            while (!reader.EndOfStream)
            {
                string line = reader.ReadLine();
                // Remove comment (chars after #)
                int sharpIdx = line.IndexOf('#');
                if (sharpIdx >= 0)
                {
                    line = line.Substring(0, sharpIdx);
                }
                // Split MIME type and extension list
                string[] parts = line.Split(new [] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 2) continue; // Skip if the line is empty or MIME type only
                string mimeType = parts[0];
                foreach (string ext in parts.Skip(1))
                {
                    extensionToMimeType[ext] = mimeType;
                }
            }
        }
    }

    public string EstimateFromFilename(string filename)
    {
        string ext = Path.GetExtension(filename).ToLower();
        // Remove first '.' if exist, because GetExtension returns '.foo' (or empty string if there is no extension)
        // See: https://docs.microsoft.com/ja-jp/dotnet/api/system.io.path.getextension?view=netcore-3.1
        if (ext[0] == '.') ext = ext.Substring(1); 
        if (extensionToMimeType.ContainsKey(ext))
        {
            return extensionToMimeType[ext];
        }
        else
        {
            // Currently, we use "application/octet-stream" as a sign of unknown type.
            //  https://www.iana.org/assignments/media-types/application/octet-stream
            //  https://wiki.developer.mozilla.org/en-US/docs/Web/HTTP/Basics_of_HTTP/MIME_types$revision/1589213
            return "application/octet-stream";
        }
    }
}