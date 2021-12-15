using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using System.IO;
using UnityEngine.InputSystem;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine.Android;
using TMPro;


namespace UnityEngine.XR.ARFoundation.Annotation
{
    public class MultiLabelerTest : MonoBehaviour
    {
        private string filename;
        public Camera cam;
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
                    filename = System.DateTime.UtcNow.ToLocalTime().ToString("yyyy-MM-dd-HH-mm-ss");  // // Generate filename from date time
                    //System.DateTime.Now.Year.ToString()
                    //filename = String.Format("{0:yyyy-MM-dd}", DateTime.Now);    // Generate filename
                    Debug.Log(filename);
                    saveCPU(image, filename);   // Convert CPU image to Texture2D
                    dumpLabel(filename);
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
            File.WriteAllBytes(Application.persistentDataPath + "/" + filename + ".png", bytes);
            ScreenLog.Log(Application.dataPath + "/" + filename + ".png");
        }

        class Label
        {
            public GameObject obj;
            public string name;
            public int type;                    // 0=cube, 1=sphere
            public Label(string name, GameObject obj) // Constructor
            {
                this.name = name;               // Init label name
                this.obj = obj;                 // Assign GameObject of boundingbox
                this.type = 0;                  // Cube is default type
            }
        }
        private static List<Vector3> pos = new List<Vector3>();
        private static List<Vector3> rot = new List<Vector3>();
        private static List<Vector3> size = new List<Vector3>();
        public List<GameObject> LabelBoxPrefab;
        public TMP_Dropdown BoundingBoxSelector;      // Dropdown for selecting active label gameObject
        public Button AddBoundingBox;                       // Add new label gameObject
        public TMP_Dropdown formatSelector;           // Dropdown for selecting label type (cube/sphere)
        public TMP_InputField LabelNameField;         // Input field for selected gameObject name (string)
        private List<Label> labels = new List<Label>();     // List of all avaliable annotation object
        private List<string> labelNames = new List<string>();   // List of string to keep labeled object name

        public TMP_InputField xSizeField;             // Input field for annotation size in X axis
        public TMP_InputField ySizeField;             // Input field for annotation size in Y axis
        public TMP_InputField zSizeField;             // Input field for annotation size in Z axis
        public TMP_Text sizeControlCaption;                     // Caption text for size control panel
        public Button sizeApplyButton;                      // Button to apply object size from text field

        public Button PosRotToggleButton;                   // Button toggle between position control & rotation control
        public Button xMove, yMove, zMove;                  // Button to move object (Drag)
        public float moveSensitivity = 0.01f;                // Drag to move sesitivity (1 is way too much)

        public GameObject PlacePanel;                       // Panel for touch input (place object on planr in AR session)

        public GameObject AxisIndicator;                    // GameObject draw axis (locked in canvas coordinate & fixed rotation)

        private Label activeLabel                           // Selected Label Object (All editing will apply here)
        {
            get => labels[BoundingBoxSelector.value];
        }
        private int activeIndex
        {
            get => BoundingBoxSelector.value;
        }
        private bool _rotControlMode = true;               // Rotation control mode
        private Quaternion _rotationSave;                   // Buffer to save rotation for locking
        private List<string> formatOptions = new List<string>()
        {
            "CUBE",
            "SPHERE"
        };

        private Camera camera;

        public void dumpLabel(string filename)
        {
            PosRotList newPosRotList = new PosRotList(pos, rot, size);
            string dataAsJson = JsonUtility.ToJson(newPosRotList);
            string filepath = Application.persistentDataPath + "/" + filename + ".json";
            File.WriteAllText(filepath, dataAsJson);
        }

        void CreateNewLabel()
        {
            GameObject newObj = Instantiate(LabelBoxPrefab[0], Vector3.zero, Quaternion.identity);          // Spawn default cube
            string newName = labels.Count.ToString();                   // Use index number as default label
            Label newLabel = new Label(newName, newObj);                // Create new Label
            labels.Add(newLabel);                                       // Append new Label to the list
            BoundingBoxSelector.value = labels.Count - 1;                 // Change active object to the new one (last index)
            List<string> newNameList = new List<string>() { newName };
            BoundingBoxSelector.AddOptions(newNameList);                // Append new Label to Dropdown
            BoundingBoxSelector.value = labels.Count - 1;               // Select the most recent box
        }

        void OnCreateButton()
        {
            CreateNewLabel();               // Crate new label & append to list
        }

        void OnChangeType(TMPro.TMP_Dropdown dropdown)
        {
            int index = dropdown.value; // Get dropdown value
            activeLabel.type = index;
            // Copy original position & rotation
            Vector3 position_save = activeLabel.obj.transform.position;
            Quaternion rotation_save = activeLabel.obj.transform.rotation;
            GameObject oldObject = activeLabel.obj;
            // Create new one (same position & rotation)
            activeLabel.obj = Instantiate(LabelBoxPrefab[index], position_save, rotation_save);
            Destroy(oldObject);
            switch (index)
            {
                case 0: // BOX
                    sizeControlCaption.text = "Object size (Cartesian)";
                    xSizeField.text = activeLabel.obj.transform.localScale.x.ToString();    // Display current X size
                    ySizeField.gameObject.SetActive(true);
                    zSizeField.gameObject.SetActive(true);
                    ySizeField.text = activeLabel.obj.transform.localScale.y.ToString();    // Display current Y size
                    zSizeField.text = activeLabel.obj.transform.localScale.z.ToString();    // Display current Z size
                    break;
                case 1: // SPHERE
                    sizeControlCaption.text = "Object size (Radius)";
                    xSizeField.text = activeLabel.obj.transform.localScale.x.ToString();    // Display current Radius
                    ySizeField.gameObject.SetActive(false);
                    zSizeField.gameObject.SetActive(false);
                    break;
                default:
                    break;
            }
        }

        void OnChangeActiveObject(TMPro.TMP_Dropdown dropdown)
        {
            int index = dropdown.value; // Get dropdown value
            LabelNameField.text = BoundingBoxSelector.options[index].text;          // Display current label name

            switch (activeLabel.type)
            {
                case 0: // BOX
                    xSizeField.text = activeLabel.obj.transform.localScale.x.ToString();    // Display current X size
                    ySizeField.gameObject.SetActive(true);
                    zSizeField.gameObject.SetActive(true);
                    ySizeField.text = activeLabel.obj.transform.localScale.y.ToString();    // Display current Y size
                    zSizeField.text = activeLabel.obj.transform.localScale.z.ToString();    // Display current Z size
                    break;
                case 1: // SPHERE
                    xSizeField.text = activeLabel.obj.transform.localScale.x.ToString();    // Display current Radius
                    ySizeField.gameObject.SetActive(false);
                    zSizeField.gameObject.SetActive(false);
                    break;
                default:
                    break;
            }
        }

        private Quaternion _rotationDelta;   // Quarternion for saving reference between camera & active object
        private Vector3 _deltaAngle;

        void OnNameChange()
        {
            //labelNames[BoundingBoxSelector.value] = LabelNameField.text;
            BoundingBoxSelector.options[BoundingBoxSelector.value].text = LabelNameField.text;
            BoundingBoxSelector.captionText.text = LabelNameField.text;
        }

        public Text posRotToggleButtonText;
        void OnToggleControlMode()
        {
            _rotControlMode = !_rotControlMode;
            if (_rotControlMode)
            {
                PosRotToggleButton.GetComponent<Image>().color = Color.red;
                PosRotToggleButton.GetComponentInChildren<TMP_Text>().text = "Rotation";
            }
            else
            {
                PosRotToggleButton.GetComponent<Image>().color = Color.green;
                PosRotToggleButton.GetComponentInChildren<TMP_Text>().text = "Position";
            }
        }

        void OnSizeApply()
        {
            Vector3 newSize = activeLabel.obj.transform.localScale;
            switch (activeLabel.type)
            {
                case 0: // BOX
                    newSize = new Vector3(float.Parse(xSizeField.text),
                                            float.Parse(ySizeField.text),
                                            float.Parse(zSizeField.text));
                    break;
                case 1: // SPHERE (use X as radius)
                    newSize = new Vector3(float.Parse(xSizeField.text),
                                            float.Parse(xSizeField.text),
                                            float.Parse(xSizeField.text));
                    break;
                default:
                    break;
            }
            activeLabel.obj.transform.localScale = newSize;
        }


        void Start()
        {
            // Request External storage write permission
            if (!Permission.HasUserAuthorizedPermission(Permission.ExternalStorageWrite))
            {
                Permission.RequestUserPermission(Permission.ExternalStorageWrite);
            }

            camera = this.GetComponent<ARSessionOrigin>().camera;
            raycaster = GetComponent<ARRaycastManager>();
            // Button add new bounding box
            AddBoundingBox.onClick.AddListener(OnCreateButton);
            // Dropdown select format
            formatSelector.ClearOptions();     // Clear default options
            formatSelector.AddOptions(formatOptions);
            formatSelector.onValueChanged.AddListener(delegate
            {
                OnChangeType(formatSelector);
            });
            // Dropdown select active box
            BoundingBoxSelector.ClearOptions();     // Clear default options
            BoundingBoxSelector.onValueChanged.AddListener(delegate
            {
                OnChangeActiveObject(BoundingBoxSelector);
            });
            // Object Size Apply Button
            sizeApplyButton.onClick.AddListener(OnSizeApply);
            // Object Name Field
            LabelNameField.onValueChanged.AddListener(delegate
            {
                OnNameChange();
            });
            // Button Toggle Pos-Rot mode
            PosRotToggleButton.onClick.AddListener(OnToggleControlMode);



            // init first label gameObject
            CreateNewLabel();

            // Update Object Size Field
            xSizeField.text = activeLabel.obj.transform.localScale.x.ToString();    // Display current X size
            ySizeField.text = activeLabel.obj.transform.localScale.y.ToString();    // Display current Y size
            zSizeField.text = activeLabel.obj.transform.localScale.z.ToString();    // Display current Z size
            // Update Object Name Field
            LabelNameField.text = activeLabel.name;
            OnToggleControlMode();
        }

        private static int _touchMode;      // Indicate touchmode
        private static bool _isTouched;     // Indicate if there was touch in last frame
        Touch touch;
        void checkTouch(Touch touch)
        {
            if (_isTouched) return;  // Not change during finger down
            if (RectTransformUtility.RectangleContainsScreenPoint(xMove.GetComponent<RectTransform>(), touch.position)) _touchMode = 0;
            else if (RectTransformUtility.RectangleContainsScreenPoint(yMove.GetComponent<RectTransform>(), touch.position)) _touchMode = 1;
            else if (RectTransformUtility.RectangleContainsScreenPoint(zMove.GetComponent<RectTransform>(), touch.position)) _touchMode = 2;
            else if (RectTransformUtility.RectangleContainsScreenPoint(PlacePanel.GetComponent<RectTransform>(), touch.position)) _touchMode = 3;
            else _touchMode = -1;
            _isTouched = true; // activate touch indicator
        }

        ARRaycastManager raycaster;
        List<ARRaycastHit> hits = new List<ARRaycastHit>();
        private void Update()
        {
            // Update pos, rot export variable
            while (pos.Count < labels.Count) pos.Add(new Vector3(0, 0, 0));
            while (rot.Count < labels.Count) rot.Add(new Vector3(0, 0, 0));
            for (int i = 0; i < labels.Count; i++)
            {
                pos[i] = labels[i].obj.transform.position;
                rot[i] = labels[i].obj.transform.rotation.eulerAngles;
                size[i] = labels[i].obj.transform.localScale;
            }

            // Move axis indicator
            //AxisIndicator.transform.position = FindObjectOfType<ARSessionOrigin>().camera.ScreenToWorldPoint(new Vector3(300, 150, 10));
            AxisIndicator.transform.position = camera.ScreenToWorldPoint(new Vector3(300, 150, 10));

            if (Input.touchCount > 0)
            {
                touch = Input.GetTouch(0);
                checkTouch(touch);  // update _moveAxis
                if (touch.phase == UnityEngine.TouchPhase.Moved)
                {
                    float delta = touch.deltaPosition.x;
                    if (_rotControlMode)    // Rotation Control Mode
                    {
                        switch (_touchMode)
                        {
                            case 0:
                                activeLabel.obj.transform.Rotate(delta * 0.1f, 0, 0, Space.Self);
                                break;
                            case 1:
                                activeLabel.obj.transform.Rotate(0, delta * 0.1f, 0, Space.Self);
                                break;
                            case 2:
                                activeLabel.obj.transform.Rotate(0, 0, delta * 0.1f, Space.Self);
                                break;
                            case 3:
                                if (raycaster.Raycast(touch.position, hits, TrackableType.PlaneWithinPolygon))
                                {
                                    Pose hitPose = hits[0].pose; // get the hit point (pose) on the plane
                                    activeLabel.obj.transform.SetPositionAndRotation(hitPose.position, hitPose.rotation);
                                }
                                break;
                            default:
                                break;
                        }
                    }
                    else                    // Position Control Mode
                    {
                        switch (_touchMode)
                        {
                            case 0:
                                activeLabel.obj.transform.Translate(delta * moveSensitivity, 0, 0, Space.World);
                                break;
                            case 1:
                                activeLabel.obj.transform.Translate(0, delta * moveSensitivity, 0, Space.World);
                                break;
                            case 2:
                                activeLabel.obj.transform.Translate(0, 0, delta * moveSensitivity, Space.World);
                                break;
                            case 3:
                                if (raycaster.Raycast(touch.position, hits, TrackableType.PlaneWithinPolygon))
                                {
                                    Pose hitPose = hits[0].pose; // get the hit point (pose) on the plane
                                    activeLabel.obj.transform.SetPositionAndRotation(hitPose.position, hitPose.rotation);
                                }
                                break;
                            default:
                                break;
                        }
                    }
                }
            }
            else
            {
                _isTouched = false; // deactivate touch indicator
                _touchMode = -1;     // stop moving
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

    [Serializable]
    public class PosRotList
    {
        public List<Vector3> pos;
        public List<Vector3> rot;
        public List<Vector3> size;
        public PosRotList(List<Vector3> pos, List<Vector3> rot, List<Vector3> size)
        {
            this.pos = pos;
            this.rot = rot;
            this.size = size;
        }
    }
}

