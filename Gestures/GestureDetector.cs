using Microsoft.Kinect;
using System;
using System.Collections.Generic;
using System.Windows.Shapes;

namespace KinectGame.Gestures
{
    public class GestureDetector
    {
        public event Action<SkeletonPoint> OnPaintGesture;
        public event Action OnEraseGesture;

        private Skeleton currentTrackedSkeleton;
        private List<Line> drawnLines;

        public GestureDetector(List<Line> drawnLines)
        {
            this.drawnLines = drawnLines;
        }

        public void Update(Skeleton skeleton)
        {
            currentTrackedSkeleton = skeleton;
            DetectGestures();
        }

        private void DetectGestures()
        {
            if (currentTrackedSkeleton == null) return;

            Joint handRight = currentTrackedSkeleton.Joints[JointType.HandRight];
            Joint handLeft = currentTrackedSkeleton.Joints[JointType.HandLeft];

            // Check if hand is palm down for painting
            if (handRight.TrackingState == JointTrackingState.Tracked &&
                handRight.Position.Y < currentTrackedSkeleton.Position.Y &&
                handRight.Position.Z < currentTrackedSkeleton.Position.Z)
            {
                OnPaintGesture?.Invoke(handRight.Position);
            }

            // Check for erase gesture: if the left hand is raised above the head
            if (handLeft.TrackingState == JointTrackingState.Tracked &&
                handLeft.Position.Y > currentTrackedSkeleton.Position.Y + 0.5)
            {
                OnEraseGesture?.Invoke();
            }
        }
    }
}