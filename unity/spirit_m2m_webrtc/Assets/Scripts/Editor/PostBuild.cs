using System.Collections;
using System.Collections.Generic;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using System.IO;

public class PostBuild : IPostprocessBuildWithReport
{
    public int callbackOrder { get { return 0; } }

    public void OnPostprocessBuild(BuildReport report)
    {
        string buildPath = Path.GetDirectoryName(report.summary.outputPath);
        
        // Determine the data folder name based on build target
        string dataFolderName = report.summary.platform == UnityEditor.BuildTarget.StandaloneLinux64 
            ? "Build_Data" 
            : "spirit_unity_Data";
        
        string dataFolder = Path.Combine(buildPath, dataFolderName);
        
        // Copy entire peer directory
        string sourcePeerDir = Path.Combine(Application.dataPath, "peer");
        string targetPeerDir = Path.Combine(dataFolder, "peer");
        CopyDirectory(sourcePeerDir, targetPeerDir, true);
        Debug.Log($"Copied peer directory to {targetPeerDir}");
        
        // Copy entire config directory
        string sourceConfigDir = Path.Combine(Application.dataPath, "config");
        string targetConfigDir = Path.Combine(dataFolder, "config");

        CopyDirectory(sourceConfigDir, targetConfigDir, true);
        Debug.Log($"Copied config directory to {targetConfigDir}");
    }
    
    private static void CopyDirectory(string sourceDir, string targetDir, bool recursive)
    {
        // Get the source directory info
        DirectoryInfo dir = new DirectoryInfo(sourceDir);
        
        if (!dir.Exists)
        {
            throw new DirectoryNotFoundException($"Source directory not found: {dir.FullName}");
        }
        
        // Create the target directory if it doesn't exist
        Directory.CreateDirectory(targetDir);
        
        // Copy all files in the directory
        foreach (FileInfo file in dir.GetFiles())
        {
            string targetFilePath = Path.Combine(targetDir, file.Name);
            file.CopyTo(targetFilePath, true);
        }
        
        // Recursively copy subdirectories if requested
        if (recursive)
        {
            foreach (DirectoryInfo subDir in dir.GetDirectories())
            {
                string newTargetDir = Path.Combine(targetDir, subDir.Name);
                CopyDirectory(subDir.FullName, newTargetDir, true);
            }
        }
    }
}
