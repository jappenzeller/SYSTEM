using UnityEngine;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using System.IO;

public class WebGLTemplatePostProcessor : IPostprocessBuildWithReport
{
    public int callbackOrder => 1;

    public void OnPostprocessBuild(BuildReport report)
    {
        // Only process WebGL builds
        if (report.summary.platform != BuildTarget.WebGL)
            return;

        string buildPath = report.summary.outputPath;
        string indexPath = Path.Combine(buildPath, "index.html");

        if (!File.Exists(indexPath))
        {
            Debug.LogWarning($"[WebGLTemplatePostProcessor] index.html not found at: {indexPath}");
            return;
        }

        Debug.Log($"[WebGLTemplatePostProcessor] Processing template variables in: {indexPath}");

        // Read the index.html file
        string content = File.ReadAllText(indexPath);

        // Get the build directory name (Local, Test, or Production)
        string buildDirName = Path.GetFileName(buildPath);

        // Replace Unity template variables
        content = content.Replace("%UNITY_WEB_NAME%", PlayerSettings.productName);
        content = content.Replace("%UNITY_PRODUCT_NAME%", PlayerSettings.productName);
        content = content.Replace("%UNITY_COMPANY_NAME%", PlayerSettings.companyName);
        content = content.Replace("%UNITY_VERSION%", Application.unityVersion);
        content = content.Replace("%UNITY_WIDTH%", PlayerSettings.defaultWebScreenWidth.ToString());
        content = content.Replace("%UNITY_HEIGHT%", PlayerSettings.defaultWebScreenHeight.ToString());
        content = content.Replace("%UNITY_WEBGL_LOADER_URL%", $"{buildDirName}.loader.js");
        content = content.Replace("%UNITY_WEBGL_BUILD_URL%", buildDirName);

        // Write back the modified content
        File.WriteAllText(indexPath, content);

        Debug.Log($"[WebGLTemplatePostProcessor] ✅ Template variables replaced successfully");
        Debug.Log($"  Product Name: {PlayerSettings.productName}");
        Debug.Log($"  Company Name: {PlayerSettings.companyName}");
        Debug.Log($"  Resolution: {PlayerSettings.defaultWebScreenWidth}x{PlayerSettings.defaultWebScreenHeight}");
    }
}
