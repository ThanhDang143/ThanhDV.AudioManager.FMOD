#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;

public static class PackageInstaller
{
    // ===================================================================================
    // !!! CHANGE THE URL BELOW !!!
    // Paste the direct URL to your .unitypackage file from GitHub Releases here.
    // ===================================================================================
    private static readonly string packageUrl = "https://github.com/YourUsername/YourRepo/releases/download/v1.0.0/MyAwesomePlugin.unitypackage";

    // Variable to track the ongoing request
    private static UnityWebRequest webRequest;
    // Store the temp file path to use after download completes
    private static string downloadTempPath;

    /// <summary>
    /// This method creates a menu item in the Unity Editor at "Tools/Your Plugin Name/Install or Update".
    /// </summary>
    [MenuItem("Tools/My Awesome Plugin/Install or Update")]
    private static void InstallPackage()
    {
        // Check if another request is already running
        if (webRequest != null)
        {
            EditorUtility.DisplayDialog("Download In Progress", "Another package download is in progress. Please wait.", "OK");
            return;
        }

        Debug.Log("Starting download of package from: " + packageUrl);

        // Determine temporary file save path
        downloadTempPath = Path.Combine(Application.temporaryCachePath, "downloadedPackage.unitypackage");

        // Create a new request to download the file
        webRequest = new UnityWebRequest(packageUrl)
        {
            downloadHandler = new DownloadHandlerFile(downloadTempPath),
            timeout = 600 // Increase timeout to 10 minutes
        };

        // Send the request and register the Update callback to track progress
        webRequest.SendWebRequest();
        EditorApplication.update += OnRequestUpdate;
    }

    /// <summary>
    /// Called repeatedly by EditorApplication.update to track download progress.
    /// </summary>
    private static void OnRequestUpdate()
    {
        // Show a progress bar to the user
        EditorUtility.DisplayProgressBar(
            "Downloading Package",
            $"Downloading package... ({(webRequest.downloadedBytes / 1024f / 1024f):F2} MB)",
            webRequest.downloadProgress
        );

        // Check if the request has finished
        if (webRequest.isDone)
        {
            // Clear the progress bar and unregister the Update callback
            EditorUtility.ClearProgressBar();
            EditorApplication.update -= OnRequestUpdate;

            // Check the result
            if (webRequest.result == UnityWebRequest.Result.Success)
            {
                Debug.Log("Package downloaded successfully! Starting import...");
                AssetDatabase.ImportPackage(downloadTempPath, true); // true to show the import dialog to the user
            }
            else
            {
                Debug.LogError($"Package download failed! Error: {webRequest.error}");
            }

            // Clean up the request
            webRequest.Dispose();
            webRequest = null;
        }
    }
}
#endif