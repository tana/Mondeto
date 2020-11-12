using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEditor.Callbacks;

public class PostBuildOperations
{
    // Copy license and credits into build artifact directory.
    // See https://docs.unity3d.com/ja/2019.4/ScriptReference/Callbacks.PostProcessBuildAttribute.html
    [PostProcessBuild(100)]
    public static void CopyLicenseFilesAfterBuild(BuildTarget target, string path)
    {
        if (target == BuildTarget.StandaloneWindows || target == BuildTarget.StandaloneWindows64)
        {
            CopyLicenseFiles(Path.GetDirectoryName(path));
        }
        // TODO: support other platforms
    }

    /*
    // This is added to Unity Editor menu
    // See https://docs.unity3d.com/2019.4/Documentation/ScriptReference/MenuItem.html
    [MenuItem("Mondeto/Dry-run CopyLicenseFiles")]
    public static void TestCopyLicenseFiles()
    {
        CopyLicenseFiles("../Mondeto-build", dryRun: true);
    }
    */

    static void CopyLicenseFiles(string path, bool dryRun = false)
    {
        CopyFile(new FileInfo("LICENSE"), new DirectoryInfo(path), dryRun);

        var destDirInfo = new DirectoryInfo(path + Path.DirectorySeparatorChar + "credits");
        if (destDirInfo.Exists)
        {
            Debug.Log("Deleting " + destDirInfo.FullName);

            if (!dryRun)
            {
                destDirInfo.Delete(true);   // recursively delete
            }
        }
        CopyDirectory(new DirectoryInfo("credits"), destDirInfo, dryRun);
    }

    static void CopyDirectory(DirectoryInfo origDirInfo, DirectoryInfo destDirInfo, bool dryRun)
    {
        Debug.Log("Creating " + destDirInfo.FullName);
        destDirInfo.Create();

        foreach (var fileInfo in origDirInfo.EnumerateFiles())
        {
            CopyFile(fileInfo, destDirInfo, dryRun);
        }

        foreach (var childDirInfo in origDirInfo.EnumerateDirectories())
        {
            CopyDirectory(childDirInfo, new DirectoryInfo(destDirInfo.FullName + Path.DirectorySeparatorChar + childDirInfo.Name), dryRun);
        }
    }

    static void CopyFile(FileInfo fileInfo, DirectoryInfo destDirInfo, bool dryRun = false)
    {
        var destFilePath = destDirInfo.FullName + Path.DirectorySeparatorChar + fileInfo.Name;
        Debug.Log("Copying into " + destFilePath);

        if (!dryRun)
        {
            fileInfo.CopyTo(destFilePath, true);   // overwrite
        }
    }
}