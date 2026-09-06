using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System.IO;

namespace SimpleCapture
{
    public class SimpleCapture : EditorWindow
    {
        readonly int[,] prestSize = new int[,] { { 720,480 }, {1280,720}, {1920,1080}, {2560,1440}, {3840,2160}, {2048,1080}, {4096,2160}, {8192,4320} };

        private enum resPreset {
            [InspectorName("SD (720x480)")] SD,
            [InspectorName("HD (1280x720)")] HD,
            [InspectorName("FHD (1920x1080)")] FHD,
            [InspectorName("QHD (2560x1440)")] QHD,
            [InspectorName("UHD (3840x2160)")] UHD,
            [InspectorName("2k (2048x1080)")] _2k,
            [InspectorName("4k (4096x2160)")] _4k,
            [InspectorName("8k (8192x4320)")] _8k,
            [InspectorName("Custom")] Custom
        }
        private resPreset res = resPreset.FHD;

        private enum camOrientationPreset { Horizon, Vertical }
        private camOrientationPreset camOrientaion = camOrientationPreset.Horizon;

        private Camera camera;

        private enum BGType { Default, Skybox, Color, Transparent }
        private BGType bgType = BGType.Default;
        private Color bgColor = Color.white;

        Vector2 captureSize = new Vector2(1920, 1080);
        

        // Main
        [MenuItem("Curiss/Simple Capture")]
        public static void ShowWindow()
        {
            EditorWindow window = GetWindow<SimpleCapture>("Simple Capture");
            window.minSize = new Vector2(300, 150);
        }

        // GUI
        void OnGUI()
        {
            // 타이틀.
            GUILayout.Space(7);
            Rect controlRect = EditorGUILayout.GetControlRect();
            GUI.Label(controlRect, "SimpleCapture", new GUIStyle() { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold, fontSize = 16, normal = EditorStyles.label.normal });

            // 크레딧.
            GUIContent credit = new GUIContent("by Curiss");
            Vector2 labelSize = EditorStyles.label.CalcSize(credit);
            Rect creditRect = new Rect(controlRect.width - labelSize.x + 10, controlRect.y, labelSize.x, labelSize.y);

            if (GUI.Button(creditRect, credit, new GUIStyle(EditorStyles.label) { alignment = TextAnchor.LowerLeft, fontSize = 10 }))
            {
                Application.OpenURL("https://twitter.com/_Curiss");
            }

            GuiLine();

            ////////////// Inspector //////////////
            // 카메라.
            camera = (Camera)EditorGUILayout.ObjectField("Camera", camera, typeof(Camera), true);

            GUILayout.Space(5);

            // 해상도 설정.
            res = (resPreset)EditorGUILayout.EnumPopup("Resolution", res);
            if (res != resPreset.Custom)
            {
                camOrientaion = (camOrientationPreset)EditorGUILayout.EnumPopup(" ", camOrientaion);

                // 가로, 세로 설정.
                if (camOrientaion == camOrientationPreset.Horizon)
                {
                    captureSize.x = prestSize[(int)res, 0];
                    captureSize.y = prestSize[(int)res, 1];
                }
                else
                {
                    captureSize.x = prestSize[(int)res, 1];
                    captureSize.y = prestSize[(int)res, 0];
                }
            }
            else
            {
                EditorGUILayout.BeginHorizontal();
                GUILayout.Label("Size");
                captureSize = EditorGUILayout.Vector2Field("", captureSize, GUILayout.Width(EditorGUIUtility.currentViewWidth-EditorGUIUtility.labelWidth));
                EditorGUILayout.EndHorizontal();
            }

            GUILayout.Space(5);

            // 배경.
            bgType  = (BGType)EditorGUILayout.EnumPopup("Background", bgType);
            
            // 배경 색.
            if (bgType == BGType.Color)
                bgColor = EditorGUILayout.ColorField("Color", bgColor);

            GUILayout.Space(10);

            // 캡쳐 버튼.
            EditorGUI.BeginDisabledGroup(!camera);
            if (GUILayout.Button("Capture"))
            {
                // 캡쳐용 임시 카메라 생성.
                Camera captureCamera = Instantiate<Camera>(camera);

                // 해상도.
                int captureWidth = (int)Mathf.Round(captureSize.x);
                int captureHeight = (int)Mathf.Round(captureSize.y);

                // 경로 적용.
                string path = Application.dataPath + "/ScreenShot/";
                DirectoryInfo dir = new DirectoryInfo(path);
                if (!dir.Exists)
                {
                    Directory.CreateDirectory(path);
                }

                // 파일명.
                string name;
                name = path + System.DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss (" + captureSize.x + "x" + captureSize.y + ")") + ".png";

                // 텍스쳐 생성.
                TextureFormat format = (bgType == BGType.Transparent) ? TextureFormat.ARGB32 : TextureFormat.RGB24;
                Texture2D screenShot = new Texture2D(captureWidth, captureHeight, format, false);
                RenderTexture rt = new RenderTexture(captureWidth, captureHeight, 24);

                // 카메라 설정.
                RenderTexture.active = rt;
                captureCamera.targetTexture = rt;
                if (bgType == BGType.Skybox)
                {
                    captureCamera.clearFlags = CameraClearFlags.Skybox;
                    captureCamera.backgroundColor = Color.white;
                }
                else if (bgType == BGType.Color)
                {
                    captureCamera.clearFlags = CameraClearFlags.SolidColor;
                    captureCamera.backgroundColor = bgColor;
                }
                else if (bgType == BGType.Transparent)
                {
                    captureCamera.clearFlags = CameraClearFlags.SolidColor;
                    captureCamera.backgroundColor = Color.clear;
                }
                captureCamera.Render();

                // 캡쳐.
                screenShot.ReadPixels(new Rect(0, 0, captureWidth, captureHeight), 0, 0);
                screenShot.Apply();

                // 저장.
                byte[] bytes = screenShot.EncodeToPNG();
                File.WriteAllBytes(name, bytes);

                AssetDatabase.Refresh();

                // 임시 카메라 제거.
                DestroyImmediate(captureCamera.gameObject);
            }
            EditorGUI.EndDisabledGroup();
        }

        // 경계선.
        void GuiLine(int i_height = 1, int padding = 5)
        {
            GUILayout.Space(padding);
            Rect rect = EditorGUILayout.GetControlRect(false, i_height);
            rect.height = i_height;
            EditorGUI.DrawRect(rect, new Color(0.5f, 0.5f, 0.5f, 1));
            GUILayout.Space(padding);
        }
    }
}