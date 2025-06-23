using System;
using UnityEngine;

namespace Assets.Scripts.Model
{
    public class AstronomicalObject : MonoBehaviour
    {
        static public double G = 6.67384e-11;
        public string Name;
        public double Mass;
        private float scaleDownFactor = 1000; // 1unit: m -> km
        public float height;

        // Replace Vuforia with OpenCV
        public OpenCVTrackingBehaviour openCVTracker;

        // Add scaleFactor property for compatibility with PlaceHolder.cs and other scripts
        public float scaleFactor
        {
            get { return scaleDownFactor; }
            set { scaleDownFactor = value; }
        }

        public float Distance(AstronomicalObject obj)
        {
            return Vector3.Distance(transform.position, obj.transform.position) * scaleDownFactor;
        }

        public double GetAttractiveForce(AstronomicalObject obj)
        {
            return (G * this.Mass * obj.Mass) / Mathf.Pow((Distance(obj) * 1000), 2f);
        }

        public void setHeight()
        {
            transform.localPosition = new Vector3(transform.localPosition.x, height, transform.localPosition.z);
        }

        private void Start()
        {
            // Get OpenCV tracker component
            if (openCVTracker == null)
            {
                openCVTracker = GetComponentInParent<OpenCVTrackingBehaviour>();
            }
        }

        private void Update()
        {
            // Replace Vuforia tracking logic with OpenCV
            if (openCVTracker != null)
            {
                if (isNearEdge())
                {
                    openCVTracker.SetTrackingStatus(OpenCVTrackingStatus.ExtendedTracked);
                }
                else if (openCVTracker.IsTracked())
                {
                    openCVTracker.SetTrackingStatus(OpenCVTrackingStatus.Tracked);
                }
            }
        }

        bool isNearEdge()
        {
            var screenPoint = Camera.main.WorldToScreenPoint(transform.position);
            if ((screenPoint.x < Screen.width / 10) || screenPoint.x > (Screen.width - Screen.width / 10) ||
                screenPoint.y < Screen.height / 10 || screenPoint.y > Screen.height - Screen.height / 10)
                return true;
            return false;
        }

        public void Lost()
        {
            print("lost");
        }
    }
}