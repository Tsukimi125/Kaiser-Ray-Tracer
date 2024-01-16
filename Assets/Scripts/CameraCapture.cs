using System.Collections;
using System.Collections.Generic;
using UnityEngine;

//[ExecuteInEditMode]
public class CameraCapture : MonoBehaviour
{
    // 截取的像素尺寸
    public int resWidth = 1920;
    public int resHeight = 1080;
    public Camera captureCamera;

    private bool takeHiResShot = false;

    public static string ScreenShotName(int width, int height)
    {
        // 输出路径和文件名（自带尺寸和日期）
        return string.Format("{0}/screen_{1}x{2}_{3}.png",
                                Application.dataPath,
                                width, height,
                                System.DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss"));
    }

    void OnGUI()
    {
        // 这里用个简单的OnGUI的按钮来触发截屏
        if (GUI.Button(new Rect(10, 10, 100, 30), "Camera Capture"))
        {
            takeHiResShot = true;
            Debug.Log("Shot!");
        }
    }

    void Update()
    {
        if (takeHiResShot)
        {
            // create an renderTexture to save the image data from camera
            RenderTexture rt = new RenderTexture(resWidth, resHeight, 24);
            captureCamera.targetTexture = rt;
            // create an texture2d to recieve the renderTexture data
            Texture2D screenShot = new Texture2D(resWidth, resHeight, TextureFormat.RGB24, false);
            // render from camera
            captureCamera.Render();
            RenderTexture.active = rt;
            screenShot.ReadPixels(new Rect(0, 0, resWidth, resHeight), 0, 0);
            // disable renderTexture to avoid errors
            captureCamera.targetTexture = null;
            RenderTexture.active = null;
            Destroy(rt);
            // Save to png file
            byte[] bytes = screenShot.EncodeToPNG();
            string filename = ScreenShotName(resWidth, resHeight);
            System.IO.File.WriteAllBytes(filename, bytes);

            Debug.Log(string.Format("Took screenshot to: {0}", filename));
            // Set trigger to false to make sure it only runs one time
            takeHiResShot = false;
        }
    }
}