using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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
        public float scaleFactor = 1;
        public DefaultObserverEventHandler eventHandler;
        public float Distance(AstronomicalObject obj)
        {
            return Vector3.Distance(transform.position, obj.transform.position) * scaleDownFactor; 
        }
        public double GetAttractiveForce(AstronomicalObject obj)
        {
            return (G * this.Mass * obj.Mass) / Mathf.Pow((Distance(obj)*1000),2f);
        }
        public void setHeight()
        {
            transform.localPosition = new Vector3(transform.localPosition.x, height, transform.localPosition.z);
        }
        private void Update()
        {
            //if (eventHandler == null) return;
            //if (isNearEdge())
            //    eventHandler.StatusFilter = DefaultObserverEventHandler.TrackingStatusFilter.Tracked;
            //else
            //    eventHandler.StatusFilter = DefaultObserverEventHandler.TrackingStatusFilter.Tracked_ExtendedTracked;
        }
        bool isNearEdge()
        {
            var screenPoint = Camera.main.WorldToScreenPoint(transform.position);
            if ((screenPoint.x < Screen.width / 10) || screenPoint.x > (Screen.width - Screen.width / 10) || screenPoint.y < Screen.height / 10 || screenPoint.y > Screen.height - Screen.height / 10)
                return true;
            return false;
        }
        public void Lost()
        {
            print("lost");
        }

    }
}
