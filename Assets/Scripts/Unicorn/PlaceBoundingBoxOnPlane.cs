using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

namespace UnityEngine.XR.ARFoundation.Annotation
{
    public class PlaceBoundingBoxOnPlane : MonoBehaviour
    {
        [SerializeField] GameObject placedPrefab;
        GameObject spawnedObject;
        ARRaycastManager raycaster;
        List<ARRaycastHit> hits = new List<ARRaycastHit>();
    
        void Start()
        {
            raycaster = GetComponent<ARRaycastManager>();
        }

        void OnPlaceBoundingBox(InputValue value)
        {
            // get the screen touch position
            Vector2 touchPosition = value.Get<Vector2>();
            // raycast frame the touchposition into the 3D scene looking for a plane
            // // if the raycast hit a plane then
            if(raycaster.Raycast(touchPosition, hits, TrackableType.PlaneWithinPolygon))
            {
                // get the hit point (pose) on the plane
                Pose hitPose = hits[0].pose;
                // if this is the first time placing an object,
                if (spawnedObject == null)
                {
                    // instantiate the prefab at the hit position and rotation
                    spawnedObject = Instantiate(placedPrefab, hitPose.position, hitPose.rotation);
                    CaptureGpuImage.label_container = spawnedObject;
                }
                else
                {
                    // change the position of the previously instantiated object
                    spawnedObject.transform.SetPositionAndRotation(hitPose.position, hitPose.rotation);
                }
            }
        }
        // Update is called once per frame
        void Update()
        {
        
        }
    }

}
