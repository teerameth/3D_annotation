using System;
using System.Collections;
using System.IO;
using System.Collections.Generic;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using UnityEngine.Android;

namespace UnityEngine.XR.ARFoundation.Annotation
{
    public class CaptureGpuImage : MonoBehaviour
    {
        private string filename;
        public static GameObject label_container; // Gameobject that contain >= 1 annotation GameObject(s)
        public Camera cam;
        public void Start()
        {
            // Request External storage write permission
            if (!Permission.HasUserAuthorizedPermission(Permission.ExternalStorageWrite))
            {
                Permission.RequestUserPermission(Permission.ExternalStorageWrite);
            }
        }
        public ARCameraManager CameraManager
        {
            get => _cameraManager;
            set => _cameraManager = value;
        }

        [SerializeField]
        [Tooltip("The ARCameraManager which will produce camera frame events.")]
        private ARCameraManager _cameraManager;

        public void Capture()
        {
            ScreenLog.Log("Capture GPU image");
            if (_cameraManager.TryAcquireLatestCpuImage(out XRCpuImage image)) // Get depth texture
            {
                ScreenLog.Log("Try Convert CPU image");
                using (image)
                {
                    ScreenLog.Log("Convert CPU image");
                    filename = String.Format("{0:yyyy-MM-dd}", DateTime.Now);    // Generate filename
                    saveCPU(image, filename);   // Convert CPU image to Texture2D
                }
            }
            else
            {
                ScreenLog.Log("Convert Error");
            }
        }
        unsafe void saveCPU(XRCpuImage cpuImage, string filename)
        {
            var format = TextureFormat.RGB24;
            Texture2D m_CameraTexture = new Texture2D(cpuImage.width, cpuImage.height, format, false);
            
            var conversionParams = new XRCpuImage.ConversionParams(cpuImage, format, XRCpuImage.Transformation.MirrorX);

            var rawTextureData = m_CameraTexture.GetRawTextureData<byte>();
            try
            {
                cpuImage.Convert(conversionParams, new IntPtr(rawTextureData.GetUnsafePtr()), rawTextureData.Length);
            }
            finally
            {
                // We must dispose of the XRCpuImage after we're finished
                // with it to avoid leaking native resources.
                cpuImage.Dispose();
            }

            // Adjust Image Plane Size Automaically //
            // Get the aspect ratio for the current texture.
            var textureAspectRatio = (float)m_CameraTexture.width / m_CameraTexture.height;

            // Determine the raw image rectSize preserving the texture aspect ratio, matching the screen orientation,
            // and keeping a minimum dimension size.
            const float minDimension = 480.0f;
            var maxDimension = Mathf.Round(minDimension * textureAspectRatio);
            var rectSize = new Vector2(maxDimension, minDimension);
            //var rectSize = new Vector2(minDimension, maxDimension);   //Portrait
            m_CameraTexture.Apply();

            // Encode texture into PNG
            byte[] bytes = m_CameraTexture.EncodeToPNG();
            File.WriteAllBytes(Application.persistentDataPath + filename + ".png", bytes);
            ScreenLog.Log(Application.dataPath + filename + ".png");

        }

        void save3D() // Save RAW 3D pose of 3D bounding box
        {

        }

        void save2D() // Save 2D bounding box (projected from 3D bounding box)
        {

        }

        public void label2D()
        {
            Capture();
            save2D();
        }
        public void label3D()
        {
            Capture();
            save3D();
        }

        void OnGUI()
        {
            if (label_container != null)
            {
                Rect visualRect = Get_object_bounding_box(label_container, cam);
                Debug.Log(visualRect.ToString());
                GUI.color = Color.black;
                Rect visualRect_mirrorX = visualRect;
                visualRect_mirrorX.yMin = Screen.height - visualRect_mirrorX.yMin;
                visualRect_mirrorX.yMax = Screen.height - visualRect_mirrorX.yMax;
                GUI.Box(visualRect_mirrorX, "Hello World!");
            }
            else
            {
                Debug.Log("Label not found");
            }
            
        }
        private Rect Get_object_bounding_box(GameObject game_object, Camera cam)
        {
            // This is a relativly intense way as it is looking at each mesh point to calculate the bounding box.
            // but it produces perfect bounding boxes so yolo.

            // get the mesh points
            Vector3[] vertices = game_object.GetComponent<MeshFilter>().mesh.vertices;

            // apply the world transforms (position, rotation, scale) to the mesh points and then get their 2D position
            // relative to the camera
            Vector2[] vertices_2d = new Vector2[vertices.Length];
            for (var i = 0; i < vertices.Length; i++)
            {
                vertices_2d[i] = cam.WorldToScreenPoint(game_object.transform.TransformPoint(vertices[i]));
            }

            // find the min max bounds of the 2D points
            Vector2 min = vertices_2d[0];
            Vector2 max = vertices_2d[0];
            foreach (Vector2 vertex in vertices_2d)
            {
                min = Vector2.Min(min, vertex);
                max = Vector2.Max(max, vertex);
            }

            // thats our perfect bounding box
            return new Rect(min.x, min.y, max.x - min.x, max.y - min.y);
        }
    }
}