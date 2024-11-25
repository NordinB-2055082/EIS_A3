using System.Windows;
using System.Windows.Media;
using System.Windows.Shapes;
using Emgu.CV;
using Emgu.CV.CvEnum;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Controls;
using Microsoft.Kinect;
using System.Drawing;
using KinectGame.Calibration;
using System.Windows.Threading;
using KinectGame.Gestures;

namespace KinectGame
{
    public partial class MainWindow : Window
    {
        private KinectSensor kinectSensor;
        private PartialCalibrationClass calibrationProcessor;
        private List<System.Windows.Point> screenPoints = new List<System.Windows.Point>();
        private Skeleton currentTrackedSkeleton;
        private int currentPointIndex = 0;
        private DispatcherTimer captureTimer;
        private bool isWaiting = true; // Start in "waiting" mode for the first point
        private bool isCalibrationComplete = false; // Flag to indicate calibration is complete
        private List<Line> drawnLines = new List<Line>(); // Store drawn lines
        private GestureDetector gestureDetector;

        public MainWindow()
        {
            InitializeComponent();
            InitializeKinect();
            SetupCalibrationPoints();
            SetupTimer();
            captureTimer.Start(); // Start timer for the first delay

            // Initialize the gesture detector
            gestureDetector = new GestureDetector(drawnLines);
            gestureDetector.OnPaintGesture += DrawAt;
            gestureDetector.OnEraseGesture += EraseDrawing;
        }

        private void InitializeKinect()
        {
            kinectSensor = KinectSensor.KinectSensors.FirstOrDefault(s => s.Status == KinectStatus.Connected);
            if (kinectSensor == null)
            {
                MessageBox.Show("No Kinect sensor connected.");
                Application.Current.Shutdown();
                return;
            }

            kinectSensor.SkeletonStream.Enable();
            kinectSensor.SkeletonFrameReady += KinectSkeletonFrameReady;
            kinectSensor.Start();

            calibrationProcessor = new PartialCalibrationClass(kinectSensor);
        }

        private void SetupCalibrationPoints()
        {
            // Define the corners of the calibration rectangle on the screen
            screenPoints.Add(new System.Windows.Point(200, 25));   // Top-left
            screenPoints.Add(new System.Windows.Point(800, 25));   // Top-right
            screenPoints.Add(new System.Windows.Point(800, 825));  // Bottom-right
            screenPoints.Add(new System.Windows.Point(200, 825));  // Bottom-left

            UpdateInstructions();
            MoveIndicator(screenPoints[currentPointIndex]);
        }

        private void SetupTimer()
        {
            captureTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(5) // 5-second delay for all points, including the first
            };
            captureTimer.Tick += (sender, e) =>
            {
                isWaiting = false; // Allow point capture
                captureTimer.Stop();
            };
        }

        private void KinectSkeletonFrameReady(object sender, SkeletonFrameReadyEventArgs e)
        {
            using (SkeletonFrame skeletonFrame = e.OpenSkeletonFrame())
            {
                if (skeletonFrame == null) return;

                Skeleton[] skeletons = new Skeleton[skeletonFrame.SkeletonArrayLength];
                skeletonFrame.CopySkeletonDataTo(skeletons);

                currentTrackedSkeleton = skeletons.FirstOrDefault(s => s.TrackingState == SkeletonTrackingState.Tracked);

                if (currentTrackedSkeleton != null && !isWaiting)
                {
                    CollectCalibrationPoint(screenPoints[currentPointIndex]);
                    if (isCalibrationComplete)
                    {
                        System.Windows.Point projectedPoint = calibrationProcessor.kinectToProjectionPoint(currentTrackedSkeleton.Position);
                        UpdateUser(projectedPoint);

                        // Update the gesture detector
                        gestureDetector.Update(currentTrackedSkeleton);
                    }
                }
            }
        }

        private void UpdateUser(System.Windows.Point projectedPoint)
        {
            // Round the projected point coordinates to 1 decimal place
            double roundedX = Math.Round(projectedPoint.X, 0);
            double roundedY = Math.Round(projectedPoint.Y, 0);

            // Move a visual indicator to the user's projected position
            User_Indicator.Visibility = Visibility.Visible;
            PointIndicator.Visibility = Visibility.Collapsed;
            Canvas.SetLeft(User_Indicator, roundedX - User_Indicator.Width / 2);
            Canvas.SetTop(User_Indicator, roundedY - User_Indicator.Height / 2);

            // Update a text block with the user's position, rounded to 1 decimal place
            User_PositionTextBlock.Text = $"User  Position: ({roundedX}, {roundedY})";
        }

        private void CollectCalibrationPoint(System.Windows.Point screenPoint)
        {
            if (currentTrackedSkeleton == null || isCalibrationComplete) return;

            // Prevent immediate re-capture
            isWaiting = true;
            captureTimer.Start();

            // Add the skeleton and screen points to the calibration processor
            SkeletonPoint skeletonPoint = currentTrackedSkeleton.Position;
            calibrationProcessor.AddSkeletonCalibrationPoint(skeletonPoint);
            calibrationProcessor.AddCalibrationPoint(screenPoint);

            if (calibrationProcessor.HasAllPoints())
            {
                calibrationProcessor.calibrate();
                isCalibrationComplete = true; // Mark calibration as complete
                InstructionsTextBlock.Text = "All calibration points have been captured.";
            }
            else
            {
                // Move to the next calibration point
                currentPointIndex++;
                if (currentPointIndex >= screenPoints.Count)
                {
                    InstructionsTextBlock.Text = "All calibration points have been captured.";
                    return; // Exit to prevent further processing
                }

                UpdateInstructions();
                MoveIndicator(screenPoints[currentPointIndex]);
            }
        }

        private void UpdateInstructions()
        {
            InstructionsTextBlock.Text = $"Stand in position {currentPointIndex + 1}.";
        }

        private void MoveIndicator(System.Windows.Point target)
        {
            Canvas.SetLeft(PointIndicator, target.X - PointIndicator.Width / 2);
            Canvas.SetTop(PointIndicator, target.Y - PointIndicator.Height / 2);
            PointIndicator.Visibility = Visibility.Visible;
        }

        private void DrawAt(SkeletonPoint position)
        {
            // Convert the skeleton position to screen coordinates
            System.Windows.Point screenPoint = calibrationProcessor.kinectToProjectionPoint(position);
            Line line = new Line
            {
                Stroke = System.Windows.Media.Brushes.Black,
                StrokeThickness = 10,
                X1 = Math.Round(screenPoint.X),
                Y1 = Math.Round(screenPoint.Y),
                X2 = Math.Round(screenPoint.X) + 2, // Draw a small line segment
                Y2 = Math.Round(screenPoint.Y) + 2,
            };
            CalibrationCanvas.Children.Add(line);
            drawnLines.Add(line);
        }

        private void EraseDrawing()
        {
            for (int i = 0; i < drawnLines.Count; i++)
            {
                CalibrationCanvas.Children.Remove(drawnLines[i]);
            }
            // Clear the drawn lines
            drawnLines.Clear();
        }

        private void Window_Closed(object sender, EventArgs e)
        {
            if (kinectSensor != null)
            {
                kinectSensor.Stop();
                kinectSensor.Dispose();
            }
        }
    }
}