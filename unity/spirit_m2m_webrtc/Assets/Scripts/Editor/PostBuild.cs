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

        // Create directories
        Directory.CreateDirectory(buildPath + "/spirit_unity_Data/peer");
        Directory.CreateDirectory(buildPath + "/spirit_unity_Data/config");
        // Copy config if not exists

        // Copy peer.exe
        File.Copy(Application.dataPath + "/peer/webRTC-peer-win.exe", buildPath + "/spirit_unity_Data/peer/webRTC-peer-win.exe", true);
        if (!File.Exists(buildPath + "/spirit_unity_Data/config/session_config.json"))
        {
            File.Copy(Application.dataPath + "/config/session_config.json", buildPath + "/spirit_unity_Data/config/session_config.json", false);
        }
        Debug.Log(Application.dataPath);
    }
}
