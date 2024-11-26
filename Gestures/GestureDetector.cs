using Microsoft.Kinect;
using System.Collections.Generic;
using System.Windows.Media;
using System.Windows.Shapes;
using System;
using Emgu.CV.XObjdetect;

public class GestureDetector
{
    public event Action<SkeletonPoint, Color> OnPaintGesture; // Now includes color
    public event Action<Color> OnEraseGesture;

    private Skeleton currentTrackedSkeleton1;
    private Skeleton currentTrackedSkeleton2;
    private List<Line> drawnLinesPlayer1;
    private List<Line> drawnLinesPlayer2;

    public GestureDetector()
    {
        drawnLinesPlayer1 = new List<Line>();
        drawnLinesPlayer2 = new List<Line>();
    }

    public void Update(Skeleton skeleton1, Skeleton skeleton2)
    {
        currentTrackedSkeleton1 = skeleton1;
        currentTrackedSkeleton2 = skeleton2;
        DetectGestures();
    }

    private void DetectGestures()
    {
        DetectPlayerGestures(currentTrackedSkeleton1, Colors.Black, drawnLinesPlayer1);
        DetectPlayerGestures(currentTrackedSkeleton2, Colors.Red, drawnLinesPlayer2);
    }

    private void DetectPlayerGestures(Skeleton skeleton, Color color, List<Line> drawnLines)
    {
        if (skeleton == null) return;

        Joint handRight = skeleton.Joints[JointType.HandRight];
        Joint handLeft = skeleton.Joints[JointType.HandLeft];

        // Check if hand is palm down for painting
        if (handRight.TrackingState == JointTrackingState.Tracked &&
            handRight.Position.Y < skeleton.Position.Y &&
            handRight.Position.Z < skeleton.Position.Z)
        {
            OnPaintGesture?.Invoke(handRight.Position, color);
        }

        // Check for erase gesture: if the left hand is raised above the head
        if (handLeft.TrackingState == JointTrackingState.Tracked &&
            handLeft.Position.Y > skeleton.Position.Y + 0.5)
        {
            OnEraseGesture?.Invoke(color);
        }
    }
}