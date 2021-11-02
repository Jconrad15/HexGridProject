using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace TheZooMustGrow
{
    public class HexMapCamera : MonoBehaviour
    {
        private Transform swivel;
        private Transform stick;

        float zoom = 1f;
        public float stickMinZoom, stickMaxZoom;
        public float swivelMinZoom, swivelMaxZoom;

        public float moveSpeedMinZoom, moveSpeedMaxZoom;

        public float rotationSpeed;
        private float rotationAngle;

        // Bounding related parameters
        public HexGrid grid;

        private static HexMapCamera instance;

        public static bool Locked
        {
            set
            {
                instance.enabled = !value;
            }
        }

        private void Awake()
        {
            swivel = transform.GetChild(0);
            stick = swivel.GetChild(0);
        }

        void OnEnable()
        {
            instance = this;
        }

        void Update()
        {
            // Zoom 
            float zoomDelta = Input.GetAxis("Mouse ScrollWheel");
            if (zoomDelta != 0f)
            {
                AdjustZoom(zoomDelta);
            }

            // Rotate
            float rotationDelta = Input.GetAxis("Rotation");
            if (rotationDelta != 0f)
            {
                AdjustRotation(rotationDelta);
            }

            // Pan
            float xDelta = Input.GetAxis("Horizontal");
            float zDelta = Input.GetAxis("Vertical");
            if (xDelta != 0f || zDelta != 0f)
            {
                AdjustPosition(xDelta, zDelta);
            }
        }

        private void AdjustZoom(float delta)
        {
            zoom = Mathf.Clamp01(zoom + delta);

            // Zoom out
            float distance = Mathf.Lerp(stickMinZoom, stickMaxZoom, zoom);
            stick.localPosition = new Vector3(0f, 0f, distance);

            // Swivels the camera to look down as the camera zooms out
            float angle = Mathf.Lerp(swivelMinZoom, swivelMaxZoom, zoom);
            swivel.localRotation = Quaternion.Euler(angle, 0f, 0f);
        }

        private void AdjustRotation(float delta)
        {
            rotationAngle += delta * rotationSpeed * Time.deltaTime;
            
            // Wrap rotation angle to stay between 0 and 360
            if (rotationAngle < 0f)
            {
                rotationAngle += 360f;
            }
            else if (rotationAngle >= 360f)
            {
                rotationAngle -= 360f;
            }

            transform.localRotation = Quaternion.Euler(0f, rotationAngle, 0f);
        }

        private void AdjustPosition(float xDelta, float zDelta)
        {
            Vector3 direction = 
                transform.localRotation *
                new Vector3(xDelta, 0f, zDelta).normalized;

            float damping = Mathf.Max(Mathf.Abs(xDelta), Mathf.Abs(zDelta));
            float distance = 
                Mathf.Lerp(moveSpeedMinZoom, moveSpeedMaxZoom, zoom) *
                damping *
                Time.deltaTime;

            Vector3 position = transform.localPosition;
            position += direction * distance;
            transform.localPosition = ClampPosition(position);
        }

        private Vector3 ClampPosition(Vector3 position)
        {
            // The X position has a minimum of zero, and a maximum defined by the map size minus a 0.5 offset
            float xMax =
                ((grid.cellCountX * HexMetrics.chunkSizeX) - 0.5f) *
                (2f * HexMetrics.innerRadius);
            position.x = Mathf.Clamp(position.x, 0f, xMax);

            // The Z position has a minimum of zero, and a maximum defined by the map size minus a 1 offset
            float zMax =
                ((grid.cellCountZ * HexMetrics.chunkSizeZ) - 1) *
                (1.5f * HexMetrics.outerRadius);
            position.z = Mathf.Clamp(position.z, 0f, zMax);

            return position;
        }

        public static void ValidatePosition()
        {
            instance.AdjustPosition(0f, 0f);
        }

    }
}