using System;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

namespace UnityEngine.XR.ARFoundation.Annotation
{
    public class RawImageVisualizer : MonoBehaviour
    {
        Texture2D m_CameraTexture;
        public ARCameraManager CameraManager
        {
            get => _cameraManager;
            set => _cameraManager = value;
        }

        [SerializeField]
        [Tooltip("The ARCameraManager which will produce camera frame events.")]
        private ARCameraManager _cameraManager;

        public RawImage RawImage
        {
            get => _rawImage;
            set => _rawImage = value;
        }

        [SerializeField]
        [Tooltip("The UI RawImage used to display the image on screen.")]
        private RawImage _rawImage;

        void Update()
        {
            if (_cameraManager.TryAcquireLatestCpuImage(out XRCpuImage image)) // Get depth texture
            {
                using (image)
                {
                    //ScreenLog.Log(string.Format("Image info:\n\twidth: {0}\n\theight: {1}\n\tplaneCount: {2}\n\ttimestamp: {3}\n\tformat: {4}", image.width, image.height, image.planeCount, image.timestamp, image.format));

                    // Use the texture.
                    UpdateRawImage(_rawImage, image);
                
                }
            }
        }

        unsafe void UpdateRawImage(RawImage rawImage, XRCpuImage cpuImage)
        {
            var format = TextureFormat.RGB24;
            if (m_CameraTexture == null || m_CameraTexture.width != cpuImage.width || m_CameraTexture.height != cpuImage.height)
            {
                m_CameraTexture = new Texture2D(cpuImage.width, cpuImage.height, format, false);
            }
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
            rawImage.rectTransform.sizeDelta = rectSize;

            m_CameraTexture.Apply();
            _rawImage.texture = m_CameraTexture;
        }
    }

}
